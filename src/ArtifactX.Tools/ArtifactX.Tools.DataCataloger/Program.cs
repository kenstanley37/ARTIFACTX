using ArtifactX.GameData;
using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;
using ArtifactX.GameData.Services.Interfaces;
using System.Reflection;
using System.Text.RegularExpressions;
using libMBIN;

string workingFolder = Path.Combine(AppContext.BaseDirectory, "Working");
Directory.CreateDirectory(workingFolder);

// "DataCataloger build-all <output.sqlite> [game folder]" - the whole build the
// app runs on the player's PC (GameDataBuilder): main build, every add-* step,
// trim, game-build stamp. Game folder defaults to config.json, else the first
// install found.
if (args.Length > 1 && args[0].Equals("build-all", StringComparison.OrdinalIgnoreCase))
{
    var game = args.Length > 2 ? GameInstall.Inspect(args[2])
        : (DevGamePath() is string configured ? GameInstall.Inspect(configured) : null) ?? GameInstall.Find();
    if (game is null) { ConsoleStyle.Error("No No Man's Sky install found."); return; }
    ConsoleStyle.Header($"Building from {game.RootFolder} ({game.BuildLabel}, fingerprint {game.Fingerprint})");
    GameDataBuilder.Build(game, Path.GetFullPath(args[1]), Path.Combine(workingFolder, "build-all"),
        new Progress<GameDataProgress>(p => ConsoleStyle.Success($"[{p.Step}/{p.Steps}] {p.Label}")));
    var info = GameDataInfo.Read(Path.GetFullPath(args[1]));
    ConsoleStyle.Success($"Done: format {info?.Format}, fingerprint {info?.Fingerprint}, matches install: {info?.Matches(game)}");
    return;
}

// "DataCataloger trim" reuses an already-built working catalog and writes
// the small, ship-ready distribution copy - no PAK re-extraction needed.
if (args.Length > 0 && args[0].Equals("trim", StringComparison.OrdinalIgnoreCase))
{
    string sourcePath = Path.Combine(workingFolder, "nms_catalog.sqlite");
    string distPath = Path.Combine(workingFolder, "nms_catalog_dist.sqlite");

    ConsoleStyle.Header("Trimming working catalog to a distribution-ready copy...");
    CatalogTrimService.Trim(sourcePath, distPath);
    ConsoleStyle.Success($"Done. Copy {Path.GetFileName(distPath)} into the app's Data folder as nms_catalog.sqlite.");
    return;
}

// "DataCataloger multitool" lists every multi-tool .SCENE.MBIN model path found
// across all PAKs. Reads manifests only (no MBIN decoding), so it finishes in
// seconds - a quick way to confirm which model paths actually exist before
// wiring any of them into the editor's Type selector.
if (args.Length > 0 && args[0].Equals("multitool", StringComparison.OrdinalIgnoreCase))
{
    var multiToolSettings = SettingsService.Load();
    if (!SettingsService.IsValid(multiToolSettings))
    {
        ConsoleStyle.Error("No valid ArtifactX installation path configured yet - run the normal build once first.");
        return;
    }

    IPakDiscoveryService mtPakDiscovery = new PakDiscoveryService();
    IPakReaderService mtPakReader = new PakReaderService();
    var mtPaks = mtPakDiscovery.Discover(multiToolSettings.NmsInstallationPath!);

    var matches = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var pak in mtPaks)
    {
        IReadOnlyList<ArtifactX.GameData.Models.PakEntry> entries;
        try
        {
            entries = mtPakReader.Read(pak.FullPath);
        }
        catch
        {
            continue;
        }

        foreach (var entry in entries)
        {
            if (!string.IsNullOrEmpty(entry.FileName) &&
                entry.FileName.Contains("MULTITOOL", StringComparison.OrdinalIgnoreCase) &&
                entry.FileName.EndsWith(".SCENE.MBIN", StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(entry.FileName);
            }
        }
    }

    ConsoleStyle.Header($"Found {matches.Count} multi-tool .SCENE.MBIN files:");
    foreach (var path in matches)
        ConsoleStyle.Success("  " + path);

    return;
}

// "DataCataloger listpak <pak filename substring>" - lists every entry
// filename in the first matching PAK, no decoding. For finding the exact
// filename of a globals file when a substring guess (e.g. "spaceshipglobals")
// ambiguously matches multiple real files (e.g. "aispaceshipglobals" too).
if (args.Length > 1 && args[0].Equals("listpak", StringComparison.OrdinalIgnoreCase))
{
    string pakSubstring = args[1];
    var listPakSettings = SettingsService.Load();
    if (!SettingsService.IsValid(listPakSettings))
    {
        ConsoleStyle.Error("No valid ArtifactX installation path configured yet - run the normal build once first.");
        return;
    }

    IPakDiscoveryService listPakDiscovery = new PakDiscoveryService();
    IPakReaderService listPakReader = new PakReaderService();
    var listPaks = listPakDiscovery.Discover(listPakSettings.NmsInstallationPath!)
        .Where(p => p.FileName.Contains(pakSubstring, StringComparison.OrdinalIgnoreCase))
        .ToList();

    foreach (var pak in listPaks)
    {
        IReadOnlyList<ArtifactX.GameData.Models.PakEntry> entries;
        try { entries = listPakReader.Read(pak.FullPath); }
        catch { continue; }

        ConsoleStyle.Header($"{pak.FileName} ({entries.Count} entries):");
        foreach (var entry in entries.OrderBy(e => e.FileName, StringComparer.OrdinalIgnoreCase))
            ConsoleStyle.Success("  " + entry.FileName);
    }
    return;
}

// "DataCataloger ships" - same manifest-only listing technique as "multitool"
// above, scoped to MODELS/COMMON/SPACECRAFT instead. Used to independently
// discover the real set of starship .SCENE.MBIN files (base hull models, not
// sub-parts) for Squadron's Ship Resource picker - not copied from any
// external reference, same reasoning as MultiToolTypes.cs.
if (args.Length > 0 && args[0].Equals("ships", StringComparison.OrdinalIgnoreCase))
{
    var shipSettings = SettingsService.Load();
    if (!SettingsService.IsValid(shipSettings))
    {
        ConsoleStyle.Error("No valid ArtifactX installation path configured yet - run the normal build once first.");
        return;
    }

    IPakDiscoveryService shipPakDiscovery = new PakDiscoveryService();
    IPakReaderService shipPakReader = new PakReaderService();
    var shipPaks = shipPakDiscovery.Discover(shipSettings.NmsInstallationPath!);

    var shipMatches = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var pak in shipPaks)
    {
        IReadOnlyList<ArtifactX.GameData.Models.PakEntry> entries;
        try
        {
            entries = shipPakReader.Read(pak.FullPath);
        }
        catch
        {
            continue;
        }

        foreach (var entry in entries)
        {
            if (!string.IsNullOrEmpty(entry.FileName) &&
                entry.FileName.Contains("SPACECRAFT", StringComparison.OrdinalIgnoreCase) &&
                entry.FileName.EndsWith(".SCENE.MBIN", StringComparison.OrdinalIgnoreCase))
            {
                shipMatches.Add(entry.FileName);
            }
        }
    }

    ConsoleStyle.Header($"Found {shipMatches.Count} spacecraft .SCENE.MBIN files:");
    foreach (var path in shipMatches)
        ConsoleStyle.Success("  " + path);

    return;
}

