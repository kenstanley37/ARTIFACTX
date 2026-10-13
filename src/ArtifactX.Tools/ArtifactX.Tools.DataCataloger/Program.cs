using ArtifactX.Tools.DataCataloger.Data;
using ArtifactX.Tools.DataCataloger.Models;
using ArtifactX.Tools.DataCataloger.Services;
using ArtifactX.Tools.DataCataloger.Services.Interfaces;
using System.Reflection;
using System.Text.RegularExpressions;
using libMBIN;

string workingFolder = Path.Combine(AppContext.BaseDirectory, "Working");
Directory.CreateDirectory(workingFolder);

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
        IReadOnlyList<ArtifactX.Tools.DataCataloger.Models.PakEntry> entries;
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
        IReadOnlyList<ArtifactX.Tools.DataCataloger.Models.PakEntry> entries;
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
        IReadOnlyList<ArtifactX.Tools.DataCataloger.Models.PakEntry> entries;
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
        ArtifactX.Tools.DataCataloger.Models.PakHeader header;
        IReadOnlyList<ArtifactX.Tools.DataCataloger.Models.PakEntry> entries;
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
        ArtifactX.Tools.DataCataloger.Models.PakHeader header;
        IReadOnlyList<ArtifactX.Tools.DataCataloger.Models.PakEntry> entries;
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
        ArtifactX.Tools.DataCataloger.Models.PakHeader header;
        IReadOnlyList<ArtifactX.Tools.DataCataloger.Models.PakEntry> entries;
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
        ArtifactX.Tools.DataCataloger.Models.PakHeader header;
        IReadOnlyList<ArtifactX.Tools.DataCataloger.Models.PakEntry> entries;
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
    string targetDbPath = args[1];
    if (!File.Exists(targetDbPath))
    {
        ConsoleStyle.Error($"Target catalog DB not found: {targetDbPath}");
        return;
    }

    var wordSettings = SettingsService.Load();
    if (!SettingsService.IsValid(wordSettings))
    {
        ConsoleStyle.Error("No valid ArtifactX installation path configured yet - run the normal build once first.");
        return;
    }

    IPakDiscoveryService wordPakDiscovery = new PakDiscoveryService();
    IPakReaderService wordPakReader = new PakReaderService();
    var wordExtraction = new ExtractionService();
    var wordPaks = wordPakDiscovery.Discover(wordSettings.NmsInstallationPath!);

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
    string brokenDbPath = args[1];
    if (!File.Exists(brokenDbPath))
    {
        ConsoleStyle.Error($"Target catalog DB not found: {brokenDbPath}");
        return;
    }

    var brokenSettings = SettingsService.Load();
    if (!SettingsService.IsValid(brokenSettings))
    {
        ConsoleStyle.Error("No valid ArtifactX installation path configured yet - run the normal build once first.");
        return;
    }

    IPakReaderService brokenPakReader = new PakReaderService();
    var brokenExtraction = new ExtractionService();
    libMBIN.NMS.GameComponents.GcTechnologyTable? techTable = null;

    foreach (var pak in new PakDiscoveryService().Discover(brokenSettings.NmsInstallationPath!))
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
    string assetsDbPath = args[1];
    if (!File.Exists(assetsDbPath))
    {
        ConsoleStyle.Error($"Target catalog DB not found: {assetsDbPath}");
        return;
    }

    var assetsSettings = SettingsService.Load();
    if (!SettingsService.IsValid(assetsSettings))
    {
        ConsoleStyle.Error("No valid ArtifactX installation path configured yet - run the normal build once first.");
        return;
    }

    IPakReaderService assetsPakReader = new PakReaderService();
    var assetsExtraction = new ExtractionService();
    var assetsIndex = new GlobalFileIndexService();

    foreach (var pak in new PakDiscoveryService().Discover(assetsSettings.NmsInstallationPath!))
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
    string guideDbPath = args[1];
    if (!File.Exists(guideDbPath))
    {
        ConsoleStyle.Error($"Target catalog DB not found: {guideDbPath}");
        return;
    }

    var guideSettings = SettingsService.Load();
    if (!SettingsService.IsValid(guideSettings))
    {
        ConsoleStyle.Error("No valid ArtifactX installation path configured yet - run the normal build once first.");
        return;
    }

    IPakDiscoveryService guidePakDiscovery = new PakDiscoveryService();
    IPakReaderService guidePakReader = new PakReaderService();
    var guideExtraction = new ExtractionService();
    var guidePaks = guidePakDiscovery.Discover(guideSettings.NmsInstallationPath!);

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
    string paintDbPath = args[1];
    if (!File.Exists(paintDbPath))
    {
        ConsoleStyle.Error($"Target catalog DB not found: {paintDbPath}");
        return;
    }

    var paintSettings = SettingsService.Load();
    if (!SettingsService.IsValid(paintSettings))
    {
        ConsoleStyle.Error("No valid ArtifactX installation path configured yet - run the normal build once first.");
        return;
    }

    IPakReaderService paintPakReader = new PakReaderService();
    var paintExtraction = new ExtractionService();
    var paintPaks = new PakDiscoveryService().Discover(paintSettings.NmsInstallationPath!);

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
    string corvetteDbPath = args[1];
    if (!File.Exists(corvetteDbPath))
    {
        ConsoleStyle.Error($"Target catalog DB not found: {corvetteDbPath}");
        return;
    }

    var corvetteSettings = SettingsService.Load();
    if (!SettingsService.IsValid(corvetteSettings))
    {
        ConsoleStyle.Error("No valid ArtifactX installation path configured yet - run the normal build once first.");
        return;
    }

    IPakReaderService corvettePakReader = new PakReaderService();
    var corvetteExtraction = new ExtractionService();
    libMBIN.NMS.GameComponents.GcBaseBuildingTable? buildingTable = null;
    foreach (var pak in new PakDiscoveryService().Discover(corvetteSettings.NmsInstallationPath!))
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
    string statsDbPath = args[1];
    if (!File.Exists(statsDbPath))
    {
        ConsoleStyle.Error($"Target catalog DB not found: {statsDbPath}");
        return;
    }

    var statsSettings = SettingsService.Load();
    if (!SettingsService.IsValid(statsSettings))
    {
        ConsoleStyle.Error("No valid ArtifactX installation path configured yet - run the normal build once first.");
        return;
    }

    IPakReaderService statsPakReader = new PakReaderService();
    var statsExtraction = new ExtractionService();
    libMBIN.NMS.GameComponents.GcTechnologyTable? techTable = null;
    libMBIN.NMS.GameComponents.GcProceduralTechnologyTable? procTable = null;
    foreach (var pak in new PakDiscoveryService().Discover(statsSettings.NmsInstallationPath!))
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
    foreach (var proc in procTable.Table)
    {
        string id = proc.ID?.Value ?? "";
        string template = proc.Template?.Value ?? "";
        if (id.Length == 0 || !techStats.TryGetValue(template, out var fromTemplate)) continue;
        if (techStats.TryAdd(id, (fromTemplate.Stat, false, fromTemplate.Link))) procCount++;
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
            UsageCategory = info.Primary ? "Primary" : ""
        });
    statsDb.SaveChanges();

    ConsoleStyle.Success($"Wrote {techStats.Count} tech stats to {statsDbPath}.");
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
    string descDbPath = args[1];
    if (!File.Exists(descDbPath))
    {
        ConsoleStyle.Error($"Target catalog DB not found: {descDbPath}");
        return;
    }

    var descSettings = SettingsService.Load();
    if (!SettingsService.IsValid(descSettings))
    {
        ConsoleStyle.Error("No valid ArtifactX installation path configured yet - run the normal build once first.");
        return;
    }

    IPakReaderService descPakReader = new PakReaderService();
    var descExtraction = new ExtractionService();
    var descLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var pak in new PakDiscoveryService().Discover(descSettings.NmsInstallationPath!))
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
    string recipeDbPath = args[1];
    if (!File.Exists(recipeDbPath))
    {
        ConsoleStyle.Error($"Target catalog DB not found: {recipeDbPath}");
        return;
    }

    var recipeSettings = SettingsService.Load();
    if (!SettingsService.IsValid(recipeSettings))
    {
        ConsoleStyle.Error("No valid ArtifactX installation path configured yet - run the normal build once first.");
        return;
    }

    IPakReaderService recipePakReader = new PakReaderService();
    var recipeExtraction = new ExtractionService();
    libMBIN.NMS.GameComponents.GcRecipeTable? recipeTable = null;
    var recipeLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    foreach (var pak in new PakDiscoveryService().Discover(recipeSettings.NmsInstallationPath!))
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
    string fishDbPath = args[1];
    if (!File.Exists(fishDbPath))
    {
        ConsoleStyle.Error($"Target catalog DB not found: {fishDbPath}");
        return;
    }

    var fishSettings = SettingsService.Load();
    if (!SettingsService.IsValid(fishSettings))
    {
        ConsoleStyle.Error("No valid ArtifactX installation path configured yet - run the normal build once first.");
        return;
    }

    IPakReaderService fishPakReader = new PakReaderService();
    var fishExtraction = new ExtractionService();
    libMBIN.NMS.GameComponents.GcFishTable? fishTable = null;
    string fishSource = "";

    foreach (var pak in new PakDiscoveryService().Discover(fishSettings.NmsInstallationPath!))
    {
        if (fishTable is not null) break;
        PakHeader header;
        IReadOnlyList<PakEntry> entries;
        try
        {
            header = fishPakReader.ReadHeader(pak.FullPath);
            entries = fishPakReader.Read(pak.FullPath);
        }
        catch { continue; }

        foreach (var entry in entries.Where(e => !string.IsNullOrEmpty(e.FileName)
                     && e.FileName.EndsWith(".mbin", StringComparison.OrdinalIgnoreCase)
                     && e.FileName.Contains("fish", StringComparison.OrdinalIgnoreCase)))
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
                if (mbin.GetData() is libMBIN.NMS.GameComponents.GcFishTable table)
                {
                    fishTable = table;
                    fishSource = entry.FileName!;
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
    string valueDbPath = args[1];
    if (!File.Exists(valueDbPath))
    {
        ConsoleStyle.Error($"Target catalog DB not found: {valueDbPath}");
        return;
    }

    var valueSettings = SettingsService.Load();
    if (!SettingsService.IsValid(valueSettings))
    {
        ConsoleStyle.Error("No valid ArtifactX installation path configured yet - run the normal build once first.");
        return;
    }

    IPakReaderService valuePakReader = new PakReaderService();
    var valueExtraction = new ExtractionService();
    libMBIN.NMS.GameComponents.GcProductTable? productTable = null;
    libMBIN.NMS.GameComponents.GcSubstanceTable? substanceTable = null;
    var valueLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    foreach (var pak in new PakDiscoveryService().Discover(valueSettings.NmsInstallationPath!))
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
    string craftDbPath = args[1];
    if (!File.Exists(craftDbPath))
    {
        ConsoleStyle.Error($"Target catalog DB not found: {craftDbPath}");
        return;
    }

    var craftSettings = SettingsService.Load();
    if (!SettingsService.IsValid(craftSettings))
    {
        ConsoleStyle.Error("No valid ArtifactX installation path configured yet - run the normal build once first.");
        return;
    }

    IPakReaderService craftPakReader = new PakReaderService();
    var craftExtraction = new ExtractionService();
    libMBIN.NMS.GameComponents.GcProductTable? craftProducts = null;
    libMBIN.NMS.GameComponents.GcTechnologyTable? craftTech = null;
    var craftLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    foreach (var pak in new PakDiscoveryService().Discover(craftSettings.NmsInstallationPath!))
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
    string procDbPath = args[1];
    if (!File.Exists(procDbPath))
    {
        ConsoleStyle.Error($"Target catalog DB not found: {procDbPath}");
        return;
    }

    var procSettings = SettingsService.Load();
    if (!SettingsService.IsValid(procSettings))
    {
        ConsoleStyle.Error("No valid ArtifactX installation path configured yet - run the normal build once first.");
        return;
    }

    IPakReaderService procPakReader = new PakReaderService();
    var procExtraction = new ExtractionService();
    var procIndex = new GlobalFileIndexService();
    libMBIN.NMS.GameComponents.GcProceduralProductTable? procTable = null;
    var procLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    foreach (var pak in new PakDiscoveryService().Discover(procSettings.NmsInstallationPath!))
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
        ArtifactX.Tools.DataCataloger.Models.PakHeader header;
        IReadOnlyList<ArtifactX.Tools.DataCataloger.Models.PakEntry> entries;
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
        ArtifactX.Tools.DataCataloger.Models.PakHeader header;
        IReadOnlyList<ArtifactX.Tools.DataCataloger.Models.PakEntry> entries;
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

        if (!ArtifactX.Tools.DataCataloger.Services.CatalogClassifier.TryClassify(template, out var listField, out _))
        {
            ConsoleStyle.Error($"Not classifiable: {match.FileName} (top-level type {template.GetType().Name})");
            return;
        }

        var rows = ArtifactX.Tools.DataCataloger.Services.CatalogClassifier.ExtractRows(template, listField);
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

    string? wrapped = ArtifactX.Tools.DataCataloger.Services.ReflectionUtil.ExtractString(obj);
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

    string? wrapped = ArtifactX.Tools.DataCataloger.Services.ReflectionUtil.ExtractString(obj);
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

    using var db = new ArtifactX.Tools.DataCataloger.Data.CatalogDbContext(sourcePath);

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