using System.Reflection;
using libMBIN;
using Microsoft.EntityFrameworkCore;
using ArtifactX.Tools.DataCataloger.Data;
using ArtifactX.Tools.DataCataloger.Models;
using ArtifactX.Tools.DataCataloger.Services.Interfaces;

namespace ArtifactX.Tools.DataCataloger.Services;

public class CatalogBuildService
{
    private readonly IPakDiscoveryService _discovery;
    private readonly IPakReaderService _pakReader;
    private readonly ExtractionService _extraction;

    // ArtifactX has shipped several generations of loc tables across its many expansions
    // (loc1, loc4, loc5, ... but also "update3" - Hello Games doesn't keep a single
    // consistent prefix), each adding keys for newer content. Some groups use
    // "_english", newer ones use "_usenglish" instead. We need ALL of them merged, or
    // names for anything added after whichever generation happened to be first simply
    // won't resolve. Matching any alphabetic prefix (not hardcoding "loc") so a future
    // third naming scheme doesn't silently get skipped the same way "update3" was.
    private static readonly System.Text.RegularExpressions.Regex LocFilePattern =
        new(@"language/nms_[a-z]+\d+_(us)?english\.mbin$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public CatalogBuildService(IPakDiscoveryService discovery, IPakReaderService pakReader, ExtractionService extraction)
    {
        _discovery = discovery;
        _pakReader = pakReader;
        _extraction = extraction;
    }

    public void Run(string pcbanksPath, string dbOutputPath, int iconTargetSize = 128)
    {
        var paks = _discovery.Discover(pcbanksPath);
        LogService.Write($"CatalogBuild: found {paks.Count} PAK files.");

        // -------------------------------------------------------------
        // Phase 0: index every file in every PAK (needed later for icons,
        // which usually live in a different PAK than the table that
        // references them), while also caching each PAK's (header, entries)
        // so we don't re-parse the index twice.
        // -------------------------------------------------------------
        var fileIndex = new GlobalFileIndexService();
        var pakData = new List<(string PakPath, PakHeader Header, IReadOnlyList<PakEntry> Entries)>();

        foreach (var pak in paks)
        {
            try
            {
                var header = _pakReader.ReadHeader(pak.FullPath);
                var entries = _pakReader.Read(pak.FullPath);

                fileIndex.Add(pak.FullPath, header, entries);
                pakData.Add((pak.FullPath, header, entries));
            }
            catch (Exception ex)
            {
                LogService.Write($"CatalogBuild: failed to index {pak.FileName}: {ex.Message}");
            }
        }

        LogService.Write($"CatalogBuild: indexed {fileIndex.Count} files across all PAKs.");

        // -------------------------------------------------------------
        // Phase 1: resolve localisation FIRST. Row classification needs the
        // full loc-key set to decide which string fields are "name-shaped",
        // so this has to happen before the main sweep, not during it.
        // Merge every loc-generation's English file, not just one.
        // -------------------------------------------------------------
        var englishLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var locEntryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (pakPath, header, entries) in pakData)
        {
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.FileName) || !LocFilePattern.IsMatch(entry.FileName))
                    continue;

                locEntryPaths.Add(entry.FileName);

                var locTemplate = DecodeMbin(pakPath, entry, header);
                if (locTemplate == null) continue;

                var thisLookup = LocalisationService.BuildEnglishLookup(locTemplate);
                foreach (var kvp in thisLookup)
                    englishLookup[kvp.Key] = kvp.Value; // later files can safely override earlier ones