// "DataCataloger extract <filename substring> <output folder>" - writes the raw bytes of every MBIN whose path
// contains the substring (e.g. "PLANETBUILDINGTABLE") from the data PAKs, for research tools that decode them with
// libMBIN. The output is extracted game data: keep it local, never commit it.
if (args.Length > 2 && args[0].Equals("extract", StringComparison.OrdinalIgnoreCase))
{
    var extractSettings = SettingsService.Load();
    if (!SettingsService.IsValid(extractSettings))
    {
        ConsoleStyle.Error("No valid ArtifactX installation path configured yet - run the normal build once first.");
        return;
    }
    IPakReaderService extractPakReader = new PakReaderService();
    var extractor = new ExtractionService();
    Directory.CreateDirectory(args[2]);
    int written = 0;
    foreach (var pak in new PakDiscoveryService().Discover(extractSettings.NmsInstallationPath!))
    {
        ArtifactX.GameData.Models.PakHeader header;
        IReadOnlyList<ArtifactX.GameData.Models.PakEntry> entries;
        try { header = extractPakReader.ReadHeader(pak.FullPath); entries = extractPakReader.Read(pak.FullPath); }
        catch { continue; }
        foreach (var entry in entries.Where(e => e.FileName?.Contains(args[1], StringComparison.OrdinalIgnoreCase) == true
                     && e.FileName.EndsWith(".MBIN", StringComparison.OrdinalIgnoreCase)))
        {
            byte[]? bytes = extractor.ExtractEntryBytes(pak.FullPath, entry, header);
            if (bytes is null) continue;
            string target = Path.Combine(args[2], Path.GetFileName(entry.FileName));
            File.WriteAllBytes(target, bytes);
            ConsoleStyle.Success($"{entry.FileName} -> {target} ({bytes.Length} bytes)");
            written++;
        }
    }
    ConsoleStyle.Success($"{written} file(s) written.");
    return;
}

// "DataCataloger grep <substring>" decodes every MBIN and deep-searches its actual
// FIELD VALUES (not filenames) for the given substring, regardless of whether the
// file is table-shaped or would ever be classified by CatalogClassifier. This finds
// mapping tables `inspect` structurally can't see - e.g. if a row type's ID field
// isn't literally named "ID"/"Id", or the table shape doesn't match List<T> at all,
// `inspect` never picks it up as a Category in the first place. Deliberately scoped
// to PAKs whose name suggests game-logic/data content (Metadata/globals) rather than
// every PAK - the mesh/texture/audio/shader PAKs make up the vast majority of files
// in a full install and can't contain this kind of string reference, so scanning
// them would just be slow for no benefit. Widen the PAK name filter below if this
// scope turns up nothing.
if (args.Length > 1 && args[0].Equals("grep", StringComparison.OrdinalIgnoreCase))
{
    string needle = args[1];
    var grepSettings = SettingsService.Load();
    if (!SettingsService.IsValid(grepSettings))
    {
        ConsoleStyle.Error("No valid ArtifactX installation path configured yet - run the normal build once first.");
        return;
    }

    IPakDiscoveryService grepPakDiscovery = new PakDiscoveryService();
    IPakReaderService grepPakReader = new PakReaderService();
    var grepExtraction = new ExtractionService();
    var grepPaks = grepPakDiscovery.Discover(grepSettings.NmsInstallationPath!)
        .Where(p => p.FileName.Contains("Metadata", StringComparison.OrdinalIgnoreCase) ||
                    p.FileName.Contains("globals", StringComparison.OrdinalIgnoreCase))
        .ToList();

    ConsoleStyle.Header($"Scanning {grepPaks.Count} likely data PAKs for '{needle}' in MBIN field values...");

    int filesScanned = 0, matchesFound = 0;

    foreach (var pak in grepPaks)
    {
        ArtifactX.GameData.Models.PakHeader header;
        IReadOnlyList<ArtifactX.GameData.Models.PakEntry> entries;
        try
        {
            header = grepPakReader.ReadHeader(pak.FullPath);
            entries = grepPakReader.Read(pak.FullPath);
        }
        catch
        {
            continue;
        }

        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.FileName) ||
                !entry.FileName.EndsWith(".MBIN", StringComparison.OrdinalIgnoreCase))
                continue;

            filesScanned++;

            byte[]? bytes;
            try
            {
                bytes = grepExtraction.ExtractEntryBytes(pak.FullPath, entry, header);
            }
            catch
            {
                continue;
            }
            if (bytes == null || bytes.Length == 0) continue;

            NMSTemplate? template;
            try
            {
                using var ms = new MemoryStream(bytes);
                var mbin = new MBINFile(ms);
                mbin.Load();
                template = mbin.GetData();
            }
            catch
            {
                continue;
            }
            if (template == null) continue;

            if (ContainsStringDeep(template, needle, depth: 6, visited: new HashSet<object>()))
            {
                matchesFound++;
                ConsoleStyle.Success($"MATCH: {entry.FileName}");
            }
        }
    }

    ConsoleStyle.Header($"Scanned {filesScanned} MBIN files across {grepPaks.Count} PAKs, {matchesFound} matches for '{needle}'.");
    return;
}

