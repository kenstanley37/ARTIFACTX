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
            ConsoleStyle.Error($"Decode failed: {ex.Message}");
            return;
        }
        if (template is null)
        {
            ConsoleStyle.Error("Decoded template is null.");
            return;
        }

        ConsoleStyle.Success($"Top-level type: {template.GetType().Name}");
        DumpStructure(template, depth: 4, indent: "  ", visited: new HashSet<object>(ReferenceEqualityComparer.Instance));
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
static void DumpStructure(object? obj, int depth, string indent, HashSet<object> visited)
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
            if (i >= 5)
            {
                Console.WriteLine($"{indent}... (truncated, showing first 5 elements only)");
                break;
            }
            Console.WriteLine($"{indent}[{i}]");
            DumpStructure(item, depth - 1, indent + "  ", visited);
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
        DumpStructure(value, depth - 1, indent + "    ", visited);
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