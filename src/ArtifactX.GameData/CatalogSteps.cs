using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;
using ArtifactX.GameData.Services.Interfaces;
using System.Reflection;
using System.Text.RegularExpressions;
using libMBIN;

namespace ArtifactX.GameData;

/// <summary>The catalog's extra build steps (run after CatalogBuildService's main
/// build, each adding one kind of data). Moved from DataCataloger's command
/// line (2026-10-14) so the app can run them on the player's PC.</summary>
public static partial class CatalogSteps
{
    /// <summary>"DataCataloger add-language-words &lt;target-catalog-db-path&gt;" extracts every alien-language vocabulary word (the save-editable subset found at vLc.6f=.MF2 - see ArtifactX's project_language_words memory) from language/nms_loc1_english.mbin and writes them into an existing catalog DB as a new "GcAlienLanguageWords" category, UsageCategory = race name.  The loc table's TRA_/WAR_/EXP_/BUI_ prefixes aren't exclusively vocabulary words - the SAME prefixes are reused for lore paragraphs and other UI text (e.g. "EXP_1_PLAQUE_LORE_1" is a multi-sentence Korvax lore entry, not a word). Confirmed by direct inspection that every REAL word id follows a strict two-part shape - PREFIX_WORD, where WORD is letters/apostrophe only, no digits, no extra underscores - and that shape alone (not the text value) cleanly separates real words from lore noise with zero false positives spot-checked. Idempotent: re-running replaces any previously-inserted GcAlienLanguageWords category rather than duplicating them.</summary>
    public static void AddLanguageWords(string catalogPath, string pcbanksPath)
    {
        string targetDbPath = catalogPath;
        if (!File.Exists(targetDbPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {targetDbPath}");
            return;
        }


        IPakDiscoveryService wordPakDiscovery = new PakDiscoveryService();
        IPakReaderService wordPakReader = new PakReaderService();
        var wordExtraction = new ExtractionService();
        var wordPaks = wordPakDiscovery.Discover(pcbanksPath);

        NMSTemplate? locTemplate = null;
        foreach (var pak in wordPaks)
        {
            PakHeader header;
            IReadOnlyList<PakEntry> entries;
            try
            {
                header = wordPakReader.ReadHeader(pak.FullPath);
                entries = wordPakReader.Read(pak.FullPath);
            }
            catch { continue; }

            var match = entries.FirstOrDefault(e =>
                !string.IsNullOrEmpty(e.FileName) &&
                e.FileName.Contains("language/nms_loc1_english.mbin", StringComparison.OrdinalIgnoreCase));
            if (match is null) continue;

            byte[]? bytes;
            try { bytes = wordExtraction.ExtractEntryBytes(pak.FullPath, match, header); }
            catch (Exception ex) { ConsoleStyle.Error($"Extraction failed: {ex.Message}"); return; }
            if (bytes is null || bytes.Length == 0) { ConsoleStyle.Error("Extraction returned no bytes."); return; }

            using var ms = new MemoryStream(bytes);
            var mbin = new MBINFile(ms);
            mbin.Load();
            locTemplate = mbin.GetData();
            break;
        }

        if (locTemplate is null)
        {
            ConsoleStyle.Error("Could not find/decode language/nms_loc1_english.mbin in any PAK.");
            return;
        }

        var lookup = LocalisationService.BuildEnglishLookup(locTemplate);
        ConsoleStyle.Header($"Loc table decoded: {lookup.Count} total entries.");

        var raceByPrefix = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TRA"] = "Gek",
            ["WAR"] = "Vy'keen",
            ["EXP"] = "Korvax",
            ["BUI"] = "Autophage",
            // Not a race - a smaller (~262-word) special vocabulary pool with no
            // matching WORDS_LEARNT stat anywhere in Milestones, unlike the 4
            // real races. Lives in the exact same MF2 array/id-shape though
            // (confirmed 2026-08-04 - a few entries like ATLAS_STATION resolve
            // to short phrases rather than single words, but still pass the
            // same strict id-shape filter with zero lore-noise false positives).
            ["ATLAS"] = "Atlas"
        };

        var wordPattern = new Regex(@"^(TRA|WAR|EXP|BUI|ATLAS)_[A-Z']+$", RegexOptions.Compiled);

        var words = lookup
            .Where(kv => wordPattern.IsMatch(kv.Key))
            .Select(kv => (GameId: kv.Key.ToUpperInvariant(), NameEnglish: kv.Value, Race: raceByPrefix[kv.Key.Split('_')[0].ToUpperInvariant()]))
            .ToList();

        var byRace = words.GroupBy(w => w.Race).ToDictionary(g => g.Key, g => g.Count());
        ConsoleStyle.Header($"Filtered to {words.Count} real vocabulary words (excludes lore/UI-text noise sharing the same prefixes):");
        foreach (var (race, count) in byRace)
            ConsoleStyle.Success($"  {race}: {count}");

        using var db = new CatalogDbContext(targetDbPath);

        var existingCategory = db.Categories.FirstOrDefault(c => c.TemplateType == "GcAlienLanguageWords");
        if (existingCategory != null)
        {
            ConsoleStyle.Info("Existing GcAlienLanguageWords category found - removing before re-inserting (idempotent re-run).");
            var existingItems = db.Items.Where(i => i.CategoryId == existingCategory.Id);
            db.Items.RemoveRange(existingItems);
            db.Categories.Remove(existingCategory);
            db.SaveChanges();
        }

        var category = new CatalogCategory
        {
            TemplateType = "GcAlienLanguageWords",
            RowType = "TkLocalisationEntry",
            SourceMbinPath = "language/nms_loc1_english.mbin"
        };
        db.Categories.Add(category);
        db.SaveChanges();

        foreach (var word in words)
        {
            db.Items.Add(new CatalogItem
            {
                CategoryId = category.Id,
                GameId = word.GameId,
                NameEnglish = word.NameEnglish,
                UsageCategory = word.Race
            });
        }
        db.SaveChanges();

        ConsoleStyle.Success($"Wrote {words.Count} language word entries to {targetDbPath}.");
        return;
    }

    /// <summary>"DataCataloger add-broken-tech-flag &lt;target-catalog-db-path&gt;" marks every GcTechnology row whose game-data "BrokenSlotTech" flag is set (the damaged- slot placeholders - Hull Fracture, Short Circuit, etc. - drawn red in-game) in an EXISTING catalog DB, adding the Items.IsBrokenSlotTech column first if the DB predates it. Same "patch the shipped DB in place" pattern as add-language-words/add-guide-topics, so their already-merged rows survive without a full rebuild; fresh full builds get the flag via CatalogClassifier directly. Idempotent - safe to re-run after a game update.  Found 2026-10-10 investigating a user save whose Install Technology menu was cluttered with useless red entries: a "Mark All Known" bulk edit had taught it all 36 BrokenSlotTech rows, which a healthy save never knows.</summary>
    public static void AddBrokenTechFlag(string catalogPath, string pcbanksPath)
    {
        string brokenDbPath = catalogPath;
        if (!File.Exists(brokenDbPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {brokenDbPath}");
            return;
        }


        IPakReaderService brokenPakReader = new PakReaderService();
        var brokenExtraction = new ExtractionService();
        libMBIN.NMS.GameComponents.GcTechnologyTable? techTable = null;

        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            PakHeader header;
            IReadOnlyList<PakEntry> entries;
            try
            {
                header = brokenPakReader.ReadHeader(pak.FullPath);
                entries = brokenPakReader.Read(pak.FullPath);
            }
            catch { continue; }

            var match = entries.FirstOrDefault(e =>
                !string.IsNullOrEmpty(e.FileName) &&
                e.FileName.Contains("nms_reality_gctechnologytable.mbin", StringComparison.OrdinalIgnoreCase));
            if (match is null) continue;

            byte[]? bytes = brokenExtraction.ExtractEntryBytes(pak.FullPath, match, header);
            if (bytes is null || bytes.Length == 0) break;

            using var ms = new MemoryStream(bytes);
            var mbin = new MBINFile(ms);
            mbin.Load();
            techTable = mbin.GetData() as libMBIN.NMS.GameComponents.GcTechnologyTable;
            break;
        }

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
        return;
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


        IPakReaderService assetsPakReader = new PakReaderService();
        var assetsExtraction = new ExtractionService();
        var assetsIndex = new GlobalFileIndexService();

        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            PakHeader header;
            IReadOnlyList<PakEntry> entries;
            try
            {
                header = assetsPakReader.ReadHeader(pak.FullPath);
                entries = assetsPakReader.Read(pak.FullPath);
            }
            catch { continue; }

            assetsIndex.Add(pak.FullPath, header, entries);
        }