// "DataCataloger locid <id substring>" decodes language/nms_loc1_english.mbin
// (the English loc table `grep` found matching alien-word ids in) via the same
// LocalisationService.BuildEnglishLookup used for regular item names, then
// prints every entry whose key contains the given substring - e.g. "locid TRA_"
// dumps every Gek word's id -> English text pair in one call.
if (args.Length > 1 && args[0].Equals("locid", StringComparison.OrdinalIgnoreCase))
{
    string idSubstring = args[1];
    // Optional 3rd arg picks a different loc table than the default loc1 -
    // some content (e.g. later-expansion vehicle titles like Minotaur) lives
    // in loc7/loc8/etc instead. Pass a substring like "loc8_english".
    string locFileSubstring = args.Length > 2 ? args[2] : "language/nms_loc1_english.mbin";
    var locSettings = SettingsService.Load();
    if (!SettingsService.IsValid(locSettings))
    {
        ConsoleStyle.Error("No valid ArtifactX installation path configured yet - run the normal build once first.");
        return;
    }

    IPakDiscoveryService locPakDiscovery = new PakDiscoveryService();
    IPakReaderService locPakReader = new PakReaderService();
    var locExtraction = new ExtractionService();
    var locPaks = locPakDiscovery.Discover(locSettings.NmsInstallationPath!);

    bool locFound = false;
    foreach (var pak in locPaks)
    {
        ArtifactX.GameData.Models.PakHeader header;
        IReadOnlyList<ArtifactX.GameData.Models.PakEntry> entries;
        try
        {
            header = locPakReader.ReadHeader(pak.FullPath);
            entries = locPakReader.Read(pak.FullPath);
        }
        catch { continue; }

        var match = entries.FirstOrDefault(e =>
            !string.IsNullOrEmpty(e.FileName) &&
            e.FileName.Contains(locFileSubstring, StringComparison.OrdinalIgnoreCase));
        if (match is null) continue;

        locFound = true;
        byte[]? bytes;
        try { bytes = locExtraction.ExtractEntryBytes(pak.FullPath, match, header); }
        catch (Exception ex) { ConsoleStyle.Error($"Extraction failed: {ex.Message}"); return; }
        if (bytes is null || bytes.Length == 0) { ConsoleStyle.Error("Extraction returned no bytes."); return; }

        NMSTemplate? template;
        try
        {
            using var ms = new MemoryStream(bytes);
            var mbin = new MBINFile(ms);
            mbin.Load();
            template = mbin.GetData();
        }
        catch (Exception ex) { ConsoleStyle.Error($"Decode failed: {ex.Message}"); return; }
        if (template is null) { ConsoleStyle.Error("Decoded template is null."); return; }

        var lookup = LocalisationService.BuildEnglishLookup(template);
        ConsoleStyle.Header($"Loc table has {lookup.Count} total entries. Matching '{idSubstring}':");

        var matches = lookup.Where(kv => kv.Key.Contains(idSubstring, StringComparison.OrdinalIgnoreCase))
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var kv in matches)
            ConsoleStyle.Success($"  {kv.Key} = \"{kv.Value}\"");

        ConsoleStyle.Header($"{matches.Count} matching entries.");
        return;
    }

    if (!locFound)
        ConsoleStyle.Error("Could not find language/nms_loc1_english.mbin in any PAK.");
    return;
}

// "DataCataloger locval <text substring> [loc file substring]" - same as
// locid but searches by the English VALUE instead of the id, for when a
// display string is known (e.g. from a screenshot) but its internal id
// isn't. Companion to `grep`, which finds WHICH loc file contains a string
// but not its id - this is the follow-up step.
if (args.Length > 1 && args[0].Equals("locval", StringComparison.OrdinalIgnoreCase))
{
    string valueSubstring = args[1];
    string locFileSubstring2 = args.Length > 2 ? args[2] : "language/nms_loc1_english.mbin";
    var locValSettings = SettingsService.Load();
    if (!SettingsService.IsValid(locValSettings))
    {
        ConsoleStyle.Error("No valid ArtifactX installation path configured yet - run the normal build once first.");
        return;
    }

    IPakDiscoveryService locValPakDiscovery = new PakDiscoveryService();
    IPakReaderService locValPakReader = new PakReaderService();
    var locValExtraction = new ExtractionService();
    var locValPaks = locValPakDiscovery.Discover(locValSettings.NmsInstallationPath!);

    bool locValFound = false;
    foreach (var pak in locValPaks)
    {
        ArtifactX.GameData.Models.PakHeader header;
        IReadOnlyList<ArtifactX.GameData.Models.PakEntry> entries;
        try
        {
            header = locValPakReader.ReadHeader(pak.FullPath);
            entries = locValPakReader.Read(pak.FullPath);
        }
        catch { continue; }

        var match = entries.FirstOrDefault(e =>
            !string.IsNullOrEmpty(e.FileName) &&
            e.FileName.Contains(locFileSubstring2, StringComparison.OrdinalIgnoreCase));
        if (match is null) continue;

        locValFound = true;
        byte[]? bytes;
        try { bytes = locValExtraction.ExtractEntryBytes(pak.FullPath, match, header); }
        catch (Exception ex) { ConsoleStyle.Error($"Extraction failed: {ex.Message}"); return; }
        if (bytes is null || bytes.Length == 0) { ConsoleStyle.Error("Extraction returned no bytes."); return; }

        NMSTemplate? template;
        try
        {
            using var ms = new MemoryStream(bytes);
            var mbin = new MBINFile(ms);
            mbin.Load();
            template = mbin.GetData();
        }
        catch (Exception ex) { ConsoleStyle.Error($"Decode failed: {ex.Message}"); return; }
        if (template is null) { ConsoleStyle.Error("Decoded template is null."); return; }

        var lookup = LocalisationService.BuildEnglishLookup(template);
        ConsoleStyle.Header($"Loc table has {lookup.Count} total entries. Matching value '{valueSubstring}':");

        var matches = lookup.Where(kv => kv.Value.Contains(valueSubstring, StringComparison.OrdinalIgnoreCase))
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var kv in matches)
            ConsoleStyle.Success($"  {kv.Key} = \"{kv.Value}\"");

        ConsoleStyle.Header($"{matches.Count} matching entries.");
        return;
    }

    if (!locValFound)
        ConsoleStyle.Error($"Could not find {locFileSubstring2} in any PAK.");
    return;
}

