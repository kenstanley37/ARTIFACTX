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
        // matching NomNom's "Crew Seed" field for the same freighter.
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

        int borrowedIcons = 0;
        foreach (var category in categories)
        {
            if (category.TemplateType != "GcProceduralTechnologyTable") continue;

            foreach (var item in category.Items)
            {
                if (item.Icons.Count > 0 || string.IsNullOrEmpty(item.TemplateId)) continue;

                string stem = item.TemplateId.StartsWith("T_", StringComparison.OrdinalIgnoreCase)
                    ? item.TemplateId[2..]
                    : item.TemplateId;

                var baseItem = baseTechByGameId.FirstOrDefault(b =>
                    b.GameId.StartsWith(stem, StringComparison.OrdinalIgnoreCase));
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
    /// saves get their real in-game names (cross-checked against NomNom, an
    /// established NMS save editor, showing exactly "Normal"/"Capital"/
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