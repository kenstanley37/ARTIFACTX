using NMS.Tools.SaveInspector.Services;

if (args.Length == 0)
{
    LogService.Write("Usage: SaveInspector <path-to-file> [--full] [--extract]");
    LogService.Write("  --full     Dump the entire file as hex+ASCII instead of just the first 256 bytes.");
    LogService.Write("  --extract  If the file decompresses as a standard save container, write the raw JSON next to it as <file>.json.");
    return;
}

string targetPath = args[0];
bool dumpFull = args.Contains("--full", StringComparer.OrdinalIgnoreCase);
bool extract = args.Contains("--extract", StringComparer.OrdinalIgnoreCase);

await FileInspectionService.InspectAsync(targetPath, dumpFull, extract);