using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>"DataCataloger add-broken-tech-flag &lt;target-catalog-db-path&gt;" marks every GcTechnology row whose game-data "BrokenSlotTech" flag is set (the damaged- slot placeholders - Hull Fracture, Short Circuit, etc. - drawn red in-game) in an EXISTING catalog DB, adding the Items.IsBrokenSlotTech column first if the DB predates it. Same "patch the shipped DB in place" pattern as add-language-words/add-guide-topics, so their already-merged rows survive without a full rebuild; fresh full builds get the flag via CatalogClassifier directly. Idempotent - safe to re-run after a game update.  Found 2026-10-10 investigating a user save whose Install Technology menu was cluttered with useless red entries: a "Mark All Known" bulk edit had taught it all 36 BrokenSlotTech rows, which a healthy save never knows.</summary>
    public static void AddBrokenTechFlag(string catalogPath, string pcbanksPath)
    {
        string brokenDbPath = catalogPath;
        if (!File.Exists(brokenDbPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {brokenDbPath}");
            return;
        }

        var techTable = DecodeFirstMatch<libMBIN.NMS.GameComponents.GcTechnologyTable>(
            pcbanksPath,
            f => f.Contains("nms_reality_gctechnologytable.mbin", StringComparison.OrdinalIgnoreCase));

        if (techTable?.Table is null)
        {
            ConsoleStyle.Error("Could not find/decode the technology table (nms_reality_gctechnologytable.mbin) in any PAK.");
            return;
        }

        var brokenIds = techTable.Table
            .Where(t => t.BrokenSlotTech && !string.IsNullOrEmpty(t.ID?.Value))
            .Select(t => t.ID.Value)
            .ToList();
        ConsoleStyle.Header($"Found {brokenIds.Count} BrokenSlotTech technologies in game data.");

        using var brokenConnection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={brokenDbPath}");
        brokenConnection.Open();

        using (var check = brokenConnection.CreateCommand())
        {
            check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Items') WHERE name = 'IsBrokenSlotTech'";
            if (Convert.ToInt64(check.ExecuteScalar()) == 0)
            {
                using var alter = brokenConnection.CreateCommand();
                alter.CommandText = "ALTER TABLE Items ADD COLUMN IsBrokenSlotTech INTEGER NOT NULL DEFAULT 0";
                alter.ExecuteNonQuery();
                ConsoleStyle.Info("Added Items.IsBrokenSlotTech column.");
            }
        }

        using var brokenTx = brokenConnection.BeginTransaction();
        using (var reset = brokenConnection.CreateCommand())
        {
            reset.Transaction = brokenTx;
            reset.CommandText = "UPDATE Items SET IsBrokenSlotTech = 0";
            reset.ExecuteNonQuery();
        }

        int marked = 0;
        using (var update = brokenConnection.CreateCommand())
        {
            update.Transaction = brokenTx;
            update.CommandText = @"
                UPDATE Items SET IsBrokenSlotTech = 1
                WHERE GameId = $id
                  AND CategoryId IN (SELECT Id FROM Categories WHERE TemplateType = 'GcTechnologyTable')";
            var idParam = update.Parameters.Add("$id", Microsoft.Data.Sqlite.SqliteType.Text);
            foreach (var id in brokenIds)
            {
                idParam.Value = id;
                marked += update.ExecuteNonQuery();
            }
        }
        brokenTx.Commit();

        ConsoleStyle.Success($"Marked {marked} catalog rows as BrokenSlotTech in {brokenDbPath}.");
    }

    /// <summary>"DataCataloger add-station-assets &lt;target-catalog-db-path&gt;" adds the portal glyph images the app's Space Stations page uses: - "PortalGlyphs": 16 items PORTALSYMBOL_0..PORTALSYMBOL_F, each with the game's own glyph texture (textures/ui/frontend/icons/update3/ portalsymbol.&lt;hex&gt;.dds) as its icon - a portal address is 12 hex digits, one glyph per digit. Galaxy names are NOT here: the game's text only names galaxies 1-10, so the app uses ArtifactX.Core's hand-recorded NmsGalaxyNames (all 256) instead. Re-running also removes the "GalaxyNames" category an earlier version wrote. Same patch-in-place pattern as add-guide-topics; idempotent.</summary>
    public static void AddStationAssets(string catalogPath, string pcbanksPath)
    {
        string assetsDbPath = catalogPath;
        if (!File.Exists(assetsDbPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {assetsDbPath}");
            return;
        }

        var assetsIndex = GetIndex(pcbanksPath);

        // Glyphs keep the game's full texture size (the extractor never upscales) so the
        // app's enlarged glyph view stays sharp; only 16 images, so size is no concern.
        var iconService = new IconExtractionService(SharedExtraction, assetsIndex, targetSize: 1024);
        var glyphs = new List<(string GameId, string Name, string TexturePath, byte[] Png)>();
        for (int digit = 0; digit < 16; digit++)
        {
            string hex = digit.ToString("x");
            string texturePath = $"textures/ui/frontend/icons/update3/portalsymbol.{hex}.dds";
            byte[]? png = iconService.ExtractAndConvert(texturePath);
            if (png is null)
            {
                ConsoleStyle.Error($"Could not extract {texturePath} - aborting so the DB isn't left half-updated.");
                return;
            }
            glyphs.Add(($"PORTALSYMBOL_{hex.ToUpperInvariant()}", $"Glyph {digit + 1}", texturePath, png));
        }

        ConsoleStyle.Header($"Extracted {glyphs.Count} portal glyphs.");

        using var assetsDb = new CatalogDbContext(assetsDbPath);

        foreach (string templateType in new[] { "PortalGlyphs", "GalaxyNames" })
        {
            var existing = assetsDb.Categories.FirstOrDefault(c => c.TemplateType == templateType);
            if (existing is null) continue;

            var itemIds = assetsDb.Items.Where(i => i.CategoryId == existing.Id).Select(i => i.Id).ToList();
            var oldIcons = assetsDb.Set<IconAsset>().Where(a => itemIds.Contains(a.ItemId)).ToList();
            var oldBlobIds = oldIcons.Select(a => a.IconBlobId).ToList();
            assetsDb.RemoveRange(oldIcons);
            assetsDb.RemoveRange(assetsDb.Set<IconBlob>().Where(b => oldBlobIds.Contains(b.Id)));
            assetsDb.Items.RemoveRange(assetsDb.Items.Where(i => i.CategoryId == existing.Id));
            assetsDb.Categories.Remove(existing);
            assetsDb.SaveChanges();
        }

        var glyphCategory = new CatalogCategory
        {
            TemplateType = "PortalGlyphs",
            RowType = "PortalGlyph",
            SourceMbinPath = "textures/ui/frontend/icons/update3/portalsymbol.*.dds"
        };
        foreach (var glyph in glyphs)
        {
            glyphCategory.Items.Add(new CatalogItem
            {
                GameId = glyph.GameId,
                NameEnglish = glyph.Name,
                Icons =
                {
                    new IconAsset
                    {
                        SourceField = "Icon",
                        IconBlob = new IconBlob { SourceDdsPath = glyph.TexturePath, PngData = glyph.Png }
                    }
                }
            });
        }

        assetsDb.Categories.Add(glyphCategory);
        assetsDb.SaveChanges();

        ConsoleStyle.Success($"Wrote {glyphs.Count} glyphs to {assetsDbPath}.");
    }

    /// <summary>"DataCataloger add-freighter-paints &lt;target-catalog-db-path&gt;" adds the freighter Recolouring blueprints (Freighter Upgrades page) from the game's own metadata/gamestate/playerdata/customisationcolourpalettes.mbin: the palette with ID "FREIGHTER" lists, per colour, ExtraData.ProductToUnlock (the blueprint id stored in the save) and TipText (its loc name). Entries 0-9 are the 10 base colours; 10-19 are dark variants unlocked by the SAME 10 products, so only the first occurrence of each product is kept. Written as category "FreighterPaints": GameId = product id, NameEnglish = resolved TipText, DescriptionEnglish = the colour as "#RRGGBB" (drawn as the tile's swatch - these blueprints have no icon of their own). Added 2026-10-10 replacing a hand-typed Almanac list that had the cyan paint as FREIGHT_TURQUOISE; the real id (confirmed in a user save) is FREIGHT_CYAN. Same patch-in-place pattern as add-guide-topics; idempotent.</summary>
    public static void AddFreighterPaints(string catalogPath, string pcbanksPath)
    {
        string paintDbPath = catalogPath;
        if (!File.Exists(paintDbPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {paintDbPath}");
            return;
        }

        var palettes = DecodeFirstMatch<libMBIN.NMS.GameComponents.GcCustomisationColourPalettes>(
            pcbanksPath,
            f => f.Contains("customisationcolourpalettes.mbin", StringComparison.OrdinalIgnoreCase));
        var paintLookup = GetMergedEnglishLookup(pcbanksPath);

        var freighterPalette = palettes?.Palettes?.FirstOrDefault(p => p.ID?.Value == "FREIGHTER");
        if (freighterPalette?.ExtraData?.ProductToUnlock is null || freighterPalette.PaletteData?.Colours is null)
        {
            ConsoleStyle.Error("Could not find the FREIGHTER palette in customisationcolourpalettes.mbin.");
            return;
        }

        var products = freighterPalette.ExtraData.ProductToUnlock;
        var tips = freighterPalette.ExtraData.TipText;
        var colours = freighterPalette.PaletteData.Colours;
        var paints = new List<(string GameId, string Name, string Hex)>();
        var seenPaints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < products.Count && i < colours.Length; i++)
        {
            string id = products[i]?.Value ?? "";
            if (id.Length == 0 || !seenPaints.Add(id)) continue;

            string tipId = tips is not null && i < tips.Count ? tips[i]?.Value ?? "" : "";
            string name = paintLookup.TryGetValue(tipId, out var resolved) ? resolved : id;
            var c = colours[i];
            static int Channel(float v) => (int)Math.Round(Math.Clamp(v, 0f, 1f) * 255);
            paints.Add((id, name, $"#{Channel(c.R):X2}{Channel(c.G):X2}{Channel(c.B):X2}"));
        }

        ConsoleStyle.Header($"Found {paints.Count} freighter paints:");
        foreach (var paint in paints)
            ConsoleStyle.Success($"  {paint.GameId} = {paint.Name} {paint.Hex}");

        using var paintDb = new CatalogDbContext(paintDbPath);
        var rows = paints.Select(paint => new CatalogItem
        {
            GameId = paint.GameId,
            NameEnglish = paint.Name,
            DescriptionEnglish = paint.Hex,
            UsageCategory = "Recolouring"
        }).ToList();
        Replace(paintDb, "FreighterPaints", "GcCustomisationColourPalette", "metadata/gamestate/playerdata/customisationcolourpalettes.mbin", rows);

        ConsoleStyle.Success($"Wrote {paints.Count} freighter paint entries to {paintDbPath}.");
    }

    /// <summary>"DataCataloger add-corvette-parts &lt;target-catalog-db-path&gt;" marks which base-building parts can be built on a corvette, from the game's own base building table (GcBaseBuildingTable.Objects): BuildableInShipStructural (cockpits, habitats, landing gear, wings...) and BuildableInShipDecorative. The catalog's own BuildingPart items mix every base part (2,090) with no way to tell corvette ones apart, so the Corvette Cache's item list needs this (added 2026-10-12). Written as category "CorvetteParts": GameId = the inventory item id (OverrideProductID when the part uses one), NameEnglish = the part id, UsageCategory = Structural / Decorative / Both. The app joins it back to the product rows for names, icons and stack sizes. Same patch-in-place pattern as add-freighter-paints; idempotent.</summary>
    public static void AddCorvetteParts(string catalogPath, string pcbanksPath)
    {
        string corvetteDbPath = catalogPath;
        if (!File.Exists(corvetteDbPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {corvetteDbPath}");
            return;
        }

        var buildingTable = DecodeFirstMatch<libMBIN.NMS.GameComponents.GcBaseBuildingTable>(
            pcbanksPath,
            f => f.Contains("basebuildingobjectstable.mbin", StringComparison.OrdinalIgnoreCase));

        if (buildingTable?.Objects is null)
        {
            ConsoleStyle.Error("Could not find GcBaseBuildingTable (basebuildingobjectstable.mbin).");
            return;
        }

        var corvetteSources = CorvettePartSources(pcbanksPath);
        var corvetteParts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // The part's build-menu facts, kept as JSON in DescriptionEnglish - many
        // parts are rotations/mirrors of one buildable item (B_STR_K_NE, _NE1..3,
        // _Y_NE...), so the app needs these to tell the item from its variants.
        var corvetteFacts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in buildingTable.Objects)
        {
            if (!part.BuildableInShipStructural && !part.BuildableInShipDecorative) continue;
            string partId = part.ID?.Value ?? "";
            string itemId = part.UseProductIDOverride && part.OverrideProductID?.Value is { Length: > 0 } overrideId ? overrideId : partId;
            if (itemId.Length == 0) continue;
            string kind = part.BuildableInShipStructural && part.BuildableInShipDecorative ? "Both"
                : part.BuildableInShipStructural ? "Structural" : "Decorative";
            if (corvetteParts.TryAdd(itemId, kind))
                corvetteFacts[itemId] = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["menu"] = part.ShowInBuildMenu,
                    ["placeable"] = part.IsPlaceable,
                    ["single"] = part.SinglePartID?.Value ?? "",
                    ["modBase"] = part.ModularCustomisationBaseID?.Value ?? "",
                    ["isMod"] = part.IsModularCustomisation,
                    ["families"] = part.FamilyIDs?.Select(f => f?.Value ?? "").ToList() ?? new List<string>(),
                    ["composite"] = part.CompositePartObjectIDs?.Select(f => f?.Value ?? "").ToList() ?? new List<string>(),
                    // Format 72 (CorvettePartSources): "basic", "barter", "reward:RS_..." or "".
                    ["shop"] = corvetteSources.GetValueOrDefault(itemId).Shop ?? "",
                    ["type"] = corvetteSources.GetValueOrDefault(itemId).Type ?? "",
                });
        }
        ConsoleStyle.Header($"Found {corvetteParts.Count} corvette parts " +
            $"({corvetteParts.Values.Count(k => k != "Decorative")} structural, {corvetteParts.Values.Count(k => k != "Structural")} decorative).");

        using var corvetteDb = new CatalogDbContext(corvetteDbPath);
        var rows = corvetteParts.Select(kv => new CatalogItem
        {
            GameId = "CORVETTE:" + kv.Key,
            NameEnglish = kv.Key,
            UsageCategory = kv.Value,
            DescriptionEnglish = corvetteFacts.GetValueOrDefault(kv.Key)
        }).ToList();
        Replace(corvetteDb, "CorvetteParts", "GcBaseBuildingEntry", "metadata/reality/tables/basebuildingobjectstable.mbin", rows);

        ConsoleStyle.Success($"Wrote {corvetteParts.Count} corvette parts to {corvetteDbPath}.");
    }

    /// <summary>"DataCataloger add-tech-stats &lt;target-catalog-db-path&gt;" records the main stat each technology works on (GcTechnology.BaseStat - Ship_Hyperdrive, Ship_PulseDrive, Ship_Weapons_Lasers...), so ArtifactX can keep a device's related tech together (user request 2026-10-13: "all the drives should be together"). The game's own Group/ParentTechId fields are empty for ship tech; BaseStat groups them cleanly. Upgrade modules (procedural tech) get the stat of the template tech they're rolled from. Written as category "TechStats": GameId = "TECHSTAT:" + tech id, NameEnglish = the stat, UsageCategory = "Primary" for a device's main item (PrimaryItem), else "", DescriptionEnglish = its LinkColour (#RRGGBB, the adjacency border). Same patch-in-place pattern as add-corvette-parts; idempotent.</summary>
    public static void AddTechStats(string catalogPath, string pcbanksPath)
    {
        string statsDbPath = catalogPath;
        if (!File.Exists(statsDbPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {statsDbPath}");
            return;
        }

        var techTable = DecodeFirstMatch<libMBIN.NMS.GameComponents.GcTechnologyTable>(
            pcbanksPath,
            f => f.EndsWith("nms_reality_gctechnologytable.mbin", StringComparison.OrdinalIgnoreCase));
        var procTable = DecodeFirstMatch<libMBIN.NMS.GameComponents.GcProceduralTechnologyTable>(
            pcbanksPath,
            f => f.EndsWith("nms_reality_gcproceduraltechnologytable.mbin", StringComparison.OrdinalIgnoreCase));

        if (techTable?.Table is null || procTable?.Table is null)
        {
            ConsoleStyle.Error("Could not find the technology tables (nms_reality_gctechnologytable / gcproceduraltechnologytable).");
            return;
        }

        // LinkColour is the border the game draws round same-type tech sitting
        // side by side (the adjacency boost) - kept as #RRGGBB in DescriptionEnglish.
        static string Hex(libMBIN.NMS.Colour? c) => c is null ? "" :
            $"#{(int)Math.Round(Math.Clamp(c.R, 0, 1) * 255):X2}{(int)Math.Round(Math.Clamp(c.G, 0, 1) * 255):X2}{(int)Math.Round(Math.Clamp(c.B, 0, 1) * 255):X2}";
        var techStats = new Dictionary<string, (string Stat, bool Primary, string Link)>(StringComparer.OrdinalIgnoreCase);
        foreach (var tech in techTable.Table)
        {
            string id = tech.ID?.Value ?? "";
            if (id.Length > 0)
                techStats.TryAdd(id, (tech.BaseStat?.StatsType.ToString() ?? "Unspecified", tech.PrimaryItem, Hex(tech.LinkColour)));
        }
        int procCount = 0;
        // Upgrade module -> the template tech it's rolled from (T_FR_HYP...). The
        // game counts an installed template placeholder towards its family's 3
        // (a freighter with T_FR_HYP + 3 hyperdrive modules showed "Technology
        // overloaded", 2026-10-13), so the app needs the link.
        var moduleTemplate = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var proc in procTable.Table)
        {
            string id = proc.ID?.Value ?? "";
            string template = proc.Template?.Value ?? "";
            if (id.Length == 0 || !techStats.TryGetValue(template, out var fromTemplate)) continue;
            if (techStats.TryAdd(id, (fromTemplate.Stat, false, fromTemplate.Link))) procCount++;
            moduleTemplate[id] = template;
        }
        ConsoleStyle.Header($"Found stats for {techStats.Count - procCount} technologies and {procCount} upgrade modules.");

        // The game colours the border by the tech's TYPE, not its own LinkColour:
        // a living ship's Scream Suppressor (link #1A2733, the unset default) shows
        // the pale gold of the regular Deflector Shield, its Spewing Vents (link
        // green) the Photon Cannon's cyan (user's in-game screenshots, 2026-10-13).
        // So each stat takes the most common real link colour among the main
        // (PrimaryItem) tech of that stat; a tech keeps its own only when its stat
        // has none.
        var statColour = techStats.Values
            .Where(t => t.Primary && t.Link is { Length: > 0 } && t.Link != "#1A2733" && t.Link != "#FFFFFF")
            .GroupBy(t => t.Stat)
            .ToDictionary(g => g.Key, g => g.GroupBy(t => t.Link).OrderByDescending(l => l.Count()).First().Key);
        foreach (var id in techStats.Keys.ToList())
        {
            var t = techStats[id];
            if (statColour.TryGetValue(t.Stat, out var colour)) techStats[id] = (t.Stat, t.Primary, colour);
        }

        using var statsDb = new CatalogDbContext(statsDbPath);
        var rows = techStats.Select(kv => new CatalogItem
        {
            GameId = "TECHSTAT:" + kv.Key,
            NameEnglish = kv.Value.Stat,
            DescriptionEnglish = kv.Value.Link,
            UsageCategory = kv.Value.Primary ? "Primary" : moduleTemplate.TryGetValue(kv.Key, out var tpl) ? "Module:" + tpl : ""
        }).ToList();
        Replace(statsDb, "TechStats", "GcTechnology", "metadata/reality/tables/nms_reality_gctechnologytable.mbin", rows);

        ConsoleStyle.Success($"Wrote {techStats.Count} tech stats to {statsDbPath}.");
    }
}
