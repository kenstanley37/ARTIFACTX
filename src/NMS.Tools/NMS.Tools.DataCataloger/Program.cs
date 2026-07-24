using NMS.Tools.DataCataloger.Services;
using NMS.Tools.DataCataloger.Services.Interfaces;

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
        LogService.Write("Error: The provided path does not contain valid NMS files. Please try again.");
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