// "DataCataloger locidall <id substring>" - like locid, but scans EVERY
// english loc file (loc1/4/5/6/7/8/9/update3/etc, not just one) in a single
// pass. Ship/vehicle reward names turned out to be scattered across
// different loc files per-expedition (loc6 had ship 01, others are
// elsewhere) - this avoids guessing which numbered loc file to check one at
// a time.
if (args.Length > 1 && args[0].Equals("locidall", StringComparison.OrdinalIgnoreCase))
{
    string idSubstringAll = args[1];
    var locAllSettings = SettingsService.Load();
    if (!SettingsService.IsValid(locAllSettings))
    {
        ConsoleStyle.Error("No valid ArtifactX installation path configured yet - run the normal build once first.");
        return;
    }

    IPakDiscoveryService locAllPakDiscovery = new PakDiscoveryService();
    IPakReaderService locAllPakReader = new PakReaderService();
    var locAllExtraction = new ExtractionService();
    var locAllPaks = locAllPakDiscovery.Discover(locAllSettings.NmsInstallationPath!);

    int totalMatches = 0;

    foreach (var pak in locAllPaks)
    {
        ArtifactX.GameData.Models.PakHeader header;
        IReadOnlyList<ArtifactX.GameData.Models.PakEntry> entries;
        try
        {
            header = locAllPakReader.ReadHeader(pak.FullPath);
            entries = locAllPakReader.Read(pak.FullPath);
        }
        catch { continue; }

        var locEntries = entries.Where(e =>
            !string.IsNullOrEmpty(e.FileName) &&
            e.FileName.Contains("language/nms_", StringComparison.OrdinalIgnoreCase) &&
            e.FileName.Contains("english", StringComparison.OrdinalIgnoreCase) &&
            !e.FileName.Contains("usenglish", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var entry in locEntries)
        {
            byte[]? bytes;
            try { bytes = locAllExtraction.ExtractEntryBytes(pak.FullPath, entry, header); }
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
            if (template is null) continue;

            var lookup = LocalisationService.BuildEnglishLookup(template);
            var matches = lookup.Where(kv => kv.Key.Contains(idSubstringAll, StringComparison.OrdinalIgnoreCase))
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (matches.Count == 0) continue;

            ConsoleStyle.Header($"{entry.FileName} ({matches.Count} matches):");
            foreach (var kv in matches)
                ConsoleStyle.Success($"  {kv.Key} = \"{kv.Value}\"");
            totalMatches += matches.Count;
        }
    }

    ConsoleStyle.Header($"{totalMatches} total matching entries across all loc files.");
    return;
}

// "DataCataloger add-language-words <target-catalog-db-path>" extracts every
// alien-language vocabulary word (the save-editable subset found at
// vLc.6f=.MF2 - see ArtifactX's project_language_words memory) from
// language/nms_loc1_english.mbin and writes them into an existing catalog DB
// as a new "GcAlienLanguageWords" category, UsageCategory = race name.
//
// The loc table's TRA_/WAR_/EXP_/BUI_ prefixes aren't exclusively vocabulary
// words - the SAME prefixes are reused for lore paragraphs and other UI text
// (e.g. "EXP_1_PLAQUE_LORE_1" is a multi-sentence Korvax lore entry, not a
// word). Confirmed by direct inspection that every REAL word id follows a
// strict two-part shape - PREFIX_WORD, where WORD is letters/apostrophe only,
// no digits, no extra underscores - and that shape alone (not the text value)
// cleanly separates real words from lore noise with zero false positives
// spot-checked. Idempotent: re-running replaces any previously-inserted
// GcAlienLanguageWords category rather than duplicating them.
if (args.Length > 1 && args[0].Equals("add-language-words", StringComparison.OrdinalIgnoreCase))
{
    if (DevGamePath() is string gamePath) CatalogSteps.AddLanguageWords(args[1], gamePath);
    return;
}

// "DataCataloger add-broken-tech-flag <target-catalog-db-path>" marks every
// GcTechnology row whose game-data "BrokenSlotTech" flag is set (the damaged-
// slot placeholders - Hull Fracture, Short Circuit, etc. - drawn red in-game)
// in an EXISTING catalog DB, adding the Items.IsBrokenSlotTech column first if
// the DB predates it. Same "patch the shipped DB in place" pattern as
// add-language-words/add-guide-topics, so their already-merged rows survive
// without a full rebuild; fresh full builds get the flag via CatalogClassifier
// directly. Idempotent - safe to re-run after a game update.
//
// Found 2026-10-10 investigating a user save whose Install Technology menu was
// cluttered with useless red entries: a "Mark All Known" bulk edit had taught
// it all 36 BrokenSlotTech rows, which a healthy save never knows.
if (args.Length > 1 && args[0].Equals("add-broken-tech-flag", StringComparison.OrdinalIgnoreCase))
{
    if (DevGamePath() is string gamePath) CatalogSteps.AddBrokenTechFlag(args[1], gamePath);
    return;
}

// "DataCataloger add-station-assets <target-catalog-db-path>" adds the portal
// glyph images the app's Space Stations page uses:
//   - "PortalGlyphs": 16 items PORTALSYMBOL_0..PORTALSYMBOL_F, each with the
//     game's own glyph texture (textures/ui/frontend/icons/update3/
//     portalsymbol.<hex>.dds) as its icon - a portal address is 12 hex digits,
//     one glyph per digit.
// Galaxy names are NOT here: the game's text only names galaxies 1-10, so the
// app uses ArtifactX.Core's hand-recorded NmsGalaxyNames (all 256) instead.
// Re-running also removes the "GalaxyNames" category an earlier version wrote.
// Same patch-in-place pattern as add-guide-topics; idempotent.
if (args.Length > 1 && args[0].Equals("add-station-assets", StringComparison.OrdinalIgnoreCase))
{
    if (DevGamePath() is string gamePath) CatalogSteps.AddStationAssets(args[1], gamePath);
    return;
}

// "DataCataloger add-guide-topics <target-catalog-db-path>" extracts the
// in-game Guide screen's real topic-to-category grouping from
// metadata/reality/wiki.mbin (a GcWiki: Categories[].Topics[], found
// 2026-08-12 via `grep` on a known topic id, then `dumpfile` with bumped
// depth/listCap since the generic dumper's 4/5 defaults were too shallow
// to show more than placeholder "[0] [1] ..." for this shape - see
// DumpStructure's listCap parameter). Confirmed correct: this dump's
// output exactly matched a reference tool's own Guide tab across all 8
// categories/57 categorized topics, screenshot-verified. Writes into the
// existing catalog DB as a new "GcWiki" category, same pattern as
// add-language-words - GameId=TopicID, NameEnglish=resolved topic name
// (merged across EVERY english loc file, not just one - an initial loc4-
// only version left 10 of the 59 ids showing as raw "UI_GUIDE_TOPIC_*"
// text in the UI, since some topics' strings live in other numbered loc
// files instead; same scattered-across-files behavior locidall's own doc
// comment already describes for ship/vehicle reward names, fixed the same
// way here), UsageCategory=resolved category name (e.g. "Survival
// Basics"). ArtifactX's own GuidePage reads
// this directly rather than a hardcoded Almanac list, so a future game
// update's new/reordered topics get picked up automatically next time
// this command is re-run - no code change needed on the app side.
//
// 2 of the 59 known UI_GUIDE_TOPIC_* loc ids (EXPLORATION_2 "Finding
// Resources", TRADE_3A "Trading Basics") genuinely aren't in ANY
// category's Topics list in this data - not a bug, confirmed by direct
// inspection of the raw dump. Written with UsageCategory="Uncategorized"
// rather than dropped, since they're still real, independently toggleable
// ids in accountdata.hg (confirmed via the same real controlled test that
// found this whole feature).
if (args.Length > 1 && args[0].Equals("add-guide-topics", StringComparison.OrdinalIgnoreCase))
{
    if (DevGamePath() is string gamePath) CatalogSteps.AddGuideTopics(args[1], gamePath);
    return;
}

// "DataCataloger add-freighter-paints <target-catalog-db-path>" adds the
// freighter Recolouring blueprints (Freighter Upgrades page) from the game's
// own metadata/gamestate/playerdata/customisationcolourpalettes.mbin: the
// palette with ID "FREIGHTER" lists, per colour, ExtraData.ProductToUnlock
// (the blueprint id stored in the save) and TipText (its loc name). Entries
// 0-9 are the 10 base colours; 10-19 are dark variants unlocked by the SAME
// 10 products, so only the first occurrence of each product is kept.
// Written as category "FreighterPaints": GameId = product id, NameEnglish =
// resolved TipText, DescriptionEnglish = the colour as "#RRGGBB" (drawn as
// the tile's swatch - these blueprints have no icon of their own).
// Added 2026-10-10 replacing a hand-typed Almanac list that had the cyan
// paint as FREIGHT_TURQUOISE; the real id (confirmed in a user save) is
// FREIGHT_CYAN. Same patch-in-place pattern as add-guide-topics; idempotent.
if (args.Length > 1 && args[0].Equals("add-freighter-paints", StringComparison.OrdinalIgnoreCase))
{
    if (DevGamePath() is string gamePath) CatalogSteps.AddFreighterPaints(args[1], gamePath);
    return;
}

// "DataCataloger add-corvette-parts <target-catalog-db-path>" marks which
// base-building parts can be built on a corvette, from the game's own base
// building table (GcBaseBuildingTable.Objects): BuildableInShipStructural
// (cockpits, habitats, landing gear, wings...) and BuildableInShipDecorative.
// The catalog's own BuildingPart items mix every base part (2,090) with no
// way to tell corvette ones apart, so the Corvette Cache's item list needs
// this (added 2026-10-12). Written as category "CorvetteParts": GameId = the
// inventory item id (OverrideProductID when the part uses one), NameEnglish =
// the part id, UsageCategory = Structural / Decorative / Both. The app joins
// it back to the product rows for names, icons and stack sizes. Same
// patch-in-place pattern as add-freighter-paints; idempotent.
if (args.Length > 1 && args[0].Equals("add-corvette-parts", StringComparison.OrdinalIgnoreCase))
{
    if (DevGamePath() is string gamePath) CatalogSteps.AddCorvetteParts(args[1], gamePath);
    return;
}

// "DataCataloger add-tech-stats <target-catalog-db-path>" records the main
// stat each technology works on (GcTechnology.BaseStat - Ship_Hyperdrive,
// Ship_PulseDrive, Ship_Weapons_Lasers...), so ArtifactX can keep a device's
// related tech together (user request 2026-10-13: "all the drives should be
// together"). The game's own Group/ParentTechId fields are empty for ship
// tech; BaseStat groups them cleanly. Upgrade modules (procedural tech) get
// the stat of the template tech they're rolled from. Written as category
// "TechStats": GameId = "TECHSTAT:" + tech id, NameEnglish = the stat,
// UsageCategory = "Primary" for a device's main item (PrimaryItem), else "",
// DescriptionEnglish = its LinkColour (#RRGGBB, the adjacency border).
// Same patch-in-place pattern as add-corvette-parts; idempotent.
if (args.Length > 1 && args[0].Equals("add-tech-stats", StringComparison.OrdinalIgnoreCase))
{
    if (DevGamePath() is string gamePath) CatalogSteps.AddTechStats(args[1], gamePath);
    return;
}

// "DataCataloger add-system-descriptors <target-catalog-db-path>" adds the
// words the game uses to describe a star system's economy, wealth and
// conflict (system info panel / Galaxy Map), e.g. "Mathematical", "Opulent",
// "Formidable". Each is a loc string named for its category:
//   UI_ECON_CLASS_<TRADE|MINING|MANUFACT|TECH|SCIENCE|POWER|ALLOY>_<n>
//   UI_ECON_LEVEL_<LOW|MED|HIGH>_<n>        (wealth)
//   UI_CONFLICT_LEVEL_<LOW|MED|HIGH>_<n>
// found 2026-10-11 via `locval` on words from a user screenshot (they live in
// nms_update3_english). Written as category "SystemDescriptors": GameId = loc
// id, NameEnglish = the word, UsageCategory = Economy/Wealth/Conflict,
// DescriptionEnglish = the tier (TRADE, HIGH...). ArtifactX's Planets page uses
// these so players can pick the exact word the game shows them. Same
// patch-in-place pattern as add-guide-topics; idempotent.
if (args.Length > 1 && args[0].Equals("add-system-descriptors", StringComparison.OrdinalIgnoreCase))
{
    if (DevGamePath() is string gamePath) CatalogSteps.AddSystemDescriptors(args[1], gamePath);
    return;
}

// "DataCataloger add-planet-descriptors <target-catalog-db-path>" adds the
// words the game's discovery screen uses to describe a planet, which the save
// doesn't store: weather (WEATHER_<biome><n>, e.g. WEATHER_LUSHEXTREME6
// "Painfully Hot Rain", in nms_update3_english), sentinels (SENTINEL_<level><n>,
// e.g. SENTINEL_HIGH2 "Frenzied") and flora/fauna (RARITY_<level><n>, e.g.
// RARITY_HIGH6 "Generous"). Found 2026-10-13 via `locval` on words from a
// user screenshot. Category "PlanetDescriptors": GameId = loc id, NameEnglish
// = the word, UsageCategory = Weather/Sentinels/Life, DescriptionEnglish = the
// tier (biome or level). Same pattern as add-system-descriptors; idempotent.
if (args.Length > 1 && args[0].Equals("add-planet-descriptors", StringComparison.OrdinalIgnoreCase))
{
    if (DevGamePath() is string gamePath) CatalogSteps.AddPlanetDescriptors(args[1], gamePath);
    return;
}

// "DataCataloger add-recipes <target-catalog-db-path>" adds every refiner and
// cooking recipe from metadata/reality/tables/nms_reality_gcrecipetable.mbin
// (GcRecipeTable: Id, RecipeName/RecipeType loc keys, Result + Ingredients
// [{Id, Amount, Type}], TimeToMake, Cooking) as category "Recipes". Stored as
// ordinary catalog items so they survive `trim` with no schema change:
// GameId = recipe id, NameEnglish = resolved recipe name, UsageCategory =
// "Refiner"/"Cooking", DescriptionEnglish = compact JSON
// {"type","result":[id,amount],"ingredients":[[id,amount],...],"time"} - ids
// are item GameIds (products/substances), resolved to names/icons by the app
// through the normal catalog lookup. Added 2026-10-11 for the Recipes tool;
// same patch-in-place pattern as add-guide-topics; idempotent.
if (args.Length > 1 && args[0].Equals("add-recipes", StringComparison.OrdinalIgnoreCase))
{
    if (DevGamePath() is string gamePath) CatalogSteps.AddRecipes(args[1], gamePath);
    return;
}

// "DataCataloger add-fish <target-catalog-db-path>" adds where/when every fish
// can be caught, from the game's GcFishTable (found by type, not by path):
// ProductID, Quality, Size, Time (Day/Night/Both), Biome (bool[17] in
// GcBiomeType order - the same order as a planet's biome in the save's
// discovery records), NeedsStorm, RequiresMissionActive. Category "Fish";
// GameId = "FISH:" + product id (prefixed so it never collides with the fish
// item's own catalog row), NameEnglish = product id (only so the row survives
// `trim` - the app names fish through the normal item lookup),
// DescriptionEnglish = JSON {"id","quality","size","time","storm","mission",
// "biomes":[indexes]}. Added 2026-10-11 for the Fishing tool; idempotent.
if (args.Length > 1 && args[0].Equals("add-fish", StringComparison.OrdinalIgnoreCase))
{
    if (DevGamePath() is string gamePath) CatalogSteps.AddFish(args[1], gamePath);
    return;
}

// "DataCataloger add-item-values <target-catalog-db-path>" adds the base Units
// value and trade data of every product and substance (GcProductTable /
// GcRealitySubstanceTable: BaseValue, TradeCategory, Legality, Rarity) as
// category "ItemValues". GameId = "VALUE:" + item id (prefixed so it never
// collides with the item's own catalog row in id lookups), NameEnglish =
// readable name, UsageCategory = TradeCategory (Mineral, Tech, Commodity,
// Component, Alloy, Exotic, Energy, None, SpecialShop), DescriptionEnglish =
// JSON {"id","value","legality","rarity","kind"}. Added 2026-10-11 for the
// Item Values tool; idempotent, same patch-in-place pattern as add-recipes.
if (args.Length > 1 && args[0].Equals("add-item-values", StringComparison.OrdinalIgnoreCase))
{
    if (DevGamePath() is string gamePath) CatalogSteps.AddItemValues(args[1], gamePath);
    return;
}

// "DataCataloger add-crafting <target-catalog-db-path>" adds what every craftable
// thing is made from, for the Crafting calculator: GcProductData.Requirements
// (what a product costs to craft) and GcTechnology.Requirements (what a
// technology costs to build/install), each a list of {ID, Amount, Type}.
// Category "Crafting"; GameId = "CRAFT:" + id (prefixed so it never collides
// with the item's own catalog row), NameEnglish = readable name, UsageCategory
// = "Product"/"Technology", DescriptionEnglish = JSON {"id","kind","craftable",
// "req":[[id,amount],...]}. Only rows with at least one requirement are kept;
// ids in "req" are products/substances (raw when they have no row here).
// Added 2026-10-11; idempotent, same patch-in-place pattern as add-item-values.
if (args.Length > 1 && args[0].Equals("add-crafting", StringComparison.OrdinalIgnoreCase))
{
    if (DevGamePath() is string gamePath) CatalogSteps.AddCrafting(args[1], gamePath);
    return;
}

if (args.Length > 1 && args[0].Equals("add-ship-looks", StringComparison.OrdinalIgnoreCase))
{
    if (DevGamePath() is string gamePath) CatalogSteps.AddShipLooks(args[1], gamePath);
    return;
}

if (args.Length > 1 && args[0].Equals("add-palettes", StringComparison.OrdinalIgnoreCase))
{
    if (DevGamePath() is string gamePath) CatalogSteps.AddPalettes(args[1], gamePath);
    return;
}

if (args.Length > 1 && args[0].Equals("add-part-groups", StringComparison.OrdinalIgnoreCase))
{
    if (DevGamePath() is string gamePath) CatalogSteps.AddPartGroups(args[1], gamePath);
    return;
}

// "DataCataloger add-multitool-names <target-catalog-db-path>" - see CatalogSteps.AddMultiToolNameWords.
if (args.Length > 1 && args[0].Equals("add-multitool-names", StringComparison.OrdinalIgnoreCase))
{
    if (DevGamePath() is string gamePath) CatalogSteps.AddMultiToolNameWords(args[1], gamePath);
    return;
}

// "DataCataloger add-ship-builder <target-catalog-db-path>" - see CatalogSteps.AddShipBuilder.
if (args.Length > 1 && args[0].Equals("add-ship-builder", StringComparison.OrdinalIgnoreCase))
{
    if (DevGamePath() is string gamePath) CatalogSteps.AddShipBuilder(args[1], gamePath);
    return;
}

// "DataCataloger add-ship-descriptors <target-catalog-db-path>" - see CatalogSteps.AddShipDescriptors.
if (args.Length > 1 && args[0].Equals("add-ship-descriptors", StringComparison.OrdinalIgnoreCase))
{
    if (DevGamePath() is string gamePath) CatalogSteps.AddShipDescriptors(args[1], gamePath);
    return;
}

// "DataCataloger add-settlement-costs <target-catalog-db-path>" - see CatalogSteps.AddSettlementCosts.
if (args.Length > 1 && args[0].Equals("add-settlement-costs", StringComparison.OrdinalIgnoreCase))
{
    if (DevGamePath() is string gamePath) CatalogSteps.AddSettlementCosts(args[1], gamePath);
    return;
}

// "DataCataloger add-procedural-products <target-catalog-db-path>" adds the
// procedurally generated items (save ids like ^PROC_SEA#17140 - ocean
// salvage, fossils, plant samples, documents, freighter logs...) from
// metadata/reality/tables/proceduralproducttable.mbin (GcProceduralProductTable,
// one entry per GcProceduralProductCategory). The game builds each item's
// display name from random words picked by the #seed, which can't be
// reproduced here, so each row gets the product's own subtitle/category name
// and its icon instead. Category "ProceduralProducts"; GameId = the product
// id (the app already strips "#seed" when looking items up), NameEnglish =
// resolved name, icon from Product.Icon (else the first ProceduralData icon).
// Added 2026-10-11; idempotent.
if (args.Length > 1 && args[0].Equals("add-procedural-products", StringComparison.OrdinalIgnoreCase))
{
    if (DevGamePath() is string gamePath) CatalogSteps.AddProceduralProducts(args[1], gamePath);
    return;
}

// "DataCataloger dumpfile <path substring>" decodes exactly one MBIN (the first
// entry whose path contains the given substring) and prints its full raw field
// structure, regardless of whether CatalogClassifier would recognize it as a
// table. Use this when a file is confirmed relevant (e.g. via `grep`) but never
// shows up via `inspect` - that usually means either its ID field isn't literally
// named "ID"/"Id" (so every row silently gets an empty GameId and the whole
// category gets dropped with zero items, no error), or its shape isn't a
// List<T>/array at all. Seeing the actual structure is the fastest way to find
// out which, and what the classifier would need to extract instead.
if (args.Length > 1 && args[0].Equals("dumpfile", StringComparison.OrdinalIgnoreCase))
{
    string targetPath = args[1];
    // Optional 3rd/4th args - "dumpfile <path> [depth] [listCap]" - for
    // shapes deeper/wider than the 4/5 defaults can show (e.g. GcWiki's
    // Categories[].Topics[] needed depth 8/listCap 20 to see actual topic
    // IDs instead of bare "[0] [1] ..." placeholders with nothing beneath).
    int dumpDepth = args.Length > 2 && int.TryParse(args[2], out int d) ? d : 4;
    int dumpListCap = args.Length > 3 && int.TryParse(args[3], out int lc) ? lc : 5;
    var dumpSettings = SettingsService.Load();
    if (!SettingsService.IsValid(dumpSettings))
    {
        ConsoleStyle.Error("No valid ArtifactX installation path configured yet - run the normal build once first.");
        return;
    }

    IPakDiscoveryService dumpPakDiscovery = new PakDiscoveryService();
    IPakReaderService dumpPakReader = new PakReaderService();
    var dumpExtraction = new ExtractionService();
    var dumpPaks = dumpPakDiscovery.Discover(dumpSettings.NmsInstallationPath!);

    bool found = false;

    foreach (var pak in dumpPaks)
    {
        ArtifactX.GameData.Models.PakHeader header;
        IReadOnlyList<ArtifactX.GameData.Models.PakEntry> entries;
        try
        {
            header = dumpPakReader.ReadHeader(pak.FullPath);
            entries = dumpPakReader.Read(pak.FullPath);
        }
        catch
        {
            continue;
        }

        var match = entries.FirstOrDefault(e =>
            !string.IsNullOrEmpty(e.FileName) &&
            e.FileName.Contains(targetPath, StringComparison.OrdinalIgnoreCase));

        if (match is null) continue;

        found = true;
        ConsoleStyle.Header($"Found {match.FileName} in {pak.FileName} - decoding...");

        byte[]? bytes;
        try
        {
            bytes = dumpExtraction.ExtractEntryBytes(pak.FullPath, match, header);
        }
        catch (Exception ex)
        {
            ConsoleStyle.Error($"Extraction failed: {ex.Message}");
            return;
        }
        if (bytes is null || bytes.Length == 0)
        {
            ConsoleStyle.Error("Extraction returned no bytes.");
            return;
        }

        NMSTemplate? template;
        try
        {
            using var ms = new MemoryStream(bytes);
            var mbin = new MBINFile(ms);
            mbin.Load();
            template = mbin.GetData();
        }
        catch (Exception ex)
        {
            ConsoleStyle.Error($"Decode failed: {ex}");
            return;
        }
        if (template is null)
        {
            ConsoleStyle.Error("Decoded template is null.");
            return;
        }

        ConsoleStyle.Success($"Top-level type: {template.GetType().Name}");
        DumpStructure(template, depth: dumpDepth, indent: "  ", visited: new HashSet<object>(ReferenceEqualityComparer.Instance), listCap: dumpListCap);
        return;
    }

    if (!found)
        ConsoleStyle.Error($"No file matching '{targetPath}' found in any PAK.");
}

// "DataCataloger dumptable <path-substring>" - like dumpfile, but runs the file
// through the REAL CatalogClassifier.TryClassify/ExtractRows path (not a generic
// depth-limited object dump) and prints EVERY row's GameId/UsageCategory/TemplateId
// on one compact line each. dumpfile's generic dumper caps list output at 5
// elements, which hides whatever's actually happening to rows past index 4 - this
// shows the classifier's real per-row output for the whole table in one screen,
// making it possible to see exactly which rows get an empty GameId (silently
// dropped by CatalogBuildService) or an unexpected UsageCategory.
if (args.Length > 1 && args[0].Equals("dumptable", StringComparison.OrdinalIgnoreCase))
{
    string targetPath = args[1];
    var dumpTableSettings = SettingsService.Load();
    if (!SettingsService.IsValid(dumpTableSettings))
    {
        ConsoleStyle.Error("No valid ArtifactX installation path configured yet - run the normal build once first.");
        return;
    }

    IPakDiscoveryService dtPakDiscovery = new PakDiscoveryService();
    IPakReaderService dtPakReader = new PakReaderService();
    var dtExtraction = new ExtractionService();
    var dtPaks = dtPakDiscovery.Discover(dumpTableSettings.NmsInstallationPath!);

    bool dtFound = false;
    foreach (var pak in dtPaks)
    {
        ArtifactX.GameData.Models.PakHeader header;
        IReadOnlyList<ArtifactX.GameData.Models.PakEntry> entries;
        try
        {
            header = dtPakReader.ReadHeader(pak.FullPath);
            entries = dtPakReader.Read(pak.FullPath);
        }
        catch { continue; }

        var match = entries.FirstOrDefault(e =>
            !string.IsNullOrEmpty(e.FileName) &&
            e.FileName.Contains(targetPath, StringComparison.OrdinalIgnoreCase));
        if (match is null) continue;

        dtFound = true;
        byte[]? bytes;
        try { bytes = dtExtraction.ExtractEntryBytes(pak.FullPath, match, header); }
        catch (Exception ex) { ConsoleStyle.Error($"Extraction failed: {ex.Message}"); return; }
        if (bytes is null || bytes.Length == 0) { ConsoleStyle.Error("Extraction returned no bytes."); return; }

        NMSTemplate? template;
        try
        {
            using var ms = new MemoryStream(bytes);
            var mbin = new MBINFile(ms);
            mbin.Load();
            template = mbin.GetData();
        }
        catch (Exception ex) { ConsoleStyle.Error($"Decode failed: {ex.Message}"); return; }
        if (template is null) { ConsoleStyle.Error("Decoded template is null."); return; }

        if (!ArtifactX.GameData.Services.CatalogClassifier.TryClassify(template, out var listField, out _))
        {
            ConsoleStyle.Error($"Not classifiable: {match.FileName} (top-level type {template.GetType().Name})");
            return;
        }

        var rows = ArtifactX.GameData.Services.CatalogClassifier.ExtractRows(template, listField);
        ConsoleStyle.Header($"{match.FileName}: {rows.Count} rows");
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            ConsoleStyle.Success($"  [{i}] GameId='{r.GameId}' UsageCategory={r.UsageCategory ?? "(null)"} Template={r.TemplateId ?? "(null)"} NameLocKey={r.NameLocKey ?? "(null)"}");
        }
        return;
    }

    if (!dtFound)
        ConsoleStyle.Error($"No file matching '{targetPath}' found in any PAK.");
}

