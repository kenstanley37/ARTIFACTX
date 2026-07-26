using ArtifactX.Tools.DataCataloger.Services;
using ArtifactX.Tools.DataCataloger.Services.Interfaces;
using System.Reflection;
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
                $"UsageCategory={item.UsageCategory}  MaxStackSize={item.MaxStackSize}");
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