                LogService.Write($"CatalogBuild: merged {thisLookup.Count} entries from {entry.FileName} (total now {englishLookup.Count}).");
            }
        }

        if (englishLookup.Count == 0)
            LogService.Write("CatalogBuild: WARNING - no localisation table found. Names will be stored as loc keys only.");

        // -------------------------------------------------------------
        // Phase 2: classify every MBIN across every PAK. Skip the loc table
        // itself - it technically matches our own "has a List<T> with an Id
        // field" heuristic, but it isn't a gameplay catalog.
        // -------------------------------------------------------------
        var iconService = new IconExtractionService(_extraction, fileIndex, iconTargetSize);
        var iconBlobCache = new Dictionary<string, IconBlob>(StringComparer.OrdinalIgnoreCase);
        var categories = new List<CatalogCategory>();
        int totalRows = 0, totalIcons = 0;

        // -------------------------------------------------------------
        // Phase 1.5: Multi-tool "Type" (base model) discovery. No in-game data
        // table maps a Type display name to a model path - confirmed by
        // searching every classified table, grepping raw MBIN field values for
        // the string, and dumping the one plausible candidate's actual structure
        // (see project history / DataCataloger's `inspect`/`grep`/`dumpfile`
        // commands). The game itself has no such table either - "Type" IS the
        // model path (NTx.93M in the save), confirmed by a real in-game test
        // showing the visual swap needs no other field.
        //
        // So instead of a curated list, this is a FILENAME RULE applied to the
        // real, already-indexed PAK entries: every .SCENE.MBIN directly under
        // weapons/multitool/ whose name ends in "MULTITOOL.SCENE.MBIN" is a
        // genuine base-model candidate (confirmed against the full 38-file
        // weapons/multitool scan - this exact pattern cleanly separates every
        // real candidate from every muzzle-flash/projectile/sub-part/effect
        // file, none of which match it). Only NPC-only and platform-exclusive
        // (Switch crossover) variants are excluded, by name - a small, stable
        // exception list, not a growing inclusion list. Re-running the build
        // after a game update picks up new Types automatically; nothing here
        // needs manual maintenance unless Hello Games adds a new file that
        // ALSO needs excluding for the same NPC/platform-exclusive reasons.
        // -------------------------------------------------------------
        var multiToolTypeCategory = new CatalogCategory
        {
            TemplateType = "MultiToolTypes",
            RowType = "SceneModelPath",
            SourceMbinPath = "models/common/weapons/multitool/*MULTITOOL.SCENE.MBIN (filename rule, not a data table)"
        };

        var seenModelPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, _, entries) in pakData)
        {
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.FileName)) continue;

                string upperPath = entry.FileName.ToUpperInvariant();
                if (!upperPath.Contains("WEAPONS/MULTITOOL/")) continue;
                if (!upperPath.EndsWith("MULTITOOL.SCENE.MBIN")) continue;
                if (upperPath.Contains("NPC")) continue;
                if (upperPath.Contains("SWITCH")) continue;
                if (!seenModelPaths.Add(upperPath)) continue;

                multiToolTypeCategory.Items.Add(new CatalogItem
                {
                    GameId = upperPath,
                    NameEnglish = DeriveMultiToolTypeName(upperPath)
                });
            }
        }

        if (multiToolTypeCategory.Items.Count > 0)
        {
            categories.Add(multiToolTypeCategory);
            LogService.Write($"CatalogBuild: discovered {multiToolTypeCategory.Items.Count} multi-tool Type model paths.");
        }

        // -------------------------------------------------------------
        // Phase 1.55: Freighter "Type" (base model) discovery - same filename
        // rule idea as Multi-Tool Types. A first pass wrongly concluded every
        // freighter uses one shared model, based on sampling only one save;
        // cross-checking real saves from 3 different characters found THREE
        // distinct model paths actually in use (PIRATEFREIGHTER,
        // CAPITALFREIGHTER_PROC, FREIGHTER_PROC), all living directly under
        // models/common/spacecraft/industrial/ - so "Type" IS the model path
        // here too (bIR.93M in the save), same mechanism as Ships/Multi-Tool.
        //
        // Filename rule: every .SCENE.MBIN directly under spacecraft/industrial/
        // (not a subfolder - those are sub-components like hull/cargo/engine
        // pieces, not full swappable base models). Excludes legacy/destroyed/
        // trench/LOD/switch/cruiser/inventory-effect/decoration files by name -
        // a small, stable exception list, not a growing inclusion list. Unlike
        // Multi-Tool Types, individual entries beyond the three confirmed above
        // haven't each been verified in a real save - flagged here so a future
        // pass knows to treat any newly-surfaced entry as unconfirmed until seen
        // in real player data.
        // -------------------------------------------------------------
        var freighterTypeCategory = new CatalogCategory
        {
            TemplateType = "FreighterTypes",
            RowType = "SceneModelPath",
            SourceMbinPath = "models/common/spacecraft/industrial/*.SCENE.MBIN (filename rule, not a data table)"
        };

        var seenFreighterModelPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        const string industrialFolder = "SPACECRAFT/INDUSTRIAL/";

        foreach (var (_, _, entries) in pakData)
        {
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.FileName)) continue;

                string upperPath = entry.FileName.ToUpperInvariant();
                int industrialIdx = upperPath.IndexOf(industrialFolder, StringComparison.Ordinal);
                if (industrialIdx < 0) continue;
                if (!upperPath.EndsWith(".SCENE.MBIN")) continue;

                // Must be directly in the folder, not a nested subfolder (hull/,
                // cargo/, engine/, etc. hold sub-components, not full models).
                string afterIndustrial = upperPath[(industrialIdx + industrialFolder.Length)..];
                if (afterIndustrial.Contains('/')) continue;

                if (upperPath.Contains("LEGACY")) continue;
                if (upperPath.Contains("DESTROYED")) continue;
                if (upperPath.Contains("TRENCH")) continue;
                if (upperPath.Contains("LOD")) continue;
                if (upperPath.Contains("SWITCH")) continue;
                if (upperPath.Contains("CRUISER")) continue;
                if (upperPath.Contains("INVENTORY_")) continue;
                if (upperPath.Contains("DECORATION")) continue;
                if (upperPath.Contains("TORPEDO")) continue;
                // Real, distinctly different NPC-only craft (station trade
                // traffic and a one-off unnamed ship) that happen to sit in
                // the same folder - neither is a real player freighter hull.
                if (upperPath.Contains("SMALLTRANSPORT")) continue;
                if (upperPath.Contains("FREIGHTSHIP")) continue;
                if (!seenFreighterModelPaths.Add(upperPath)) continue;

                freighterTypeCategory.Items.Add(new CatalogItem
                {
                    GameId = upperPath,
                    NameEnglish = DeriveFreighterTypeName(upperPath)
                });
            }
        }

        if (freighterTypeCategory.Items.Count > 0)
        {
            categories.Add(freighterTypeCategory);
            LogService.Write($"CatalogBuild: discovered {freighterTypeCategory.Items.Count} freighter Type model paths.");
        }

        // -------------------------------------------------------------
        // Phase 1.56: Freighter Crew Race discovery. The freighter's crew
        // captain is a separate model reference (Sjw.93M in the save) from the
        // freighter hull itself (bIR.93M) - confirmed by a real save showing
        // Sjw.93M = ".../NPCVYKEEN.SCENE.MBIN" with Sjw.@EL's seed exactly
        // matching a reference tool's own "Crew Seed" field for the same freighter.
        //
        // Filename rule: every NPC*.SCENE.MBIN directly under
        // player/playercharacter/, limited to the three real playable species
        // (Gek/Korvax/Vy'keen) plus a fourth "Robot" captain model - the
        // folder also holds several NON-race NPCs (Nada/Polo - unique Atlas
        // Emissary characters, Fourth/Fifth - Expedition story-unique
        // characters, Settler/SpecialShop/Unique/Astro/RobotSpider/RobotTorso -
        // vendor or sub-part models) which are deliberately excluded by an
        // explicit inclusion list rather than an exclusion list, since "is this
        // a real selectable crew race" isn't reliably detectable from the
        // filename alone the way Multi-Tool/Freighter Type files are.
        // -------------------------------------------------------------
        var freighterCrewRaceCategory = new CatalogCategory
        {
            TemplateType = "FreighterCrewRaces",
            RowType = "SceneModelPath",
            SourceMbinPath = "models/common/player/playercharacter/npc{gek,korvax,vykeen,robot}.scene.mbin (filename rule, not a data table)"
        };

        var includedCrewRaceFiles = new[] { "NPCGEK.SCENE.MBIN", "NPCKORVAX.SCENE.MBIN", "NPCVYKEEN.SCENE.MBIN", "NPCROBOT.SCENE.MBIN" };
        var seenCrewRacePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, _, entries) in pakData)
        {
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.FileName)) continue;

                string upperPath = entry.FileName.ToUpperInvariant();
                if (!upperPath.Contains("PLAYER/PLAYERCHARACTER/")) continue;
                if (!includedCrewRaceFiles.Any(f => upperPath.EndsWith(f, StringComparison.Ordinal))) continue;
                if (!seenCrewRacePaths.Add(upperPath)) continue;

                freighterCrewRaceCategory.Items.Add(new CatalogItem
                {
                    GameId = upperPath,
                    NameEnglish = DeriveCrewRaceName(upperPath)
                });
            }
        }

        if (freighterCrewRaceCategory.Items.Count > 0)
        {
            categories.Add(freighterCrewRaceCategory);
            LogService.Write($"CatalogBuild: discovered {freighterCrewRaceCategory.Items.Count} freighter crew race model paths.");
        }

        // -------------------------------------------------------------
        // Phase 1.6: Ship Technology/Cargo capacity per (ship type, class letter).
        // Unlike Multi-Tool Types, this IS a real in-game data table, not a
        // filename rule: metadata/reality/tables/inventorytable.mbin decodes to
        // GcInventoryTable, whose ShipInventoryMaxUpgradeSize array (indexed by
        // GcSpaceshipClasses.ShipClassEnum) holds a GcShipInventoryMaxUpgradeCapacity
        // per ship type, each with MaxInventoryCapacity/MaxTechInventoryCapacity
        // arrays indexed by GcInventoryClass.InventoryClassEnum (C/B/A/S) -
        // confirmed against a real install via DataCataloger's dumpfile command.
        // MaxCargoInventoryCapacity (a third, separate array on that same type)
        // was 0 in every entry sampled - the real Cargo total lives in
        // MaxInventoryCapacity instead, so that's what gets stored as "CARGO"
        // here. Stored as plain CatalogItem rows (two per type+class, CARGO and
        // TECH) rather than hardcoded in the app, so a game update that changes
        // these numbers - or adds a new ship type - is picked up by just
        // re-running the cataloger, no code change needed.
        // -------------------------------------------------------------
        var shipCapacityCategory = new CatalogCategory
        {
            TemplateType = "ShipCapacity",
            RowType = "ShipTypeClassCapacity",
            SourceMbinPath = "metadata/reality/tables/inventorytable.mbin (GcInventoryTable.ShipInventoryMaxUpgradeSize)"
        };

        // Multi-Tool Technology capacity, extracted from the SAME decoded table
        // below - GcInventoryTable.WeaponInventoryMaxUpgradeSize. Unlike ships,
        // this is a single GcWeaponInventoryMaxUpgradeCapacity (not an array per
        // type) with just one MaxInventoryCapacity[C/B/A/S] array - confirmed
        // multi-tool capacity does NOT vary by Type/shape (Pistol/Rifle/Staff/
        // etc.), only by Class, matching real game mechanics. Replaces
        // MultiToolCapacity.cs's old flat "best guess, not verified" constant
        // (6 rows regardless of class) with the real per-class numbers.
        var multiToolCapacityCategory = new CatalogCategory
        {
            TemplateType = "MultiToolCapacity",
            RowType = "ClassCapacity",
            SourceMbinPath = "metadata/reality/tables/inventorytable.mbin (GcInventoryTable.WeaponInventoryMaxUpgradeSize)"
        };

        foreach (var (pakPath, header, entries) in pakData)
        {
            var inventoryTableEntry = entries.FirstOrDefault(e =>
                !string.IsNullOrEmpty(e.FileName) &&
                e.FileName.EndsWith("inventorytable.mbin", StringComparison.OrdinalIgnoreCase));

            if (inventoryTableEntry == null) continue;

            NMSTemplate? decoded;
            try
            {
                decoded = DecodeMbin(pakPath, inventoryTableEntry, header);
            }
            catch
            {
                continue; // already logged inside DecodeMbin
            }

            if (decoded is not libMBIN.NMS.GameComponents.GcInventoryTable inventoryTable ||
                inventoryTable.ShipInventoryMaxUpgradeSize == null)
                continue;

            var shipTypeNames = Enum.GetValues<libMBIN.NMS.GameComponents.GcSpaceshipClasses.ShipClassEnum>();
            var classLetters = Enum.GetValues<libMBIN.NMS.GameComponents.GcInventoryClass.InventoryClassEnum>();

            for (int t = 0; t < inventoryTable.ShipInventoryMaxUpgradeSize.Length && t < shipTypeNames.Length; t++)
            {
                var capacity = inventoryTable.ShipInventoryMaxUpgradeSize[t];
                if (capacity?.MaxInventoryCapacity == null || capacity.MaxTechInventoryCapacity == null) continue;

                string typeName = shipTypeNames[t].ToString().ToUpperInvariant();

                for (int c = 0; c < capacity.MaxInventoryCapacity.Length && c < classLetters.Length; c++)
                {
                    string classLetter = classLetters[c].ToString().ToUpperInvariant();

                    shipCapacityCategory.Items.Add(new CatalogItem
                    {
                        GameId = $"{typeName}_{classLetter}_CARGO",
                        NameEnglish = $"{typeName} {classLetter} Cargo",
                        CapacityValue = capacity.MaxInventoryCapacity[c]
                    });
                    shipCapacityCategory.Items.Add(new CatalogItem
                    {
                        GameId = $"{typeName}_{classLetter}_TECH",
                        NameEnglish = $"{typeName} {classLetter} Tech",
                        CapacityValue = capacity.MaxTechInventoryCapacity[c]
                    });
                }
            }

            if (inventoryTable.WeaponInventoryMaxUpgradeSize?.MaxInventoryCapacity != null)
            {
                var weaponCapacity = inventoryTable.WeaponInventoryMaxUpgradeSize.MaxInventoryCapacity;
                for (int c = 0; c < weaponCapacity.Length && c < classLetters.Length; c++)
                {
                    string classLetter = classLetters[c].ToString().ToUpperInvariant();

                    multiToolCapacityCategory.Items.Add(new CatalogItem
                    {
                        GameId = $"MULTITOOL_{classLetter}_TECH",
                        NameEnglish = $"Multi-Tool {classLetter} Tech",
                        CapacityValue = weaponCapacity[c]
                    });
                }
            }

            break; // found and decoded the one real table - no need to keep scanning PAKs
        }

        if (shipCapacityCategory.Items.Count > 0)
        {
            categories.Add(shipCapacityCategory);
            LogService.Write($"CatalogBuild: extracted ship capacity for {shipCapacityCategory.Items.Count / 2} ship type/class combinations.");
        }
        else
        {
            LogService.Write("CatalogBuild: WARNING - could not extract ship capacity data from inventorytable.mbin.");
        }

        if (multiToolCapacityCategory.Items.Count > 0)
        {
            categories.Add(multiToolCapacityCategory);
            LogService.Write($"CatalogBuild: extracted multi-tool capacity for {multiToolCapacityCategory.Items.Count} classes.");
        }
        else
        {
            LogService.Write("CatalogBuild: WARNING - could not extract multi-tool capacity data from inventorytable.mbin.");
        }

        // -------------------------------------------------------------
        // Phase 1.65: Creature/Pet species data - metadata/simulation/
        // ecosystem/creaturedatatable.mbin decodes to GcCreatureDataTable,
        // whose Table is a real data table (not a filename rule) keyed by
        // the same archetype Id the save's own pet entries use as XID (e.g.
        // "RODENT" - confirmed via real save data, see
        // ArtifactX.Core.NmsModels.NmsPetPaths). Found while investigating
        // whether the in-game "Battle Abilities" grades (Combat
        // Effectiveness/Agility/Health) are baked in here - they are NOT
        // (no such field exists anywhere on GcCreatureData), but the table
        // does hold real, confirmed, otherwise-undiscoverable per-species
        // data worth keeping regardless: Rarity (Common/Uncommon/Rare/
        // SuperRare - species-wide, not per-pet-instance), whether the
        // species can appear in Holo-Arena Pet Battles at all
        // (CanBeUsedInPetBattler), its native MoveArea (Ground/Water/Air/
        // Space), and its PetBattlerForcedAffinity (Normal/Lush/Cold/Fire/
        // Toxic/Barren/Radioactive/Weird/Mech - an elemental-style battle
        // typing). There's no real display-name field on this row shape
        // (the fancy in-game Latin species name is generated per-pet-
        // instance from seeds, not looked up from a static table), so
        // NameEnglish is just a Title-Cased version of Id.
        // -------------------------------------------------------------
        var creatureSpeciesCategory = new CatalogCategory
        {
            TemplateType = "CreatureSpecies",
            RowType = "GcCreatureData",
            SourceMbinPath = "metadata/simulation/ecosystem/creaturedatatable.mbin (GcCreatureDataTable.Table)"
        };

        foreach (var (pakPath, header, entries) in pakData)
        {
            var creatureDataEntry = entries.FirstOrDefault(e =>
                !string.IsNullOrEmpty(e.FileName) &&
                e.FileName.EndsWith("creaturedatatable.mbin", StringComparison.OrdinalIgnoreCase));

            if (creatureDataEntry == null) continue;

            NMSTemplate? decoded;
            try
            {
                decoded = DecodeMbin(pakPath, creatureDataEntry, header);
            }
            catch
            {
                continue; // already logged inside DecodeMbin
            }

            if (decoded is not libMBIN.NMS.GameComponents.GcCreatureDataTable creatureTable ||
                creatureTable.Table == null)
                continue;

            foreach (var row in creatureTable.Table)
            {
                string? id = row?.Id?.ToString();
                if (string.IsNullOrWhiteSpace(id)) continue;

                string rarity = row!.Rarity?.CreatureRarity.ToString() ?? "Unknown";
                string moveArea = row.MoveArea.ToString();
                string affinity = row.PetBattlerForcedAffinity?.PetBattlerAffinity.ToString() ?? "Unknown";
                string petBattler = row.CanBeUsedInPetBattler ? "Yes" : "No";

                creatureSpeciesCategory.Items.Add(new CatalogItem
                {
                    GameId = id.ToUpperInvariant(),
                    NameEnglish = char.ToUpperInvariant(id[0]) + id[1..].ToLowerInvariant(),
                    UsageCategory = rarity,
                    DescriptionEnglish = $"PetBattler: {petBattler}; MoveArea: {moveArea}; Affinity: {affinity}"
                });
            }

            break; // found and decoded the one real table - no need to keep scanning PAKs
        }

        if (creatureSpeciesCategory.Items.Count > 0)
        {
            categories.Add(creatureSpeciesCategory);
            LogService.Write($"CatalogBuild: extracted {creatureSpeciesCategory.Items.Count} creature species entries.");
        }
        else
        {
            LogService.Write("CatalogBuild: WARNING - could not extract creature species data from creaturedatatable.mbin.");
        }

        // -------------------------------------------------------------
        // Phase 1.7: Creature body-part "Descriptors" trees (osl in the save -
        // confirmed 2026-07-28 via a real pet's osl array, e.g. ["_TREX_4",
        // "_HEAD_ALIEN", "_BLOB_2B", "_EYES_1", "_ANTENNAS_1", "_BODY_BIRDREX",
        // "_BBRACC_5N", "_TAIL_RAT", "4262532434"], cross-referenced against
        // the real decoded models/planets/creatures/trexrig/trex.descriptor.
        // mbin - every named entry matched a node in this tree exactly. Not a
        // flat value list: each creature body "rig" (trex, cat, rodent,
        // spider, grunt, ...) has its own recursively-nested option tree
        // (TkModelDescriptorList -> TkResourceDescriptorList.Descriptors ->
        // TkResourceDescriptorData, whose own Children re-enter a nested
        // TkModelDescriptorList for the next branch down) - e.g. TREX's tree
        // picks a body archetype (_TREX_4 vs _TREX_3XRARE) which opens up
        // HEAD/BODY/TAIL choices, some of which open further sub-choices
        // (_HEAD_ALIEN -> BLOB -> EYES/ANTENNAS). osl is a flat array of the
        // selected node Id at each branch actually taken for that pet, not
        // every possible node. Its trailing numeric-looking entry (e.g.
        // "4262532434") doesn't match any node Id anywhere in the tree -
        // most likely a per-instance detail/variation seed, not itself a
        // selectable option.
        //
        // No CatalogItem/CatalogCategory row here - the recursive parent/
        // child shape doesn't fit that flat schema, so this gets its own
        // dedicated table (CreatureDescriptorOption, self-referencing via
        // ParentOptionId) written directly in Phase 3 below.
        //
        // Rig discovery is a FILENAME RULE, not a curated list (same idea as
        // Multi-Tool/Freighter Types above): every *.descriptor.mbin
        // directly under models/planets/creatures/, excluding any path
        // segment starting with "ANIM" (ANIM/ANIMS/ANIMATION subfolders hold
        // hundreds of unrelated per-animation-clip descriptor files,
        // confirmed by sampling - those are a completely different use of
        // the same MBIN type, not body-part trees). RigId is the filename
        // stem lowercased (e.g. "trex.descriptor.mbin" -> "trex") -
        // confirmed to exactly match a pet's own XID lowercased for the one
        // archetype tested (TREX); cross-checking the full ~85-entry
        // creaturedatatable.mbin species list against these rig filenames
        // found most match exactly but a real minority don't (SWIMCOW/
        // cowswim, TWOLEGANTELOPE/antelopetwolegs, ROBOTANTELOPE/
        // anteloperobot - word order flips), so the app-side lookup should
        // be an exact match with a graceful "no data" fallback for those,
        // not a fuzzy matcher guessing at the irregular cases.
        // -------------------------------------------------------------
        var creatureDescriptorOptions = new List<CreatureDescriptorOption>();
        var seenRigFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int rigsProcessed = 0;

        foreach (var (pakPath, header, entries) in pakData)
        {
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.FileName)) continue;

                string upperPath = entry.FileName.ToUpperInvariant();
                if (!upperPath.Contains("MODELS/PLANETS/CREATURES/")) continue;
                if (!upperPath.EndsWith(".DESCRIPTOR.MBIN")) continue;
                if (upperPath.Split('/').Any(s => s.StartsWith("ANIM", StringComparison.Ordinal))) continue;
                if (!seenRigFiles.Add(upperPath)) continue;

                NMSTemplate? decoded;
                try
                {
                    decoded = DecodeMbin(pakPath, entry, header);
                }
                catch
                {
                    continue; // already logged inside DecodeMbin
                }

                if (decoded is not libMBIN.NMS.Toolkit.TkModelDescriptorList rootList || rootList.List == null)
                    continue;

                string fileName = entry.FileName[(entry.FileName.LastIndexOf('/') + 1)..];
                int dotIdx = fileName.IndexOf(".descriptor", StringComparison.OrdinalIgnoreCase);
                string rigId = (dotIdx >= 0 ? fileName[..dotIdx] : fileName).ToLowerInvariant();

                foreach (var slot in rootList.List)
                    ExtractDescriptorSlot(slot, rigId, null, creatureDescriptorOptions);

                rigsProcessed++;
            }
        }

        if (creatureDescriptorOptions.Count > 0)
            LogService.Write($"CatalogBuild: extracted {creatureDescriptorOptions.Count} creature descriptor tree nodes across {rigsProcessed} rigs.");
        else
            LogService.Write("CatalogBuild: WARNING - found no creature descriptor.mbin body-part trees.");

        // -------------------------------------------------------------
        // Phase 1.8: Settlement Perks ("buffs" shown in-game under
        // "Settlement Features") - metadata/reality/tables/
        // settlementperkstable.mbin decodes to GcSettlementPerksTable, a
        // real data table the generic classifier sweep below ALREADY
        // discovers on its own (TemplateType "GcSettlementPerksTable"),
        // but GcSettlementPerkData's Name/Description fields aren't
        // recognized as loc-key candidates by the generic row extractor,
        // so every row comes out with NameEnglish/DescriptionEnglish both
        // null. They ARE genuine loc keys, not literal text - confirmed by
        // resolving one directly against the merged English loc lookup
        // ("UI_PERK_POSITIVE_TITLE_18" -> "Public artworks",
        // "UI_PERK_POSITIVE_DESC_MOOD" -> "Improves citizen happiness"),
        // which matched EXACTLY what a real settlement's own in-game
        // "Settlement Features" panel showed for that perk. Extracted here
        // directly instead, same idea as the Creature Species phase -
        // deliberately using a custom TemplateType ("SettlementPerks", not
        // the real "GcSettlementPerksTable") so this doesn't collide with
        // the generic sweep's own nameless copy of the same table; that
        // nameless copy is harmless dead weight in the working catalog and
        // never reaches the trimmed distribution copy (CatalogTrimService
        // only keeps items with a resolved NameEnglish).
        //
        // A perk's StatChanges (which stat it affects and how strongly -
        // e.g. Happiness/PositiveMedium) is folded into DescriptionEnglish
        // as a plain qualitative summary ("Happiness +Medium"), NOT the
        // exact numeric range a reference tool's UI shows (e.g. "-6..-1") - that
        // exact range lives in a SEPARATE global table
        // (GcSettlementGlobals.PerkStatStrengthValues, one entry per Stat,
        // each itself holding a range per Strength tier) not decoded here;
        // left as a follow-up if the qualitative summary isn't enough.
        // -------------------------------------------------------------
        var settlementPerksCategory = new CatalogCategory
        {
            TemplateType = "SettlementPerks",
            RowType = "GcSettlementPerkData",
            SourceMbinPath = "metadata/reality/tables/settlementperkstable.mbin (GcSettlementPerksTable.Table)"
        };

        foreach (var (pakPath, header, entries) in pakData)
        {
            var perksEntry = entries.FirstOrDefault(e =>
                !string.IsNullOrEmpty(e.FileName) &&
                e.FileName.EndsWith("settlementperkstable.mbin", StringComparison.OrdinalIgnoreCase));

            if (perksEntry == null) continue;

            NMSTemplate? perksDecoded;
            try
            {
                perksDecoded = DecodeMbin(pakPath, perksEntry, header);
            }
            catch
            {
                continue; // already logged inside DecodeMbin
            }

            if (perksDecoded is not libMBIN.NMS.GameComponents.GcSettlementPerksTable perksTable || perksTable.Table == null)
                continue;

            foreach (var perk in perksTable.Table)
            {
                string? id = perk?.ID?.ToString();
                if (string.IsNullOrWhiteSpace(id)) continue;

                string? nameKey = perk!.Name?.ToString();
                string? descKey = perk.Description?.ToString();
                string name = !string.IsNullOrEmpty(nameKey) && englishLookup.TryGetValue(nameKey, out var resolvedName) ? resolvedName : (nameKey ?? id);
                string description = !string.IsNullOrEmpty(descKey) && englishLookup.TryGetValue(descKey, out var resolvedDesc) ? resolvedDesc : "";

                var statSummaries = new List<string>();
                if (perk.StatChanges != null)
                {
                    foreach (var change in perk.StatChanges)
                    {
                        string statName = change?.Stat?.SettlementStatType.ToString() ?? "?";
                        string strength = change?.Strength?.SettlementStatStrength.ToString() ?? "?";
                        bool positive = strength.StartsWith("Positive", StringComparison.OrdinalIgnoreCase);
                        string magnitude = strength.Replace("Positive", "").Replace("Negative", "");
                        statSummaries.Add($"{statName} {(positive ? "+" : "-")}{magnitude}");
                    }
                }

                // IsNegative always comes first so UI code can key color-coding
                // (red/green) off a simple StartsWith check, without losing the
                // other independent flags - a perk can be e.g. both a Starter
                // AND Negative, and the original single-branch priority order
                // here silently dropped the sign whenever any of Blessing/Job/
                // Starter/Proc was also set.
                var flagTags = new List<string> { perk.IsNegative ? "Negative" : "Positive" };
                if (perk.IsBlessing) flagTags.Add("Blessing");
                if (perk.IsJob) flagTags.Add("Job");
                if (perk.IsStarter) flagTags.Add("Starter");
                if (perk.IsProc) flagTags.Add("Proc");
                string flags = string.Join(", ", flagTags);

                settlementPerksCategory.Items.Add(new CatalogItem
                {
                    GameId = id,
                    NameEnglish = name,
                    DescriptionEnglish = statSummaries.Count > 0 ? $"{description} ({string.Join(", ", statSummaries)})" : description,
                    UsageCategory = flags
                });
            }

            break; // found and decoded the one real table - no need to keep scanning PAKs
        }

        if (settlementPerksCategory.Items.Count > 0)
        {
            categories.Add(settlementPerksCategory);
            LogService.Write($"CatalogBuild: extracted {settlementPerksCategory.Items.Count} settlement perks.");
        }
        else
        {
            LogService.Write("CatalogBuild: WARNING - could not extract settlement perks from settlementperkstable.mbin.");
        }

        // -------------------------------------------------------------
        // Phase 1.9: Frigate Traits (the 5-per-frigate perks shown in-game
        // on the Frigate detail screen, e.g. "Deep Scout Prototype (+15
        // Combat)") - same "generic sweep misses/mangles this shape, add a
        // targeted phase" reasoning as Settlement Perks above. Two source
        // tables needed, both confirmed via `dumpfile`:
        //   - metadata/reality/tables/frigatetraittable.mbin decodes to
        //     GcFrigateTraitTable.Table (178 rows in the version checked) -
        //     GcFrigateTraitData.DisplayName is a loc key (e.g.
        //     "UI_NORMANDY_TRAIT1"), ID is the raw save-file id (e.g.
        //     "NORMANDY_1"), and FrigateStatType+Strength together say
        //     WHICH stat is affected and by how much - but only as an
        //     enum tier (e.g. Combat/Primary), not a real number.
        //   - metadata/reality/globals/gcfleetglobals.global.mbin (found
        //     via dumpfile as "gcfleetglobals.global.mbin") decodes to
        //     GcFleetGlobals, whose FrigateTraitStrengths field
        //     (GcFrigateTraitStrengthByType) is a fixed 11-entry array
        //     indexed by FrigateStatTypeEnum, each holding its own
        //     10-entry StatAlteration[] indexed by
        //     FrigateTraitStrengthEnum - e.g. Combat's array is
        //     [-6,-4,-2,1,2,3,2,4,6,15], so Combat+Primary (index 9) = 15.
        //     Checked all 5 traits on the same real frigate (SSV Normandy
        //     SR1) against a reference screenshot the user provided: 3 of 5
        //     matched the reference's displayed number exactly (Combat/Combat/
        //     Exploration-tagged traits). The other 2 (Stealth/Speed-tagged)
        //     resolved the right trait NAME but the reference shows special
        //     flavor text ("Silent Running Capability Enabled", "+3%
        //     Expedition Duration") instead of a plain stat delta for those
        //     two specifically - likely that reference tool hardcodes a few
        //     unique traits rather than always using the generic formula. The
        //     computed number here is still mathematically correct per the
        //     game's own StatAlteration table either way - unlike
        //     Settlement Perks, which only got a qualitative summary
        //     because its equivalent globals table wasn't decoded, this at
        //     least gives a real number, just not always matching that
        //     reference's exact wording.
        // Deliberately using a custom TemplateType ("FrigateTraits") so
        // this doesn't collide with the generic sweep's own nameless copy
        // of frigatetraittable.mbin.
        // -------------------------------------------------------------
        var frigateTraitsCategory = new CatalogCategory
        {
            TemplateType = "FrigateTraits",
            RowType = "GcFrigateTraitData",
            SourceMbinPath = "metadata/reality/tables/frigatetraittable.mbin (GcFrigateTraitTable.Table) + gcfleetglobals.global.mbin (GcFleetGlobals.FrigateTraitStrengths)"
        };

        // FrigateStatTypeEnum order (0-10) - matches libMBIN's declared enum
        // exactly, used both to index FrigateTraitStrengths.FrigateStatType[]
        // and to build a human-readable label for the formatted description.
        string[] frigateStatLabels =
        {
            "Combat", "Exploration", "Mining", "Diplomatic", "Fuel Burn Rate",
            "Fuel Capacity", "Speed", "Extra Loot", "Repair", "Invulnerable", "Stealth"
        };

        libMBIN.NMS.GameComponents.GcFrigateTraitStrengthByType? traitStrengths = null;

        foreach (var (pakPath, header, entries) in pakData)
        {
            var globalsEntry = entries.FirstOrDefault(e =>
                !string.IsNullOrEmpty(e.FileName) &&
                e.FileName.EndsWith("gcfleetglobals.global.mbin", StringComparison.OrdinalIgnoreCase));

            if (globalsEntry == null) continue;

            NMSTemplate? globalsDecoded;
            try
            {
                globalsDecoded = DecodeMbin(pakPath, globalsEntry, header);
            }
            catch
            {
                continue;
            }

            if (globalsDecoded is libMBIN.NMS.Globals.GcFleetGlobals fleetGlobals)
                traitStrengths = fleetGlobals.FrigateTraitStrengths;

            break; // found and decoded the one real globals table - no need to keep scanning PAKs
        }

        if (traitStrengths?.FrigateStatType == null)
        {
            LogService.Write("CatalogBuild: WARNING - could not extract FrigateTraitStrengths from gcfleetglobals.global.mbin; frigate traits will have no numeric value in their description.");
        }

        foreach (var (pakPath, header, entries) in pakData)
        {
            var traitsEntry = entries.FirstOrDefault(e =>
                !string.IsNullOrEmpty(e.FileName) &&
                e.FileName.EndsWith("frigatetraittable.mbin", StringComparison.OrdinalIgnoreCase));

            if (traitsEntry == null) continue;

            NMSTemplate? traitsDecoded;
            try
            {
                traitsDecoded = DecodeMbin(pakPath, traitsEntry, header);
            }
            catch
            {
                continue;
            }

            if (traitsDecoded is not libMBIN.NMS.GameComponents.GcFrigateTraitTable traitsTable || traitsTable.Traits == null)
                continue;

            foreach (var trait in traitsTable.Traits)
            {
                string? id = trait?.ID?.ToString();
                if (string.IsNullOrWhiteSpace(id)) continue;

                string? nameKey = trait!.DisplayName?.ToString();
                string name = !string.IsNullOrEmpty(nameKey) && englishLookup.TryGetValue(nameKey, out var resolvedName) ? resolvedName : (nameKey ?? id);

                int statIndex = (int)trait.FrigateStatType.FrigateStatType;
                int strengthIndex = (int)trait.Strength.FrigateTraitStrength;
                string statLabel = statIndex >= 0 && statIndex < frigateStatLabels.Length ? frigateStatLabels[statIndex] : "?";

                string description = name;
                int delta = 0;
                bool hasDelta = false;
                if (traitStrengths?.FrigateStatType != null && statIndex >= 0 && statIndex < traitStrengths.FrigateStatType.Length)
                {
                    var alterations = traitStrengths.FrigateStatType[statIndex]?.StatAlteration;
                    if (alterations != null && strengthIndex >= 0 && strengthIndex < alterations.Length)
                    {
                        delta = alterations[strengthIndex];
                        hasDelta = true;
                        description = $"{name} ({(delta >= 0 ? "+" : "")}{delta} {statLabel})";
                    }
                }

                frigateTraitsCategory.Items.Add(new CatalogItem
                {
                    GameId = id,
                    NameEnglish = name,
                    DescriptionEnglish = description,
                    UsageCategory = hasDelta ? (delta < 0 ? "Negative" : "Positive") : "Neutral"
                });
            }

            break; // found and decoded the one real table - no need to keep scanning PAKs
        }

        if (frigateTraitsCategory.Items.Count > 0)
        {
            categories.Add(frigateTraitsCategory);
            LogService.Write($"CatalogBuild: extracted {frigateTraitsCategory.Items.Count} frigate traits.");
        }
        else
        {
            LogService.Write("CatalogBuild: WARNING - could not extract frigate traits from frigatetraittable.mbin.");
        }

        foreach (var (pakPath, header, entries) in pakData)
        {
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.FileName) ||
                    !entry.FileName.EndsWith(".MBIN", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (locEntryPaths.Contains(entry.FileName))
                    continue;

                NMSTemplate? template;
                try
                {
                    template = DecodeMbin(pakPath, entry, header);
                }
                catch
                {
                    continue; // already logged inside DecodeMbin
                }

                if (template == null) continue;

                bool isClassified;
                FieldInfo listField = null!;
                Type rowType = null!;
                try
                {
                    isClassified = CatalogClassifier.TryClassify(template, out listField, out rowType);
                }
                catch (Exception ex)
                {
                    LogService.Write($"CatalogBuild: TryClassify threw for {entry.FileName}: {ex}");
                    continue;
                }

                if (!isClassified)
                    continue;

                var category = new CatalogCategory
                {
                    TemplateType = template.GetType().Name,
                    RowType = rowType.Name,
                    SourceMbinPath = entry.FileName
                };

                List<ClassifiedRow> rows;
                try
                {
                    rows = CatalogClassifier.ExtractRows(template, listField);
                }
                catch (Exception ex)
                {
                    LogService.Write($"CatalogBuild: ExtractRows threw for {entry.FileName}: {ex}");
                    continue;
                }

                foreach (var row in rows)
                {
                    if (string.IsNullOrEmpty(row.GameId)) continue;

                    var item = new CatalogItem
                    {
                        GameId = row.GameId,
                        NameLocKey = row.NameLocKey,
                        NameEnglish = row.NameLocKey != null && englishLookup.TryGetValue(row.NameLocKey, out var n) ? n : null,
                        NameLowerLocKey = row.NameLowerLocKey,
                        NameLowerEnglish = row.NameLowerLocKey != null && englishLookup.TryGetValue(row.NameLowerLocKey, out var nl) ? nl : null,
                        DescriptionLocKey = row.DescriptionLocKey,
                        DescriptionEnglish = row.DescriptionLocKey != null && englishLookup.TryGetValue(row.DescriptionLocKey, out var d) ? d : null,
                        TemplateId = row.TemplateId,
                        UsageCategory = row.UsageCategory,
                        MaxStackSize = row.MaxStackSize,
                    };

                    foreach (var (sourceField, texturePath) in row.Icons)
                    {
                        if (!iconBlobCache.TryGetValue(texturePath, out var blob))
                        {
                            byte[]? pngBytes = iconService.ExtractAndConvert(texturePath);
                            if (pngBytes == null) continue; // extraction/decode failed - skip, don't create an empty blob

                            blob = new IconBlob { SourceDdsPath = texturePath, PngData = pngBytes };
                            iconBlobCache[texturePath] = blob;
                        }

                        item.Icons.Add(new IconAsset
                        {
                            SourceField = sourceField,
                            IconBlob = blob
                        });

                        totalIcons++;
                    }

                    category.Items.Add(item);
                    totalRows++;
                }

                if (category.Items.Count > 0)
                    categories.Add(category);
            }
        }

        LogService.Write($"CatalogBuild: classified {categories.Count} tables, {totalRows} rows, {totalIcons} icon references.");

        // -------------------------------------------------------------
        // Phase 2.5: procedural upgrade modules (GcProceduralTechnologyTable -
        // Scanner/Mining/Hazard Sigma/Tau/Theta upgrades etc.) have no icon of
        // their own anywhere in that table - confirmed by decoding the real
        // table directly, every row's only icon-shaped field is simply absent.
        // Their raw "Template" value (e.g. "T_SCAN") identifies which real
        // GcTechnologyTable row is the actual base technology to borrow the
        // icon (and name, for consistency) from: strip the "T_" prefix, then
        // the SHORTEST GcTechnologyTable GameId starting with that stem is the
        // right one. Confirmed for two families by extracting and visually
        // comparing the actual icon PNGs - "T_SCAN" correctly resolves to
        // SCAN1 ("Scanner"), NOT the longer SCANBINOC1 ("Analysis Visor",
        // a real item but visually a goggles/visor shape, not what the
        // procedural Scan-family upgrade module actually looks like in-game).
        // Deliberately NOT keyed off the row's own "Group" field, which for
        // this same family pointed at that wrong Analysis Visor item - Group
        // and Template can reference two different, only loosely related
        // base techs, and only Template's link was confirmed correct here.
        // -------------------------------------------------------------
        var baseTechByGameId = categories
            .Where(c => c.TemplateType == "GcTechnologyTable")
            .SelectMany(c => c.Items)
            .Where(i => i.Icons.Count > 0)
            .OrderBy(i => i.GameId.Length)
            .ToList();

        // Exocraft/Mech's procedural upgrade families (Template "T_EXENG"/
        // "T_MCENG"/etc) DON'T follow the stem-prefix convention below at all -
        // confirmed 2026-08-06 while chasing missing Exocraft page icons: e.g.
        // Template "T_MCENG" strips to stem "MCENG", but the real base tech's
        // GameId is "MECH_ENGINE" (doesn't start with "MCENG"), and Template
        // "T_SUB" strips to stem "SUB", which WOULD wrongly match "SUB_ENGINE"
        // (Humboldt Drive) even though every UP_EXSUB row's own name resolves
        // to "Nautilon Cannon" - the real match is "SUB_GUN". Each mapping
        // below confirmed by exact NameEnglish/NameLowerEnglish match between
        // the procedural row and the base tech row (not guessed), same
        // standard as the T_SCAN/T_LASER two-family case that originally
        // shipped with this method. Checked first, before the generic
        // stem-prefix fallback, so it doesn't disturb any family (like
        // T_SCAN/T_LASER) the fallback already gets right.
        var templateOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["T_MCENG"] = "MECH_ENGINE",   // Daedalus Engine
            ["T_MCGUN"] = "MECH_GUN",      // Minotaur Cannon
            ["T_MCLAS"] = "MECH_LASER",    // Minotaur Laser
            ["T_EXENG"] = "VEHICLE_ENGINE",// Fusion Engine
            ["T_EXGUN"] = "VEHICLE_GUN",   // Mounted Cannon
            ["T_EXLAS"] = "VEHICLE_LASER", // Exocraft Mining Laser
            ["T_SUB"] = "SUB_GUN",         // Nautilon Cannon
        };

        int borrowedIcons = 0;
        foreach (var category in categories)
        {
            if (category.TemplateType != "GcProceduralTechnologyTable") continue;

            foreach (var item in category.Items)
            {
                if (item.Icons.Count > 0 || string.IsNullOrEmpty(item.TemplateId)) continue;

                CatalogItem? baseItem = null;

                if (templateOverrides.TryGetValue(item.TemplateId, out var overrideGameId))
                {
                    baseItem = baseTechByGameId.FirstOrDefault(b =>
                        string.Equals(b.GameId, overrideGameId, StringComparison.OrdinalIgnoreCase));
                }

                if (baseItem is null)
                {
                    string stem = item.TemplateId.StartsWith("T_", StringComparison.OrdinalIgnoreCase)
                        ? item.TemplateId[2..]
                        : item.TemplateId;

                    baseItem = baseTechByGameId.FirstOrDefault(b =>
                        b.GameId.StartsWith(stem, StringComparison.OrdinalIgnoreCase));
                }

                if (baseItem is null) continue;

                foreach (var baseIcon in baseItem.Icons)
                    item.Icons.Add(new IconAsset { SourceField = $"Template->{baseIcon.SourceField}", IconBlob = baseIcon.IconBlob });

                // Overwrites (not just a null fallback) - the base tech found via
                // Template is the confirmed-correct match, taking priority over
                // whatever this row's own Name/NameLower already resolved to.
                item.NameEnglish = baseItem.NameEnglish;
                item.NameLowerEnglish = baseItem.NameLowerEnglish;
                borrowedIcons += baseItem.Icons.Count;
            }
        }

        LogService.Write($"CatalogBuild: borrowed base-technology icons for {borrowedIcons} procedural upgrade module references.");

        // -------------------------------------------------------------
        // Phase 3: write everything to a fresh SQLite database.
        // -------------------------------------------------------------
        if (File.Exists(dbOutputPath))
            File.Delete(dbOutputPath);

        using var db = new CatalogDbContext(dbOutputPath);
        db.Database.EnsureCreated();
        db.ChangeTracker.AutoDetectChangesEnabled = false;

        foreach (var kvp in englishLookup)
            db.LocalizedTexts.Add(new LocalizedText { LocKey = kvp.Key, Language = "en", Text = kvp.Value });

        try
        {
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            LogService.Write($"CatalogBuild: failed to save localisation texts: {ex.Message}");
        }

        db.CreatureDescriptorOptions.AddRange(creatureDescriptorOptions);

        try
        {
            db.SaveChanges();
            LogService.Write($"CatalogBuild: wrote {creatureDescriptorOptions.Count} creature descriptor tree nodes to {dbOutputPath}.");
        }
        catch (Exception ex)
        {
            LogService.Write($"CatalogBuild: failed to save creature descriptor options: {ex.Message}");
        }

        int savedCategories = 0, savedItems = 0;

        foreach (var category in categories)
        {
            db.Categories.Add(category);

            try
            {
                db.SaveChanges();
                savedCategories++;
                savedItems += category.Items.Count;
            }
            catch (Exception ex)
            {
                LogService.Write($"CatalogBuild: failed to save category {category.SourceMbinPath}: {ex.Message}");
                DetachCategoryGraph(db, category);
            }
        }

        LogService.Write($"CatalogBuild: wrote {savedCategories}/{categories.Count} categories ({savedItems} items) to {dbOutputPath}.");
    }

    /// <summary>
    /// Detaches just the failed category's own entities (category/items/icon-assets) so the
    /// next SaveChanges isn't poisoned by it - but deliberately leaves each IconAsset's
    /// IconBlob tracked, since that blob may be shared with other categories that already
    /// saved successfully. A full ChangeTracker.Clear() here would silently un-track those
    /// too, causing duplicate-insert failures the next time a later category reuses them.
    /// </summary>
    private static void DetachCategoryGraph(CatalogDbContext db, CatalogCategory category)
    {
        db.Entry(category).State = EntityState.Detached;

        foreach (var item in category.Items)
        {
            db.Entry(item).State = EntityState.Detached;

            foreach (var icon in item.Icons)
                db.Entry(icon).State = EntityState.Detached;
        }
    }

    /// <summary>Turns a model path like ".../STAFFMULTITOOLATLAS.SCENE.MBIN" into
    /// "Staff Atlas": strips the folder and extension, replaces "MULTITOOL" with a
    /// space (wherever it falls in the name, not just as a suffix) rather than just
    /// deleting it, so concatenated words split apart correctly, then title-cases
    /// the result. The base "MULTITOOL.SCENE.MBIN" file itself has nothing left
    /// after stripping - that's the default Rifle model, special-cased below.</summary>
    private static string DeriveMultiToolTypeName(string upperPath)
    {
        string fileName = upperPath[(upperPath.LastIndexOf('/') + 1)..];
        int dot = fileName.IndexOf(".SCENE", StringComparison.Ordinal);
        if (dot >= 0) fileName = fileName[..dot];

        string name = System.Text.RegularExpressions.Regex
            .Replace(fileName, "MULTITOOL", " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            .Trim();

        if (string.IsNullOrEmpty(name)) name = "RIFLE";

        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.ToLowerInvariant());
    }

    /// <summary>Turns a model path like ".../PIRATEFREIGHTER.SCENE.MBIN" into
    /// a display name. The three model paths actually confirmed in real player
    /// saves get their real in-game names (cross-checked against a reference
    /// save editor, showing exactly "Normal"/"Capital"/
    /// "Dreadnought" as freighter Type options); anything else discovered by
    /// the filename rule falls back to a generic derivation (strip folder/
    /// extension/trailing "_PROC", split "FREIGHTER" out as its own word,
    /// title-case) since it hasn't been individually confirmed.</summary>
    private static string DeriveFreighterTypeName(string upperPath)
    {
        string fileName = upperPath[(upperPath.LastIndexOf('/') + 1)..];
        int dot = fileName.IndexOf(".SCENE", StringComparison.Ordinal);
        if (dot >= 0) fileName = fileName[..dot];

        switch (fileName.ToUpperInvariant())
        {
            case "FREIGHTER_PROC": return "Normal";
            case "CAPITALFREIGHTER_PROC": return "Capital";
            case "PIRATEFREIGHTER": return "Dreadnought";
        }

        fileName = System.Text.RegularExpressions.Regex.Replace(
            fileName, "_PROC$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        fileName = fileName.Replace('_', ' ');
        fileName = System.Text.RegularExpressions.Regex.Replace(
            fileName, "FREIGHTER", " FREIGHTER ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        fileName = System.Text.RegularExpressions.Regex.Replace(fileName, @"\s+", " ").Trim();

        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(fileName.ToLowerInvariant());
    }

    /// <summary>Turns a crew captain model path like ".../NPCVYKEEN.SCENE.MBIN"
    /// into "Vy'keen". Only the four real playable-race captain models get a
    /// name here; FreighterCrewRaces discovery only includes those four in the
    /// first place (see that block's own exclusion list for why the other NPC
    /// models found under player/playercharacter/ - Nada, Polo, story-unique
    /// characters, vendor/settler NPCs - aren't real crew race options).</summary>
    private static string DeriveCrewRaceName(string upperPath)
    {
        string fileName = upperPath[(upperPath.LastIndexOf('/') + 1)..];

        if (fileName.Contains("NPCGEK", StringComparison.OrdinalIgnoreCase)) return "Gek";
        if (fileName.Contains("NPCKORVAX", StringComparison.OrdinalIgnoreCase)) return "Korvax";
        if (fileName.Contains("NPCVYKEEN", StringComparison.OrdinalIgnoreCase)) return "Vy'keen";
        if (fileName.Contains("NPCROBOT", StringComparison.OrdinalIgnoreCase)) return "Robot";

        return fileName;
    }

    /// <summary>Recursively flattens one TkResourceDescriptorList "slot" (a
    /// category of mutually-exclusive options, e.g. all HEAD choices) into
    /// CreatureDescriptorOption rows, following each option's own Children
    /// back down into whatever further slots it opens up (e.g. _HEAD_ALIEN
    /// opening a BLOB slot). See Phase 1.7 above for the full picture.</summary>
    private static void ExtractDescriptorSlot(
        libMBIN.NMS.Toolkit.TkResourceDescriptorList? slot,
        string rigId,
        CreatureDescriptorOption? parentOption,
        List<CreatureDescriptorOption> collected)
    {
        if (slot?.Descriptors == null) return;

        string category = slot.TypeId?.ToString() ?? "";

        foreach (var data in slot.Descriptors)
        {
            string? optionId = data?.Id?.ToString();
            if (string.IsNullOrWhiteSpace(optionId)) continue;

            var option = new CreatureDescriptorOption
            {
                RigId = rigId,
                Category = category,
                OptionId = optionId,
                Name = data!.Name?.ToString() ?? "",
                Chance = data.Chance,
                ParentOption = parentOption
            };
            collected.Add(option);

            if (data.Children == null) continue;

            foreach (var child in data.Children)
            {
                if (child is libMBIN.NMS.Toolkit.TkModelDescriptorList childList && childList.List != null)
                {
                    foreach (var childSlot in childList.List)
                        ExtractDescriptorSlot(childSlot, rigId, option, collected);
                }
            }
        }
    }

    private NMSTemplate? DecodeMbin(string pakPath, PakEntry entry, PakHeader header)
    {
        byte[]? bytes = _extraction.ExtractEntryBytes(pakPath, entry, header);
        if (bytes == null || bytes.Length == 0)
            return null;

        try
        {
            using var ms = new MemoryStream(bytes);
            var mbin = new MBINFile(ms);
            mbin.Load();
            return mbin.GetData();
        }
        catch (Exception ex)
        {
            LogService.Write($"CatalogBuild: failed to decode {entry.FileName}: {ex.Message}");
            return null;
        }
    }
}