// Reflection-based deep search through a decoded MBIN's object graph for any string
// field (or NMSTemplate string-wrapper, e.g. ArtifactXString0x10) containing the needle.
// Mirrors the string-extraction convention already used by CatalogClassifier/
// ReflectionUtil, just applied to every field instead of only name/description-shaped
// ones. Depth-limited and visited-tracked to stay safe against deep/cyclic graphs.
static bool ContainsStringDeep(object? obj, string needle, int depth, HashSet<object> visited)
{
    if (obj is null || depth <= 0) return false;

    if (obj is string directString)
        return directString.Contains(needle, StringComparison.OrdinalIgnoreCase);

    var type = obj.GetType();
    if (type.IsPrimitive || type.IsEnum || obj is float || obj is double || obj is decimal)
        return false;

    if (!type.IsValueType && !visited.Add(obj))
        return false;

    string? wrapped = ArtifactX.GameData.Services.ReflectionUtil.ExtractString(obj);
    if (wrapped != null)
        return wrapped.Contains(needle, StringComparison.OrdinalIgnoreCase);

    if (obj is System.Collections.IEnumerable enumerable)
    {
        foreach (var item in enumerable)
        {
            if (ContainsStringDeep(item, needle, depth - 1, visited))
                return true;
        }
        return false;
    }

    foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
    {
        object? value;
        try
        {
            value = field.GetValue(obj);
        }
        catch
        {
            continue;
        }

        if (ContainsStringDeep(value, needle, depth - 1, visited))
            return true;
    }

    return false;
}