        // Glyphs
        // Glyphs keep the game's full texture size (the extractor never upscales) so the
        // app's enlarged glyph view stays sharp; only 16 images, so size is no concern.
        var iconService = new IconExtractionService(assetsExtraction, assetsIndex, targetSize: 1024);
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
        return;
    }

    /// <summary>"DataCataloger add-guide-topics &lt;target-catalog-db-path&gt;" extracts the in-game Guide screen's real topic-to-category grouping from metadata/reality/wiki.mbin (a GcWiki: Categories[].Topics[], found 2026-08-12 via `grep` on a known topic id, then `dumpfile` with bumped depth/listCap since the generic dumper's 4/5 defaults were too shallow to show more than placeholder "[0] [1] ..." for this shape - see DumpStructure's listCap parameter). Confirmed correct: this dump's output exactly matched a reference tool's own Guide tab across all 8 categories/57 categorized topics, screenshot-verified. Writes into the existing catalog DB as a new "GcWiki" category, same pattern as add-language-words - GameId=TopicID, NameEnglish=resolved topic name (merged across EVERY english loc file, not just one - an initial loc4- only version left 10 of the 59 ids showing as raw "UI_GUIDE_TOPIC_*" text in the UI, since some topics' strings live in other numbered loc files instead; same scattered-across-files behavior locidall's own doc comment already describes for ship/vehicle reward names, fixed the same way here), UsageCategory=resolved category name (e.g. "Survival Basics"). ArtifactX's own GuidePage reads this directly rather than a hardcoded Almanac list, so a future game update's new/reordered topics get picked up automatically next time this command is re-run - no code change needed on the app side.  2 of the 59 known UI_GUIDE_TOPIC_* loc ids (EXPLORATION_2 "Finding Resources", TRADE_3A "Trading Basics") genuinely aren't in ANY category's Topics list in this data - not a bug, confirmed by direct inspection of the raw dump. Written with UsageCategory="Uncategorized" rather than dropped, since they're still real, independently toggleable ids in accountdata.hg (confirmed via the same real controlled test that found this whole feature).</summary>
    public static void AddGuideTopics(string catalogPath, string pcbanksPath)
    {
        string guideDbPath = catalogPath;
        if (!File.Exists(guideDbPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {guideDbPath}");
            return;
        }


        IPakDiscoveryService guidePakDiscovery = new PakDiscoveryService();
        IPakReaderService guidePakReader = new PakReaderService();
        var guideExtraction = new ExtractionService();
        var guidePaks = guidePakDiscovery.Discover(pcbanksPath);

        NMSTemplate? DecodeFirstMatch(string pathSubstring)
        {
            foreach (var pak in guidePaks)
            {
                PakHeader header;
                IReadOnlyList<PakEntry> entries;
                try
                {
                    header = guidePakReader.ReadHeader(pak.FullPath);
                    entries = guidePakReader.Read(pak.FullPath);
                }
                catch { continue; }

                var match = entries.FirstOrDefault(e =>
                    !string.IsNullOrEmpty(e.FileName) &&
                    e.FileName.Contains(pathSubstring, StringComparison.OrdinalIgnoreCase));
                if (match is null) continue;

                byte[]? bytes;
                try { bytes = guideExtraction.ExtractEntryBytes(pak.FullPath, match, header); }
                catch (Exception ex) { ConsoleStyle.Error($"Extraction failed: {ex.Message}"); return null; }
                if (bytes is null || bytes.Length == 0) { ConsoleStyle.Error("Extraction returned no bytes."); return null; }

                using var ms = new MemoryStream(bytes);
                var mbin = new MBINFile(ms);
                mbin.Load();
                return mbin.GetData();
            }
            return null;
        }

        // Merged across EVERY english loc file, not just loc4 - loc4 alone was
        // the first fix, but 10 of the 59 real topic/category ids (e.g.
        // UI_GUIDE_TOPIC_SETTLEMENTS, UI_GUIDE_TOPIC_WORDS) turned out to live
        // in OTHER loc files instead, surfacing as raw ids in the UI (caught by
        // user feedback 2026-08-12 - direct violation of the standing "no raw
        // save keys in UI" rule). Same root cause and same fix as locidall's own
        // doc comment already describes for ship/vehicle reward names: content
        // is scattered across numbered loc files per-category, not consolidated
        // in one.
        var guideLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pak in guidePaks)
        {
            PakHeader header;
            IReadOnlyList<PakEntry> entries;
            try
            {
                header = guidePakReader.ReadHeader(pak.FullPath);
                entries = guidePakReader.Read(pak.FullPath);
            }
            catch { continue; }

            var locEntries = entries.Where(e =>
                !string.IsNullOrEmpty(e.FileName) &&
                e.FileName.Contains("language/nms_", StringComparison.OrdinalIgnoreCase) &&
                e.FileName.Contains("english", StringComparison.OrdinalIgnoreCase) &&
                !e.FileName.Contains("usenglish", StringComparison.OrdinalIgnoreCase));

            foreach (var entry in locEntries)
            {
                byte[]? bytes;
                try { bytes = guideExtraction.ExtractEntryBytes(pak.FullPath, entry, header); }
                catch { continue; }
                if (bytes is null || bytes.Length == 0) continue;

                NMSTemplate? locTemplate;
                try
                {
                    using var ms = new MemoryStream(bytes);
                    var mbin = new MBINFile(ms);
                    mbin.Load();
                    locTemplate = mbin.GetData();
                }
                catch { continue; }
                if (locTemplate is null) continue;

                foreach (var kv in LocalisationService.BuildEnglishLookup(locTemplate))
                    guideLookup.TryAdd(kv.Key, kv.Value);
            }
        }
        ConsoleStyle.Header($"Merged loc tables decoded: {guideLookup.Count} total entries.");

        var wikiTemplate = DecodeFirstMatch("metadata/reality/wiki.mbin");
        if (wikiTemplate is not libMBIN.NMS.GameComponents.GcWiki wiki || wiki.Categories is null)
        {
            ConsoleStyle.Error("Could not find/decode metadata/reality/wiki.mbin as GcWiki in any PAK.");
            return;
        }

        var guideRows = new List<(string GameId, string NameEnglish, string CategoryName)>();
        var seenTopicIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var category in wiki.Categories)
        {
            string categoryId = category.CategoryID?.Value ?? "";
            string categoryName = guideLookup.TryGetValue(categoryId, out var catName) ? catName : categoryId;

            if (category.Topics is null) continue;

            foreach (var topic in category.Topics)
            {
                string topicId = topic.TopicID?.Value ?? "";
                if (topicId.Length == 0 || !seenTopicIds.Add(topicId)) continue;

                string topicName = guideLookup.TryGetValue(topicId, out var topName) ? topName : topicId;
                guideRows.Add((topicId, topicName, categoryName));
            }
        }

        // Every real UI_GUIDE_TOPIC_* id from the loc table that this category
        // walk didn't find a home for - see the Uncategorized note above.
        var uncategorizedPattern = new Regex(@"^UI_GUIDE_TOPIC_[A-Z0-9_]+$", RegexOptions.Compiled);
        var uncategorized = guideLookup.Keys
            .Where(id => uncategorizedPattern.IsMatch(id) && seenTopicIds.Add(id))
            .Select(id => (GameId: id, NameEnglish: guideLookup[id], CategoryName: "Uncategorized"))
            .ToList();
        guideRows.AddRange(uncategorized);

        var byCategory = guideRows.GroupBy(r => r.CategoryName).ToDictionary(g => g.Key, g => g.Count());
        ConsoleStyle.Header($"Found {guideRows.Count} Guide topics across {byCategory.Count} categories:");
        foreach (var (cat, count) in byCategory)
            ConsoleStyle.Success($"  {cat}: {count}");

        using var guideDb = new CatalogDbContext(guideDbPath);

        var existingGuideCategory = guideDb.Categories.FirstOrDefault(c => c.TemplateType == "GcWiki");
        if (existingGuideCategory != null)
        {
            ConsoleStyle.Info("Existing GcWiki category found - removing before re-inserting (idempotent re-run).");
            var existingGuideItems = guideDb.Items.Where(i => i.CategoryId == existingGuideCategory.Id);
            guideDb.Items.RemoveRange(existingGuideItems);
            guideDb.Categories.Remove(existingGuideCategory);
            guideDb.SaveChanges();
        }

        var guideCategory = new CatalogCategory
        {
            TemplateType = "GcWiki",
            RowType = "GcWikiTopic",
            SourceMbinPath = "metadata/reality/wiki.mbin"
        };
        guideDb.Categories.Add(guideCategory);
        guideDb.SaveChanges();

        foreach (var row in guideRows)
        {
            guideDb.Items.Add(new CatalogItem
            {
                CategoryId = guideCategory.Id,
                GameId = row.GameId,
                NameEnglish = row.NameEnglish,
                UsageCategory = row.CategoryName
            });
        }
        guideDb.SaveChanges();

        ConsoleStyle.Success($"Wrote {guideRows.Count} Guide topic entries to {guideDbPath}.");
        return;
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


        IPakReaderService paintPakReader = new PakReaderService();
        var paintExtraction = new ExtractionService();
        var paintPaks = new PakDiscoveryService().Discover(pcbanksPath);

        libMBIN.NMS.GameComponents.GcCustomisationColourPalettes? palettes = null;
        var paintLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pak in paintPaks)
        {
            PakHeader header;
            IReadOnlyList<PakEntry> entries;
            try
            {
                header = paintPakReader.ReadHeader(pak.FullPath);
                entries = paintPakReader.Read(pak.FullPath);
            }
            catch { continue; }

            // Loc names scatter across numbered english files (see add-guide-topics).
            var wanted = entries.Where(e =>
                !string.IsNullOrEmpty(e.FileName) &&
                ((e.FileName.Contains("language/nms_", StringComparison.OrdinalIgnoreCase) &&
                  e.FileName.Contains("english", StringComparison.OrdinalIgnoreCase) &&
                  !e.FileName.Contains("usenglish", StringComparison.OrdinalIgnoreCase)) ||
                 (palettes is null && e.FileName.Contains("customisationcolourpalettes.mbin", StringComparison.OrdinalIgnoreCase))));

            foreach (var entry in wanted)
            {
                byte[]? bytes;
                try { bytes = paintExtraction.ExtractEntryBytes(pak.FullPath, entry, header); }
                catch { continue; }
                if (bytes is null || bytes.Length == 0) continue;

                NMSTemplate? template;
                try
                {
                    using var ms = new MemoryStream(bytes);
                    var mbin = new MBINFile(ms);
                    mbin.Load();
                    template = mbin.GetData();
                }
                catch { continue; }

                if (template is libMBIN.NMS.GameComponents.GcCustomisationColourPalettes p)
                    palettes = p;
                else if (template is not null)
                    foreach (var kv in LocalisationService.BuildEnglishLookup(template))
                        paintLookup.TryAdd(kv.Key, kv.Value);
            }
        }

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
        var existingPaintCategory = paintDb.Categories.FirstOrDefault(c => c.TemplateType == "FreighterPaints");
        if (existingPaintCategory != null)
        {
            paintDb.Items.RemoveRange(paintDb.Items.Where(i => i.CategoryId == existingPaintCategory.Id));
            paintDb.Categories.Remove(existingPaintCategory);
            paintDb.SaveChanges();
        }

        var paintCategory = new CatalogCategory
        {
            TemplateType = "FreighterPaints",
            RowType = "GcCustomisationColourPalette",
            SourceMbinPath = "metadata/gamestate/playerdata/customisationcolourpalettes.mbin"
        };
        paintDb.Categories.Add(paintCategory);
        paintDb.SaveChanges();

        foreach (var paint in paints)
        {
            paintDb.Items.Add(new CatalogItem
            {
                CategoryId = paintCategory.Id,
                GameId = paint.GameId,
                NameEnglish = paint.Name,
                DescriptionEnglish = paint.Hex,
                UsageCategory = "Recolouring"
            });
        }
        paintDb.SaveChanges();

        ConsoleStyle.Success($"Wrote {paints.Count} freighter paint entries to {paintDbPath}.");
        return;
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


        IPakReaderService corvettePakReader = new PakReaderService();
        var corvetteExtraction = new ExtractionService();
        libMBIN.NMS.GameComponents.GcBaseBuildingTable? buildingTable = null;
        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            if (buildingTable is not null) break;
            PakHeader header;
            IReadOnlyList<PakEntry> entries;
            try
            {
                header = corvettePakReader.ReadHeader(pak.FullPath);
                entries = corvettePakReader.Read(pak.FullPath);
            }
            catch { continue; }

            foreach (var entry in entries.Where(e => !string.IsNullOrEmpty(e.FileName)
                         && e.FileName.Contains("basebuildingobjectstable.mbin", StringComparison.OrdinalIgnoreCase)))
            {
                byte[]? bytes;
                try { bytes = corvetteExtraction.ExtractEntryBytes(pak.FullPath, entry, header); }
                catch { continue; }
                if (bytes is null || bytes.Length == 0) continue;
                try
                {
                    using var ms = new MemoryStream(bytes);
                    var mbin = new MBINFile(ms);
                    mbin.Load();
                    if (mbin.GetData() is libMBIN.NMS.GameComponents.GcBaseBuildingTable table) { buildingTable = table; break; }
                }
                catch { }
            }
        }

        if (buildingTable?.Objects is null)
        {
            ConsoleStyle.Error("Could not find GcBaseBuildingTable (basebuildingobjectstable.mbin).");
            return;
        }

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
                });
        }
        ConsoleStyle.Header($"Found {corvetteParts.Count} corvette parts " +
            $"({corvetteParts.Values.Count(k => k != "Decorative")} structural, {corvetteParts.Values.Count(k => k != "Structural")} decorative).");

        using var corvetteDb = new CatalogDbContext(corvetteDbPath);
        var existingCorvetteCategory = corvetteDb.Categories.FirstOrDefault(c => c.TemplateType == "CorvetteParts");
        if (existingCorvetteCategory != null)
        {
            corvetteDb.Items.RemoveRange(corvetteDb.Items.Where(i => i.CategoryId == existingCorvetteCategory.Id));
            corvetteDb.Categories.Remove(existingCorvetteCategory);
            corvetteDb.SaveChanges();
        }

        var corvetteCategory = new CatalogCategory
        {
            TemplateType = "CorvetteParts",
            RowType = "GcBaseBuildingEntry",
            SourceMbinPath = "metadata/reality/tables/basebuildingobjectstable.mbin"
        };
        corvetteDb.Categories.Add(corvetteCategory);
        corvetteDb.SaveChanges();

        foreach (var (itemId, kind) in corvetteParts)
            corvetteDb.Items.Add(new CatalogItem
            {
                CategoryId = corvetteCategory.Id,
                GameId = "CORVETTE:" + itemId,
                NameEnglish = itemId,
                UsageCategory = kind,
                DescriptionEnglish = corvetteFacts.GetValueOrDefault(itemId)
            });
        corvetteDb.SaveChanges();

        ConsoleStyle.Success($"Wrote {corvetteParts.Count} corvette parts to {corvetteDbPath}.");
        return;
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


        IPakReaderService statsPakReader = new PakReaderService();
        var statsExtraction = new ExtractionService();
        libMBIN.NMS.GameComponents.GcTechnologyTable? techTable = null;
        libMBIN.NMS.GameComponents.GcProceduralTechnologyTable? procTable = null;
        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            if (techTable is not null && procTable is not null) break;
            PakHeader header;
            IReadOnlyList<PakEntry> entries;
            try
            {
                header = statsPakReader.ReadHeader(pak.FullPath);
                entries = statsPakReader.Read(pak.FullPath);
            }
            catch { continue; }

            foreach (var entry in entries.Where(e => !string.IsNullOrEmpty(e.FileName)
                         && (e.FileName.EndsWith("nms_reality_gctechnologytable.mbin", StringComparison.OrdinalIgnoreCase)
                             || e.FileName.EndsWith("nms_reality_gcproceduraltechnologytable.mbin", StringComparison.OrdinalIgnoreCase))))
            {
                byte[]? bytes;
                try { bytes = statsExtraction.ExtractEntryBytes(pak.FullPath, entry, header); }
                catch { continue; }
                if (bytes is null || bytes.Length == 0) continue;
                try
                {
                    using var ms = new MemoryStream(bytes);
                    var mbin = new MBINFile(ms);
                    mbin.Load();
                    switch (mbin.GetData())
                    {
                        case libMBIN.NMS.GameComponents.GcTechnologyTable t: techTable ??= t; break;
                        case libMBIN.NMS.GameComponents.GcProceduralTechnologyTable t: procTable ??= t; break;
                    }
                }
                catch { }
            }
        }

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
        var existingStatsCategory = statsDb.Categories.FirstOrDefault(c => c.TemplateType == "TechStats");
        if (existingStatsCategory != null)
        {
            statsDb.Items.RemoveRange(statsDb.Items.Where(i => i.CategoryId == existingStatsCategory.Id));
            statsDb.Categories.Remove(existingStatsCategory);
            statsDb.SaveChanges();
        }

        var statsCategory = new CatalogCategory
        {
            TemplateType = "TechStats",
            RowType = "GcTechnology",
            SourceMbinPath = "metadata/reality/tables/nms_reality_gctechnologytable.mbin"
        };
        statsDb.Categories.Add(statsCategory);
        statsDb.SaveChanges();

        foreach (var (id, info) in techStats)
            statsDb.Items.Add(new CatalogItem
            {
                CategoryId = statsCategory.Id,
                GameId = "TECHSTAT:" + id,
                NameEnglish = info.Stat,
                DescriptionEnglish = info.Link,
                UsageCategory = info.Primary ? "Primary" : moduleTemplate.TryGetValue(id, out var tpl) ? "Module:" + tpl : ""
            });
        statsDb.SaveChanges();

        ConsoleStyle.Success($"Wrote {techStats.Count} tech stats to {statsDbPath}.");
        return;
    }

    /// <summary>"DataCataloger add-system-descriptors &lt;target-catalog-db-path&gt;" adds the words the game uses to describe a star system's economy, wealth and conflict (system info panel / Galaxy Map), e.g. "Mathematical", "Opulent", "Formidable". Each is a loc string named for its category: UI_ECON_CLASS_&lt;TRADE|MINING|MANUFACT|TECH|SCIENCE|POWER|ALLOY&gt;_&lt;n&gt; UI_ECON_LEVEL_&lt;LOW|MED|HIGH&gt;_&lt;n&gt;        (wealth) UI_CONFLICT_LEVEL_&lt;LOW|MED|HIGH&gt;_&lt;n&gt; found 2026-10-11 via `locval` on words from a user screenshot (they live in nms_update3_english). Written as category "SystemDescriptors": GameId = loc id, NameEnglish = the word, UsageCategory = Economy/Wealth/Conflict, DescriptionEnglish = the tier (TRADE, HIGH...). ArtifactX's Planets page uses these so players can pick the exact word the game shows them. Same patch-in-place pattern as add-guide-topics; idempotent.</summary>
    public static void AddSystemDescriptors(string catalogPath, string pcbanksPath)
    {
        string descDbPath = catalogPath;
        if (!File.Exists(descDbPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {descDbPath}");
            return;
        }


        IPakReaderService descPakReader = new PakReaderService();
        var descExtraction = new ExtractionService();
        var descLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            PakHeader header;
            IReadOnlyList<PakEntry> entries;
            try
            {
                header = descPakReader.ReadHeader(pak.FullPath);
                entries = descPakReader.Read(pak.FullPath);
            }
            catch { continue; }

            foreach (var entry in entries.Where(e =>
                         !string.IsNullOrEmpty(e.FileName) &&
                         e.FileName.Contains("language/nms_", StringComparison.OrdinalIgnoreCase) &&
                         e.FileName.Contains("english", StringComparison.OrdinalIgnoreCase) &&
                         !e.FileName.Contains("usenglish", StringComparison.OrdinalIgnoreCase)))
            {
                byte[]? bytes;
                try { bytes = descExtraction.ExtractEntryBytes(pak.FullPath, entry, header); }
                catch { continue; }
                if (bytes is null || bytes.Length == 0) continue;

                NMSTemplate? locTemplate;
                try
                {
                    using var ms = new MemoryStream(bytes);
                    var mbin = new MBINFile(ms);
                    mbin.Load();
                    locTemplate = mbin.GetData();
                }
                catch { continue; }
                if (locTemplate is null) continue;

                foreach (var kv in LocalisationService.BuildEnglishLookup(locTemplate))
                    descLookup.TryAdd(kv.Key, kv.Value);
            }
        }

        var descPattern = new Regex(@"^UI_(ECON_CLASS|ECON_LEVEL|CONFLICT_LEVEL)_([A-Z]+)_\d+$", RegexOptions.Compiled);
        var descriptors = descLookup
            .Select(kv => (Id: kv.Key, Word: kv.Value, Match: descPattern.Match(kv.Key)))
            .Where(x => x.Match.Success && !string.IsNullOrWhiteSpace(x.Word))
            .Select(x => (x.Id, x.Word,
                Group: x.Match.Groups[1].Value switch { "ECON_CLASS" => "Economy", "ECON_LEVEL" => "Wealth", _ => "Conflict" },
                Tier: x.Match.Groups[2].Value))
            .OrderBy(x => x.Group).ThenBy(x => x.Tier).ThenBy(x => x.Id, StringComparer.Ordinal)
            .ToList();

        foreach (var group in descriptors.GroupBy(d => (d.Group, d.Tier)))
            ConsoleStyle.Success($"  {group.Key.Group}/{group.Key.Tier}: {string.Join(", ", group.Select(d => d.Word))}");

        using var descDb = new CatalogDbContext(descDbPath);
        var existingDescCategory = descDb.Categories.FirstOrDefault(c => c.TemplateType == "SystemDescriptors");
        if (existingDescCategory != null)
        {
            descDb.Items.RemoveRange(descDb.Items.Where(i => i.CategoryId == existingDescCategory.Id));
            descDb.Categories.Remove(existingDescCategory);
            descDb.SaveChanges();
        }

        var descCategory = new CatalogCategory
        {
            TemplateType = "SystemDescriptors",
            RowType = "LocString",
            SourceMbinPath = "language/nms_*_english.mbin"
        };
        descDb.Categories.Add(descCategory);
        descDb.SaveChanges();

        foreach (var d in descriptors)
        {
            descDb.Items.Add(new CatalogItem
            {
                CategoryId = descCategory.Id,
                GameId = d.Id,
                NameEnglish = d.Word,
                DescriptionEnglish = d.Tier,
                UsageCategory = d.Group
            });
        }
        descDb.SaveChanges();

        ConsoleStyle.Success($"Wrote {descriptors.Count} system descriptor words to {descDbPath}.");
        return;
    }

    /// <summary>"DataCataloger add-planet-descriptors &lt;target-catalog-db-path&gt;" adds the words the game's discovery screen uses to describe a planet, which the save doesn't store: weather (WEATHER_&lt;biome&gt;&lt;n&gt;, e.g. WEATHER_LUSHEXTREME6 "Painfully Hot Rain", in nms_update3_english), sentinels (SENTINEL_&lt;level&gt;&lt;n&gt;, e.g. SENTINEL_HIGH2 "Frenzied") and flora/fauna (RARITY_&lt;level&gt;&lt;n&gt;, e.g. RARITY_HIGH6 "Generous"). Found 2026-10-13 via `locval` on words from a user screenshot. Category "PlanetDescriptors": GameId = loc id, NameEnglish = the word, UsageCategory = Weather/Sentinels/Life, DescriptionEnglish = the tier (biome or level). Same pattern as add-system-descriptors; idempotent.</summary>
    public static void AddPlanetDescriptors(string catalogPath, string pcbanksPath)
    {
        string planetDescDbPath = catalogPath;
        if (!File.Exists(planetDescDbPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {planetDescDbPath}");
            return;
        }


        IPakReaderService planetDescPakReader = new PakReaderService();
        var planetDescExtraction = new ExtractionService();
        var planetDescLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            PakHeader header;
            IReadOnlyList<PakEntry> entries;
            try
            {
                header = planetDescPakReader.ReadHeader(pak.FullPath);
                entries = planetDescPakReader.Read(pak.FullPath);
            }
            catch { continue; }

            foreach (var entry in entries.Where(e =>
                         !string.IsNullOrEmpty(e.FileName) &&
                         e.FileName.Contains("language/nms_", StringComparison.OrdinalIgnoreCase) &&
                         e.FileName.Contains("english", StringComparison.OrdinalIgnoreCase) &&
                         !e.FileName.Contains("usenglish", StringComparison.OrdinalIgnoreCase)))
            {
                byte[]? bytes;
                try { bytes = planetDescExtraction.ExtractEntryBytes(pak.FullPath, entry, header); }
                catch { continue; }
                if (bytes is null || bytes.Length == 0) continue;

                NMSTemplate? locTemplate;
                try
                {
                    using var ms = new MemoryStream(bytes);
                    var mbin = new MBINFile(ms);
                    mbin.Load();
                    locTemplate = mbin.GetData();
                }
                catch { continue; }
                if (locTemplate is null) continue;

                foreach (var kv in LocalisationService.BuildEnglishLookup(locTemplate))
                    planetDescLookup.TryAdd(kv.Key, kv.Value);
            }
        }

        // Tiers can contain "_" (WEATHER_LAVA_CLEAR1 "Sulphurous Haze" was missed
        // by [A-Z]+, 2026-10-14).
        var planetDescPattern = new Regex(@"^(WEATHER|SENTINEL|RARITY)_([A-Z_]+?)_?\d+$", RegexOptions.Compiled);
        var planetDescriptors = planetDescLookup
            .Select(kv => (Id: kv.Key, Word: kv.Value.Trim(), Match: planetDescPattern.Match(kv.Key)))
            .Where(x => x.Match.Success && x.Word.Length is > 0 and <= 40 && !x.Word.Contains('%') && !x.Word.Contains('<'))
            .Select(x => (x.Id, x.Word,
                Group: x.Match.Groups[1].Value switch { "WEATHER" => "Weather", "SENTINEL" => "Sentinels", _ => "Life" },
                Tier: x.Match.Groups[2].Value))
            // SENTINEL_ also names drone status text (STATE_PATROL "Patrolling...",
            // MISSION_GUARD, ROUTINE_ALERT...) - keep only the planet levels.
            .Where(x => x.Group != "Sentinels" || x.Tier is "NONE" or "RARE" or "LOW" or "DEFAULT" or "HIGH" or "AGGRESSIVE" or "CORRUPT")
            // The special finds a planet's Resources list can show besides
            // substances: "Ancient Bones" (UI_BONES_HINT), "Salvageable Scrap"
            // (UI_SCRAP_HINT), Vile Brood, Swarm wreckage (2026-10-14).
            .Concat(planetDescLookup
                .Where(kv => kv.Key is "UI_BONES_HINT" or "UI_SCRAP_HINT" or "UI_BUGS_HINT" or "UI_SWARM_HINT")
                .Select(kv => (Id: kv.Key, Word: kv.Value.Trim(), Group: "Resource", Tier: "SPECIAL")))
            .OrderBy(x => x.Group).ThenBy(x => x.Tier).ThenBy(x => x.Id, StringComparer.Ordinal)
            .ToList();

        foreach (var group in planetDescriptors.GroupBy(d => (d.Group, d.Tier)))
            ConsoleStyle.Success($"  {group.Key.Group}/{group.Key.Tier}: {string.Join(", ", group.Select(d => d.Word))}");

        using var planetDescDb = new CatalogDbContext(planetDescDbPath);
        var existingPlanetDescCategory = planetDescDb.Categories.FirstOrDefault(c => c.TemplateType == "PlanetDescriptors");
        if (existingPlanetDescCategory != null)
        {
            planetDescDb.Items.RemoveRange(planetDescDb.Items.Where(i => i.CategoryId == existingPlanetDescCategory.Id));
            planetDescDb.Categories.Remove(existingPlanetDescCategory);
            planetDescDb.SaveChanges();
        }

        var planetDescCategory = new CatalogCategory
        {
            TemplateType = "PlanetDescriptors",
            RowType = "LocString",
            SourceMbinPath = "language/nms_*_english.mbin"
        };
        planetDescDb.Categories.Add(planetDescCategory);
        planetDescDb.SaveChanges();

        foreach (var d in planetDescriptors)
        {
            planetDescDb.Items.Add(new CatalogItem
            {
                CategoryId = planetDescCategory.Id,
                GameId = d.Id,
                NameEnglish = d.Word,
                DescriptionEnglish = d.Tier,
                UsageCategory = d.Group
            });
        }
        planetDescDb.SaveChanges();

        ConsoleStyle.Success($"Wrote {planetDescriptors.Count} planet descriptor words to {planetDescDbPath}.");
        return;
    }

    /// <summary>"DataCataloger add-recipes &lt;target-catalog-db-path&gt;" adds every refiner and cooking recipe from metadata/reality/tables/nms_reality_gcrecipetable.mbin (GcRecipeTable: Id, RecipeName/RecipeType loc keys, Result + Ingredients [{Id, Amount, Type}], TimeToMake, Cooking) as category "Recipes". Stored as ordinary catalog items so they survive `trim` with no schema change: GameId = recipe id, NameEnglish = resolved recipe name, UsageCategory = "Refiner"/"Cooking", DescriptionEnglish = compact JSON {"type","result":[id,amount],"ingredients":[[id,amount],...],"time"} - ids are item GameIds (products/substances), resolved to names/icons by the app through the normal catalog lookup. Added 2026-10-11 for the Recipes tool; same patch-in-place pattern as add-guide-topics; idempotent.</summary>
    public static void AddRecipes(string catalogPath, string pcbanksPath)
    {
        string recipeDbPath = catalogPath;
        if (!File.Exists(recipeDbPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {recipeDbPath}");
            return;
        }


        IPakReaderService recipePakReader = new PakReaderService();
        var recipeExtraction = new ExtractionService();
        libMBIN.NMS.GameComponents.GcRecipeTable? recipeTable = null;
        var recipeLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            PakHeader header;
            IReadOnlyList<PakEntry> entries;
            try
            {
                header = recipePakReader.ReadHeader(pak.FullPath);
                entries = recipePakReader.Read(pak.FullPath);
            }
            catch { continue; }

            var wanted = entries.Where(e =>
                !string.IsNullOrEmpty(e.FileName) &&
                ((e.FileName.Contains("language/nms_", StringComparison.OrdinalIgnoreCase) &&
                  e.FileName.Contains("english", StringComparison.OrdinalIgnoreCase) &&
                  !e.FileName.Contains("usenglish", StringComparison.OrdinalIgnoreCase)) ||
                 (recipeTable is null && e.FileName.Contains("nms_reality_gcrecipetable.mbin", StringComparison.OrdinalIgnoreCase))));

            foreach (var entry in wanted)
            {
                byte[]? bytes;
                try { bytes = recipeExtraction.ExtractEntryBytes(pak.FullPath, entry, header); }
                catch { continue; }
                if (bytes is null || bytes.Length == 0) continue;

                NMSTemplate? template;
                try
                {
                    using var ms = new MemoryStream(bytes);
                    var mbin = new MBINFile(ms);
                    mbin.Load();
                    template = mbin.GetData();
                }
                catch { continue; }

                if (template is libMBIN.NMS.GameComponents.GcRecipeTable table)
                    recipeTable = table;
                else if (template is not null)
                    foreach (var kv in LocalisationService.BuildEnglishLookup(template))
                        recipeLookup.TryAdd(kv.Key, kv.Value);
            }
        }

        if (recipeTable?.Table is null)
        {
            ConsoleStyle.Error("Could not find/decode nms_reality_gcrecipetable.mbin in any PAK.");
            return;
        }

        string Loc(string? id) => id is { Length: > 0 } && recipeLookup.TryGetValue(id, out var text) ? text : id ?? "";
        var recipes = new List<CatalogItem>();
        foreach (var r in recipeTable.Table)
        {
            string id = r.Id?.Value ?? "";
            string resultId = r.Result?.Id?.Value ?? "";
            var ingredients = (r.Ingredients ?? new()).Where(i => !string.IsNullOrEmpty(i.Id?.Value)).ToList();
            if (id.Length == 0 || resultId.Length == 0 || ingredients.Count == 0) continue;

            var payload = new System.Text.Json.Nodes.JsonObject
            {
                ["type"] = Loc(r.RecipeType?.Value),
                ["result"] = new System.Text.Json.Nodes.JsonArray(resultId, r.Result!.Amount),
                ["ingredients"] = new System.Text.Json.Nodes.JsonArray(ingredients
                    .Select(i => (System.Text.Json.Nodes.JsonNode)new System.Text.Json.Nodes.JsonArray(i.Id.Value, i.Amount)).ToArray()),
                ["time"] = r.TimeToMake,
            };

            recipes.Add(new CatalogItem
            {
                GameId = id,
                NameEnglish = Loc(r.RecipeName?.Value),
                UsageCategory = r.Cooking ? "Cooking" : "Refiner",
                DescriptionEnglish = payload.ToJsonString(),
            });
        }

        ConsoleStyle.Header($"Found {recipes.Count} recipes ({recipes.Count(r => r.UsageCategory == "Cooking")} cooking, {recipes.Count(r => r.UsageCategory == "Refiner")} refiner).");
        foreach (var sample in recipes.Take(5))
            ConsoleStyle.Success($"  {sample.GameId} {sample.UsageCategory} '{sample.NameEnglish}' {sample.DescriptionEnglish}");

        using var recipeDb = new CatalogDbContext(recipeDbPath);
        var existingRecipeCategory = recipeDb.Categories.FirstOrDefault(c => c.TemplateType == "Recipes");
        if (existingRecipeCategory != null)
        {
            recipeDb.Items.RemoveRange(recipeDb.Items.Where(i => i.CategoryId == existingRecipeCategory.Id));
            recipeDb.Categories.Remove(existingRecipeCategory);
            recipeDb.SaveChanges();
        }

        var recipeCategory = new CatalogCategory
        {
            TemplateType = "Recipes",
            RowType = "GcRefinerRecipe",
            SourceMbinPath = "metadata/reality/tables/nms_reality_gcrecipetable.mbin"
        };
        recipeDb.Categories.Add(recipeCategory);
        recipeDb.SaveChanges();

        foreach (var recipe in recipes) recipe.CategoryId = recipeCategory.Id;
        recipeDb.Items.AddRange(recipes);
        recipeDb.SaveChanges();

        ConsoleStyle.Success($"Wrote {recipes.Count} recipes to {recipeDbPath}.");
        return;
    }

    /// <summary>"DataCataloger add-fish &lt;target-catalog-db-path&gt;" adds where/when every fish can be caught, from the game's GcFishTable (found by type, not by path): ProductID, Quality, Size, Time (Day/Night/Both), Biome (bool[17] in GcBiomeType order - the same order as a planet's biome in the save's discovery records), NeedsStorm, RequiresMissionActive. Category "Fish"; GameId = "FISH:" + product id (prefixed so it never collides with the fish item's own catalog row), NameEnglish = product id (only so the row survives `trim` - the app names fish through the normal item lookup), DescriptionEnglish = JSON {"id","quality","size","time","storm","mission", "biomes":[indexes]}. Added 2026-10-11 for the Fishing tool; idempotent.</summary>
    public static void AddFish(string catalogPath, string pcbanksPath)
    {
        string fishDbPath = catalogPath;
        if (!File.Exists(fishDbPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {fishDbPath}");
            return;
        }


        IPakReaderService fishPakReader = new PakReaderService();
        var fishExtraction = new ExtractionService();
        libMBIN.NMS.GameComponents.GcFishTable? fishTable = null;
        libMBIN.NMS.Globals.GcFishingGlobals? fishingGlobals = null;
        libMBIN.NMS.GameComponents.GcBaitTable? baitTable = null;
        string fishSource = "";

        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            if (fishTable is not null && fishingGlobals is not null && baitTable is not null) break;
            PakHeader header;
            IReadOnlyList<PakEntry> entries;
            try
            {
                header = fishPakReader.ReadHeader(pak.FullPath);
                entries = fishPakReader.Read(pak.FullPath);
            }
            catch { continue; }

            // The fish table, the fishing globals (gcfishingglobals.global.mbin: how depth picks a
            // fish's size) and the bait table (metadata/reality/tables/baitdatatable.mbin).
            foreach (var entry in entries.Where(e => !string.IsNullOrEmpty(e.FileName)
                         && e.FileName.EndsWith(".mbin", StringComparison.OrdinalIgnoreCase)
                         && !e.FileName.Contains("/anims/", StringComparison.OrdinalIgnoreCase)
                         && (e.FileName.Contains("fish", StringComparison.OrdinalIgnoreCase) || e.FileName.Contains("bait", StringComparison.OrdinalIgnoreCase))))
            {
                byte[]? bytes;
                try { bytes = fishExtraction.ExtractEntryBytes(pak.FullPath, entry, header); }
                catch { continue; }
                if (bytes is null || bytes.Length == 0) continue;

                try
                {
                    using var ms = new MemoryStream(bytes);
                    var mbin = new MBINFile(ms);
                    mbin.Load();
                    switch (mbin.GetData())
                    {
                        case libMBIN.NMS.GameComponents.GcFishTable table when fishTable is null:
                            fishTable = table;
                            fishSource = entry.FileName!;
                            break;
                        case libMBIN.NMS.Globals.GcFishingGlobals globals when fishingGlobals is null:
                            fishingGlobals = globals;
                            break;
                        case libMBIN.NMS.GameComponents.GcBaitTable baits when baitTable is null:
                            baitTable = baits;
                            break;
                    }
                }
                catch { continue; }
            }
        }

        if (fishTable?.Fish is null)
        {
            ConsoleStyle.Error("Could not find/decode a GcFishTable in any PAK.");
            return;
        }

        var fishRows = new List<CatalogItem>();
        foreach (var f in fishTable.Fish)
        {
            string id = f.ProductID?.Value ?? "";
            if (id.Length == 0) continue;
            var biomes = new System.Text.Json.Nodes.JsonArray();
            for (int i = 0; i < (f.Biome?.Length ?? 0); i++)
                if (f.Biome![i]) biomes.Add(i);
            var payload = new System.Text.Json.Nodes.JsonObject
            {
                ["id"] = id,
                ["quality"] = f.Quality?.ItemQuality.ToString() ?? "",
                ["size"] = f.Size?.FishSize.ToString() ?? "",
                ["time"] = f.Time?.FishingTime.ToString() ?? "",
                ["storm"] = f.NeedsStorm,
                ["mission"] = !string.IsNullOrEmpty(f.RequiresMissionActive?.Value),
                ["biomes"] = biomes,
            };
            fishRows.Add(new CatalogItem
            {
                GameId = "FISH:" + id,
                NameEnglish = id,
                UsageCategory = f.Quality?.ItemQuality.ToString() ?? "",
                DescriptionEnglish = payload.ToJsonString(),
            });
        }

        ConsoleStyle.Header($"Found {fishRows.Count} fish in {fishSource}.");
        foreach (var sample in fishRows.Take(5))
            ConsoleStyle.Success($"  {sample.GameId} {sample.DescriptionEnglish}");

        using var fishDb = new CatalogDbContext(fishDbPath);
        var existingFishCategory = fishDb.Categories.FirstOrDefault(c => c.TemplateType == "Fish");
        if (existingFishCategory != null)
        {
            fishDb.Items.RemoveRange(fishDb.Items.Where(i => i.CategoryId == existingFishCategory.Id));
            fishDb.Categories.Remove(existingFishCategory);
            fishDb.SaveChanges();
        }

        var fishCategory = new CatalogCategory { TemplateType = "Fish", RowType = "GcFishData", SourceMbinPath = fishSource };
        fishDb.Categories.Add(fishCategory);
        fishDb.SaveChanges();

        foreach (var row in fishRows) row.CategoryId = fishCategory.Id;
        fishDb.Items.AddRange(fishRows);
        fishDb.SaveChanges();

        ConsoleStyle.Success($"Wrote {fishRows.Count} fish to {fishDbPath}.");
        AddFishingRules(fishDb, fishingGlobals, baitTable);
        return;
    }

    /// <summary>Category "FishingRules" (#23): how the game picks a fish's size from the water's
    /// depth and what each bait favours, from the player's own game files. Rows:
    /// "DEPTH:&lt;size&gt;" (and "DEPTH:&lt;size&gt;:&lt;biome&gt;" for the planet types that override it -
    /// Waterworld, GasGiant) with JSON {"size","biome","base","mod","min","max"} - GcFishSizeProbability;
    /// "BAIT:&lt;product&gt;" with JSON {"id","rarity":[Junk..Legendary],"size":[S..XL],"day","night","storm"} -
    /// GcBaitData. Read 2026-10-22: Small 0-1 (base 50, depth barely matters), Medium 5-15, Large 30-65,
    /// ExtraLarge 45-90; water worlds 50-900; gas giants ignore depth. The game's own descriptions
    /// agree ("shallower water", "deep water", "the deepest of water").</summary>
    private static void AddFishingRules(CatalogDbContext db, libMBIN.NMS.Globals.GcFishingGlobals? globals, libMBIN.NMS.GameComponents.GcBaitTable? baits)
    {
        var existing = db.Categories.FirstOrDefault(c => c.TemplateType == "FishingRules");
        if (existing != null)
        {
            db.Items.RemoveRange(db.Items.Where(i => i.CategoryId == existing.Id));
            db.Categories.Remove(existing);
            db.SaveChanges();
        }
        if (globals is null && baits is null)
        {
            ConsoleStyle.Error("No fishing globals or bait table found - fishing rules left out.");
            return;
        }

        string[] sizes = { "Small", "Medium", "Large", "ExtraLarge" };
        var rows = new List<CatalogItem>();
        void Depth(libMBIN.NMS.GameComponents.GcFishSizeProbability[] weights, string? biome)
        {
            for (int i = 0; i < weights.Length && i < sizes.Length; i++)
            {
                var w = weights[i];
                rows.Add(new CatalogItem
                {
                    GameId = "DEPTH:" + sizes[i] + (biome is null ? "" : ":" + biome),
                    NameEnglish = sizes[i],
                    DescriptionEnglish = new System.Text.Json.Nodes.JsonObject
                    {
                        ["size"] = sizes[i], ["biome"] = biome ?? "",
                        ["base"] = w.BaseWeight, ["mod"] = w.DepthModifier, ["min"] = w.DepthRangeMin, ["max"] = w.DepthRangeMax,
                    }.ToJsonString(),
                });
            }
        }
        if (globals?.SizeWeights is { } general) Depth(general, null);
        foreach (var o in globals?.SizeWeightsBiomeOverrides ?? new())
            if (o.SizeWeights is { } w) Depth(w, o.Biome?.Biome.ToString());

        foreach (var bait in baits?.Bait ?? new())
        {
            string id = bait.ProductID?.Value ?? "";
            if (id.Length == 0) continue;
            rows.Add(new CatalogItem
            {
                GameId = "BAIT:" + id,
                NameEnglish = id,
                DescriptionEnglish = new System.Text.Json.Nodes.JsonObject
                {
                    ["id"] = id,
                    ["rarity"] = new System.Text.Json.Nodes.JsonArray((bait.RarityBoosts ?? Array.Empty<float>()).Select(v => (System.Text.Json.Nodes.JsonNode)(double)v).ToArray()),
                    ["size"] = new System.Text.Json.Nodes.JsonArray((bait.SizeBoosts ?? Array.Empty<float>()).Select(v => (System.Text.Json.Nodes.JsonNode)(double)v).ToArray()),
                    ["day"] = bait.DayTimeBoost, ["night"] = bait.NightTimeBoost, ["storm"] = bait.StormBoost,
                }.ToJsonString(),
            });
        }

        var category = new CatalogCategory { TemplateType = "FishingRules", RowType = "GcFishSizeProbability/GcBaitData", SourceMbinPath = "gcfishingglobals.global.mbin + baitdatatable.mbin" };
        db.Categories.Add(category);
        db.SaveChanges();
        foreach (var row in rows) row.CategoryId = category.Id;
        db.Items.AddRange(rows);
        db.SaveChanges();
        ConsoleStyle.Success($"Wrote {rows.Count} fishing rules (depth by size, baits).");
    }

    /// <summary>"DataCataloger add-item-values &lt;target-catalog-db-path&gt;" adds the base Units value and trade data of every product and substance (GcProductTable / GcRealitySubstanceTable: BaseValue, TradeCategory, Legality, Rarity) as category "ItemValues". GameId = "VALUE:" + item id (prefixed so it never collides with the item's own catalog row in id lookups), NameEnglish = readable name, UsageCategory = TradeCategory (Mineral, Tech, Commodity, Component, Alloy, Exotic, Energy, None, SpecialShop), DescriptionEnglish = JSON {"id","value","legality","rarity","kind"}. Added 2026-10-11 for the Item Values tool; idempotent, same patch-in-place pattern as add-recipes.</summary>
    public static void AddItemValues(string catalogPath, string pcbanksPath)
    {
        string valueDbPath = catalogPath;
        if (!File.Exists(valueDbPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {valueDbPath}");
            return;
        }


        IPakReaderService valuePakReader = new PakReaderService();
        var valueExtraction = new ExtractionService();
        libMBIN.NMS.GameComponents.GcProductTable? productTable = null;
        libMBIN.NMS.GameComponents.GcSubstanceTable? substanceTable = null;
        var valueLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            PakHeader header;
            IReadOnlyList<PakEntry> entries;
            try
            {
                header = valuePakReader.ReadHeader(pak.FullPath);
                entries = valuePakReader.Read(pak.FullPath);
            }
            catch { continue; }

            var wanted = entries.Where(e =>
                !string.IsNullOrEmpty(e.FileName) &&
                ((e.FileName.Contains("language/nms_", StringComparison.OrdinalIgnoreCase) &&
                  e.FileName.Contains("english", StringComparison.OrdinalIgnoreCase) &&
                  !e.FileName.Contains("usenglish", StringComparison.OrdinalIgnoreCase)) ||
                 e.FileName.EndsWith("nms_reality_gcproducttable.mbin", StringComparison.OrdinalIgnoreCase) ||
                 e.FileName.EndsWith("nms_reality_gcsubstancetable.mbin", StringComparison.OrdinalIgnoreCase)));

            foreach (var entry in wanted)
            {
                byte[]? bytes;
                try { bytes = valueExtraction.ExtractEntryBytes(pak.FullPath, entry, header); }
                catch { continue; }
                if (bytes is null || bytes.Length == 0) continue;

                NMSTemplate? template;
                try
                {
                    using var ms = new MemoryStream(bytes);
                    var mbin = new MBINFile(ms);
                    mbin.Load();
                    template = mbin.GetData();
                }
                catch { continue; }

                switch (template)
                {
                    case libMBIN.NMS.GameComponents.GcProductTable p: productTable ??= p; break;
                    case libMBIN.NMS.GameComponents.GcSubstanceTable t: substanceTable ??= t; break;
                    case not null:
                        foreach (var kv in LocalisationService.BuildEnglishLookup(template))
                            valueLookup.TryAdd(kv.Key, kv.Value);
                        break;
                }
            }
        }

        if (productTable?.Table is null || substanceTable?.Table is null)
        {
            ConsoleStyle.Error("Could not find/decode the product and substance tables.");
            return;
        }

        string Name(string? lowerKey, string? key)
        {
            if (lowerKey is { Length: > 0 } && valueLookup.TryGetValue(lowerKey, out var lower)) return lower;
            return key is { Length: > 0 } && valueLookup.TryGetValue(key, out var upper) ? upper : "";
        }

        var valueItems = new List<CatalogItem>();
        void Add(string id, string name, int value, string trade, string legality, string rarity, string kind)
        {
            if (id.Length == 0 || name.Length == 0) return;
            var payload = new System.Text.Json.Nodes.JsonObject
            {
                ["id"] = id, ["value"] = value, ["legality"] = legality, ["rarity"] = rarity, ["kind"] = kind,
            };
            valueItems.Add(new CatalogItem
            {
                GameId = "VALUE:" + id,
                NameEnglish = name,
                UsageCategory = trade,
                DescriptionEnglish = payload.ToJsonString(),
            });
        }

        foreach (var p in productTable.Table)
            Add(p.ID?.Value ?? "", Name(p.NameLower?.Value, p.Name?.Value), p.BaseValue,
                p.TradeCategory?.TradeCategory.ToString() ?? "None", p.Legality?.Legality.ToString() ?? "", p.Rarity?.Rarity.ToString() ?? "", "Product");
        foreach (var t in substanceTable.Table)
            Add(t.ID?.Value ?? "", Name(t.NameLower?.Value, t.Name?.Value), t.BaseValue,
                t.TradeCategory?.TradeCategory.ToString() ?? "None", t.Legality?.Legality.ToString() ?? "", t.Rarity?.Rarity.ToString() ?? "", "Substance");

        ConsoleStyle.Header($"Found {valueItems.Count} items with values.");
        foreach (var g in valueItems.GroupBy(i => i.UsageCategory))
            ConsoleStyle.Success($"  {g.Key}: {g.Count()}");

        using var valueDb = new CatalogDbContext(valueDbPath);
        var existingValueCategory = valueDb.Categories.FirstOrDefault(c => c.TemplateType == "ItemValues");
        if (existingValueCategory != null)
        {
            valueDb.Items.RemoveRange(valueDb.Items.Where(i => i.CategoryId == existingValueCategory.Id));
            valueDb.Categories.Remove(existingValueCategory);
            valueDb.SaveChanges();
        }

        var valueCategory = new CatalogCategory
        {
            TemplateType = "ItemValues",
            RowType = "BaseValue",
            SourceMbinPath = "metadata/reality/tables/nms_reality_gcproducttable.mbin + gcsubstancetable.mbin"
        };
        valueDb.Categories.Add(valueCategory);
        valueDb.SaveChanges();

        foreach (var item in valueItems) item.CategoryId = valueCategory.Id;
        valueDb.Items.AddRange(valueItems);
        valueDb.SaveChanges();

        ConsoleStyle.Success($"Wrote {valueItems.Count} item values to {valueDbPath}.");
        return;
    }

    /// <summary>"DataCataloger add-crafting &lt;target-catalog-db-path&gt;" adds what every craftable thing is made from, for the Crafting calculator: GcProductData.Requirements (what a product costs to craft) and GcTechnology.Requirements (what a technology costs to build/install), each a list of {ID, Amount, Type}. Category "Crafting"; GameId = "CRAFT:" + id (prefixed so it never collides with the item's own catalog row), NameEnglish = readable name, UsageCategory = "Product"/"Technology", DescriptionEnglish = JSON {"id","kind","craftable", "req":[[id,amount],...]}. Only rows with at least one requirement are kept; ids in "req" are products/substances (raw when they have no row here). Added 2026-10-11; idempotent, same patch-in-place pattern as add-item-values.</summary>
    public static void AddCrafting(string catalogPath, string pcbanksPath)
    {
        string craftDbPath = catalogPath;
        if (!File.Exists(craftDbPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {craftDbPath}");
            return;
        }


        IPakReaderService craftPakReader = new PakReaderService();
        var craftExtraction = new ExtractionService();
        libMBIN.NMS.GameComponents.GcProductTable? craftProducts = null;
        libMBIN.NMS.GameComponents.GcTechnologyTable? craftTech = null;
        var craftLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            PakHeader header;
            IReadOnlyList<PakEntry> entries;
            try
            {
                header = craftPakReader.ReadHeader(pak.FullPath);
                entries = craftPakReader.Read(pak.FullPath);
            }
            catch { continue; }

            var wanted = entries.Where(e =>
                !string.IsNullOrEmpty(e.FileName) &&
                ((e.FileName.Contains("language/nms_", StringComparison.OrdinalIgnoreCase) &&
                  e.FileName.Contains("english", StringComparison.OrdinalIgnoreCase) &&
                  !e.FileName.Contains("usenglish", StringComparison.OrdinalIgnoreCase)) ||
                 e.FileName.EndsWith("nms_reality_gcproducttable.mbin", StringComparison.OrdinalIgnoreCase) ||
                 e.FileName.EndsWith("nms_reality_gctechnologytable.mbin", StringComparison.OrdinalIgnoreCase)));

            foreach (var entry in wanted)
            {
                byte[]? bytes;
                try { bytes = craftExtraction.ExtractEntryBytes(pak.FullPath, entry, header); }
                catch { continue; }
                if (bytes is null || bytes.Length == 0) continue;

                NMSTemplate? template;
                try
                {
                    using var ms = new MemoryStream(bytes);
                    var mbin = new MBINFile(ms);
                    mbin.Load();
                    template = mbin.GetData();
                }
                catch { continue; }

                switch (template)
                {
                    case libMBIN.NMS.GameComponents.GcProductTable p: craftProducts ??= p; break;
                    case libMBIN.NMS.GameComponents.GcTechnologyTable t: craftTech ??= t; break;
                    case not null:
                        foreach (var kv in LocalisationService.BuildEnglishLookup(template))
                            craftLookup.TryAdd(kv.Key, kv.Value);
                        break;
                }
            }
        }

        if (craftProducts?.Table is null || craftTech?.Table is null)
        {
            ConsoleStyle.Error("Could not find/decode the product and technology tables.");
            return;
        }

        string CraftName(string? lowerKey, string? key)
        {
            if (lowerKey is { Length: > 0 } && craftLookup.TryGetValue(lowerKey, out var lower)) return lower;
            return key is { Length: > 0 } && craftLookup.TryGetValue(key, out var upper) ? upper : "";
        }

        var craftItems = new List<CatalogItem>();
        void AddCraft(string id, string name, string kind, bool craftable, List<libMBIN.NMS.GameComponents.GcTechnologyRequirement>? requirements)
        {
            var req = (requirements ?? new()).Where(r => !string.IsNullOrEmpty(r.ID?.Value) && r.Amount > 0).ToList();
            if (id.Length == 0 || req.Count == 0) return;
            var payload = new System.Text.Json.Nodes.JsonObject
            {
                ["id"] = id,
                ["kind"] = kind,
                ["craftable"] = craftable,
                ["req"] = new System.Text.Json.Nodes.JsonArray(req
                    .Select(r => (System.Text.Json.Nodes.JsonNode)new System.Text.Json.Nodes.JsonArray(r.ID.Value, r.Amount)).ToArray()),
            };
            craftItems.Add(new CatalogItem
            {
                GameId = "CRAFT:" + id,
                NameEnglish = name.Length > 0 ? name : id,
                UsageCategory = kind,
                DescriptionEnglish = payload.ToJsonString(),
            });
        }

        foreach (var p in craftProducts.Table)
            AddCraft(p.ID?.Value ?? "", CraftName(p.NameLower?.Value, p.Name?.Value), "Product", p.IsCraftable, p.Requirements);
        foreach (var t in craftTech.Table)
            AddCraft(t.ID?.Value ?? "", CraftName(t.NameLower?.Value, t.Name?.Value), "Technology", true, t.Requirements);

        ConsoleStyle.Header($"Found {craftItems.Count} craftable things ({craftItems.Count(i => i.UsageCategory == "Product")} products, {craftItems.Count(i => i.UsageCategory == "Technology")} technology).");
        foreach (var sample in craftItems.Where(i => i.GameId is "CRAFT:STASIS_DEVICE" or "CRAFT:HYPERDRIVE"))
            ConsoleStyle.Success($"  {sample.GameId} '{sample.NameEnglish}' {sample.DescriptionEnglish}");

        using var craftDb = new CatalogDbContext(craftDbPath);
        var existingCraftCategory = craftDb.Categories.FirstOrDefault(c => c.TemplateType == "Crafting");
        if (existingCraftCategory != null)
        {
            craftDb.Items.RemoveRange(craftDb.Items.Where(i => i.CategoryId == existingCraftCategory.Id));
            craftDb.Categories.Remove(existingCraftCategory);
            craftDb.SaveChanges();
        }

        var craftCategory = new CatalogCategory
        {
            TemplateType = "Crafting",
            RowType = "GcTechnologyRequirement",
            SourceMbinPath = "metadata/reality/tables/nms_reality_gcproducttable.mbin + gctechnologytable.mbin"
        };
        craftDb.Categories.Add(craftCategory);
        craftDb.SaveChanges();

        foreach (var item in craftItems) item.CategoryId = craftCategory.Id;
        craftDb.Items.AddRange(craftItems);
        craftDb.SaveChanges();

        ConsoleStyle.Success($"Wrote {craftItems.Count} crafting requirement rows to {craftDbPath}.");
        return;
    }

    /// <summary>"DataCataloger add-procedural-products &lt;target-catalog-db-path&gt;" adds the procedurally generated items (save ids like ^PROC_SEA#17140 - ocean salvage, fossils, plant samples, documents, freighter logs...) from metadata/reality/tables/proceduralproducttable.mbin (GcProceduralProductTable, one entry per GcProceduralProductCategory). The game builds each item's display name from random words picked by the #seed, which can't be reproduced here, so each row gets the product's own subtitle/category name and its icon instead. Category "ProceduralProducts"; GameId = the product id (the app already strips "#seed" when looking items up), NameEnglish = resolved name, icon from Product.Icon (else the first ProceduralData icon). Added 2026-10-11; idempotent.</summary>
    public static void AddProceduralProducts(string catalogPath, string pcbanksPath)
    {
        string procDbPath = catalogPath;
        if (!File.Exists(procDbPath))
        {
            ConsoleStyle.Error($"Target catalog DB not found: {procDbPath}");
            return;
        }


        IPakReaderService procPakReader = new PakReaderService();
        var procExtraction = new ExtractionService();
        var procIndex = new GlobalFileIndexService();
        libMBIN.NMS.GameComponents.GcProceduralProductTable? procTable = null;
        var procLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pak in new PakDiscoveryService().Discover(pcbanksPath))
        {
            PakHeader header;
            IReadOnlyList<PakEntry> entries;
            try
            {
                header = procPakReader.ReadHeader(pak.FullPath);
                entries = procPakReader.Read(pak.FullPath);
            }
            catch { continue; }
            procIndex.Add(pak.FullPath, header, entries);

            var wanted = entries.Where(e =>
                !string.IsNullOrEmpty(e.FileName) &&
                ((e.FileName.Contains("language/nms_", StringComparison.OrdinalIgnoreCase) &&
                  e.FileName.Contains("english", StringComparison.OrdinalIgnoreCase) &&
                  !e.FileName.Contains("usenglish", StringComparison.OrdinalIgnoreCase)) ||
                 e.FileName.EndsWith("proceduralproducttable.mbin", StringComparison.OrdinalIgnoreCase)));

            foreach (var entry in wanted)
            {
                byte[]? bytes;
                try { bytes = procExtraction.ExtractEntryBytes(pak.FullPath, entry, header); }
                catch { continue; }
                if (bytes is null || bytes.Length == 0) continue;

                NMSTemplate? template;
                try
                {
                    using var ms = new MemoryStream(bytes);
                    var mbin = new MBINFile(ms);
                    mbin.Load();
                    template = mbin.GetData();
                }
                catch { continue; }

                if (template is libMBIN.NMS.GameComponents.GcProceduralProductTable table) procTable ??= table;
                else if (template is not null)
                    foreach (var kv in LocalisationService.BuildEnglishLookup(template))
                        procLookup.TryAdd(kv.Key, kv.Value);
            }
        }

        if (procTable?.Table is null)
        {
            ConsoleStyle.Error("Could not find/decode proceduralproducttable.mbin.");
            return;
        }

        string ProcLoc(string? key) => key is { Length: > 0 } && procLookup.TryGetValue(key, out var text) ? text : "";
        var categories = Enum.GetNames(typeof(libMBIN.NMS.GameComponents.GcProceduralProductCategory.ProceduralProductCategoryEnum));
        var iconService = new IconExtractionService(procExtraction, procIndex, targetSize: 128);
        var procRows = new List<(string Id, string Name, string Category, string TexturePath, byte[]? Png)>();

        for (int i = 0; i < procTable.Table.Length; i++)
        {
            var entry = procTable.Table[i];
            var product = entry?.Product;
            if (product is null) continue;
            string category = i < categories.Length ? categories[i] : $"#{i}";
            string id = product.ID?.Value ?? "";
            string name = ProcLoc(product.NameLower?.Value);
            if (name.Length == 0) name = ProcLoc(product.Name?.Value);
            string subtitle = ProcLoc(product.Subtitle?.Value);
            string texture = product.Icon?.Filename?.Value ?? "";
            if (texture.Length == 0)
                texture = entry!.ProceduralData?.Select(d => d?.Icon?.Filename?.Value ?? "").FirstOrDefault(f => f.Length > 0) ?? "";

            ConsoleStyle.Success($"  [{i}] {category,-20} id='{id}' name='{name}' nameKey='{product.Name?.Value}' lowerKey='{product.NameLower?.Value}' subtitle='{subtitle}' (key '{product.Subtitle?.Value}') base='{entry!.NameGeneratorBase?.Word?.Value}' icon='{texture}'");
            if (id.Length == 0 || (name.Length == 0 && subtitle.Length == 0)) continue;

            // Names with %WORD% placeholders are filled in per item by the game.
            string display = name.Length > 0 && !name.Contains('%') ? name : subtitle.Length > 0 ? subtitle : category;
            byte[]? png = texture.Length > 0 ? iconService.ExtractAndConvert(texture) : null;
            procRows.Add((id, display, category, texture, png));
        }

        ConsoleStyle.Header($"Found {procRows.Count} procedural products ({procRows.Count(r => r.Png is not null)} with icons).");

        using var procDb = new CatalogDbContext(procDbPath);
        var existingProc = procDb.Categories.FirstOrDefault(c => c.TemplateType == "ProceduralProducts");
        if (existingProc is not null)
        {
            var itemIds = procDb.Items.Where(i => i.CategoryId == existingProc.Id).Select(i => i.Id).ToList();
            var oldIcons = procDb.Set<IconAsset>().Where(a => itemIds.Contains(a.ItemId)).ToList();
            var oldBlobIds = oldIcons.Select(a => a.IconBlobId).Distinct().ToList();
            procDb.RemoveRange(oldIcons);
            procDb.Items.RemoveRange(procDb.Items.Where(i => i.CategoryId == existingProc.Id));
            procDb.SaveChanges();
            // Icon images can be shared with other catalog rows; only drop ones nothing uses now.
            var stillUsed = procDb.Set<IconAsset>().Where(a => oldBlobIds.Contains(a.IconBlobId)).Select(a => a.IconBlobId).ToHashSet();
            procDb.RemoveRange(procDb.Set<IconBlob>().Where(b => oldBlobIds.Contains(b.Id) && !stillUsed.Contains(b.Id)));
            procDb.Categories.Remove(existingProc);
            procDb.SaveChanges();
        }

        var procCategory = new CatalogCategory
        {
            TemplateType = "ProceduralProducts",
            RowType = "GcProceduralProductData",
            SourceMbinPath = "metadata/reality/tables/proceduralproducttable.mbin"
        };
        // One IconBlob per texture path (unique in the DB): reuse an existing one -
        // e.g. the fossil texture is shared by two categories and may already be in
        // the catalog from the product table.
        var blobs = new Dictionary<string, IconBlob>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in procRows)
        {
            var item = new CatalogItem { GameId = row.Id, NameEnglish = row.Name, UsageCategory = row.Category };
            if (row.Png is not null)
            {
                if (!blobs.TryGetValue(row.TexturePath, out var blob))
                {
                    blob = procDb.Set<IconBlob>().FirstOrDefault(b => b.SourceDdsPath == row.TexturePath)
                        ?? new IconBlob { SourceDdsPath = row.TexturePath, PngData = row.Png };
                    blobs[row.TexturePath] = blob;
                }
                item.Icons.Add(new IconAsset { SourceField = "Icon", IconBlob = blob });
            }
            procCategory.Items.Add(item);
        }
        procDb.Categories.Add(procCategory);
        procDb.SaveChanges();

        ConsoleStyle.Success($"Wrote {procRows.Count} procedural products to {procDbPath}.");
        return;
    }
}