// Prints a decoded MBIN's object graph field-by-field, depth-limited, with element
// lists capped at 5 to keep output readable for large tables. Strings (direct or
// via the ReflectionUtil wrapper convention) are truncated; everything else shows
// its type name so it's clear what shape a field actually is.
static void DumpStructure(object? obj, int depth, string indent, HashSet<object> visited, int listCap = 5)
{
    if (obj is null || depth <= 0) return;

    if (obj is string directString)
    {
        Console.WriteLine($"{indent}(string) \"{Truncate(directString, 80)}\"");
        return;
    }

    var type = obj.GetType();

    if (type.IsPrimitive || type.IsEnum || obj is float || obj is double || obj is decimal)
    {
        Console.WriteLine($"{indent}({type.Name}) {obj}");
        return;
    }

    string? wrapped = ArtifactX.GameData.Services.ReflectionUtil.ExtractString(obj);
    if (wrapped != null)
    {
        Console.WriteLine($"{indent}(wrapped string, {type.Name}) \"{Truncate(wrapped, 80)}\"");
        return;
    }

    if (!type.IsValueType && !visited.Add(obj))
    {
        Console.WriteLine($"{indent}(already visited {type.Name})");
        return;
    }

    if (obj is System.Collections.IEnumerable enumerable)
    {
        int i = 0;
        foreach (var item in enumerable)
        {
            if (i >= listCap)
            {
                Console.WriteLine($"{indent}... (truncated, showing first {listCap} elements only)");
                break;
            }
            Console.WriteLine($"{indent}[{i}]");
            DumpStructure(item, depth - 1, indent + "  ", visited, listCap);
            i++;
        }
        return;
    }

    Console.WriteLine($"{indent}({type.Name})");
    foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
    {
        object? value;
        try
        {
            value = field.GetValue(obj);
        }
        catch
        {
            continue;
        }

        if (value is null)
        {
            Console.WriteLine($"{indent}  {field.Name}: null");
            continue;
        }

        Console.WriteLine($"{indent}  {field.Name}:");
        DumpStructure(value, depth - 1, indent + "    ", visited, listCap);
    }
}

static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "...";

// "DataCataloger inspect <keyword>" searches EVERY classified table (Category)
// in the WORKING (untrimmed) catalog by keyword against TemplateType, RowType,
// or SourceMbinPath - including tables CatalogTrimService would drop for having
// no NameEnglish on any row. That's the whole point: a base-type/customization
// table (e.g. a hypothetical GcMultiToolTable mapping an ID straight to a model
// path, with no player-facing display name) would exist here even though it'd
// never survive into the trimmed distribution catalog. Dumps up to 30 raw items
// per matching category so we can see the actual field shape, not just guess.
if (args.Length > 1 && args[0].Equals("inspect", StringComparison.OrdinalIgnoreCase))
{
    string keyword = args[1];
    int itemLimit = args.Length > 2 && int.TryParse(args[2], out int parsedLimit) ? parsedLimit : 30;

    string sourcePath = Path.Combine(workingFolder, "nms_catalog.sqlite");

    if (!File.Exists(sourcePath))
    {
        ConsoleStyle.Error($"Working catalog not found at {sourcePath} - run the normal full build first.");
        return;
    }

    using var db = new ArtifactX.GameData.Data.CatalogDbContext(sourcePath);

    var allCategories = db.Categories.ToList();
    var matching = allCategories
        .Where(c =>
            (c.TemplateType?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (c.RowType?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (c.SourceMbinPath?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false))
        .ToList();

    ConsoleStyle.Header($"Found {matching.Count} matching categories for '{keyword}' (showing up to {itemLimit} items each):");

    foreach (var cat in matching)
    {
        ConsoleStyle.Info($"\n=== {cat.TemplateType} ({cat.RowType}) from {cat.SourceMbinPath} ===");

        int totalCount = db.Items.Count(i => i.CategoryId == cat.Id);
        var items = db.Items.Where(i => i.CategoryId == cat.Id).Take(itemLimit).ToList();
        ConsoleStyle.Info($"  ({items.Count} of {totalCount} total items shown)");

        foreach (var item in items)
        {
            ConsoleStyle.Success(
                $"  GameId={item.GameId}  NameLocKey={item.NameLocKey}  NameEnglish={item.NameEnglish}  " +
                $"UsageCategory={item.UsageCategory}  MaxStackSize={item.MaxStackSize}  CapacityValue={item.CapacityValue}");
        }
    }

    return;
}

LogService.Write(">>> PROGRAM.CS MAIN() EXECUTING FROM: " + AppContext.BaseDirectory);

LogService.Write($"Working folder: {workingFolder}");
LogService.Write("");

//
// 1. Load existing settings
//
var settings = SettingsService.Load();

//
// 2. Validate path using the loop-until-valid pattern
//
while (!SettingsService.IsValid(settings))
{
    LogService.Write("Configuration is missing or path is invalid.");
    LogService.Write("Please enter the path to your No Man's Sky PCBANKS folder:");

    settings.NmsInstallationPath = Console.ReadLine()?.Trim('\"');

    if (SettingsService.IsValid(settings))
    {
        SettingsService.Save(settings);
        LogService.Write("Path saved successfully!");
    }
    else
    {
        LogService.Write("Error: The provided path does not contain valid ArtifactX files. Please try again.");
    }
}

string pcbanksPath = settings.NmsInstallationPath!;
LogService.Write("");
LogService.Write($"Using PCBANKS path: {pcbanksPath}");
LogService.Write("");

//
// 3. Discover PAK files
//
IPakDiscoveryService pakDiscovery = new PakDiscoveryService();
var paks = pakDiscovery.Discover(pcbanksPath);

ConsoleStyle.Header($"Found {paks.Count} PAK files in:");
ConsoleStyle.Info(pcbanksPath);
LogService.Write("");

foreach (var pak in paks)
{
    ConsoleStyle.Success($"- {pak.FileName} ({pak.FileSize} bytes)");
}

LogService.Write(new string('=', 80));
LogService.Write("");

//
// 4. Run the full catalog build: index every PAK, resolve localisation,
//    classify every table-shaped MBIN, extract + convert icons, write SQLite.
//
IPakReaderService pakReader = new PakReaderService();
var extraction = new ExtractionService();

string dbOutputPath = Path.Combine(workingFolder, "nms_catalog.sqlite");

var builder = new CatalogBuildService(pakDiscovery, pakReader, extraction);

try
{
    builder.Run(pcbanksPath, dbOutputPath, iconTargetSize: 128);
}
catch (Exception ex)
{
    LogService.Write($"Catalog build failed: {ex}");
}

LogService.Write("");
LogService.Write("Pipeline complete.");

// The developer's PCBANKS folder from config.json (the add-* steps take it as an argument now).
static string? DevGamePath()
{
    var devSettings = SettingsService.Load();
    if (SettingsService.IsValid(devSettings)) return devSettings.NmsInstallationPath;
    if (GameInstall.Find() is { } found) return found.PcbanksPath;
    ConsoleStyle.Error("No No Man's Sky install found, and no installation path configured - run the normal build once first.");
    return null;
}

