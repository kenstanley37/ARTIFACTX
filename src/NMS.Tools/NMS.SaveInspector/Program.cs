using NMS.Tools.SaveInspector.Services;

if (args.Length == 0)
{
    PrintUsage();
    return;
}

switch (args[0].ToLowerInvariant())
{
    case "inspect":
        if (args.Length < 2) { PrintUsage(); break; }
        await FileInspectionService.InspectAsync(
            args[1],
            args.Contains("--full", StringComparer.OrdinalIgnoreCase),
            args.Contains("--extract", StringComparer.OrdinalIgnoreCase));
        break;

    case "roundtrip":
        if (args.Length < 3) { PrintUsage(); break; }
        await SaveRoundTripService.RoundTripAsync(args[1], args[2]);
        break;

    case "edit-units":
        if (args.Length < 4 || !long.TryParse(args[3], out long newUnits)) { PrintUsage(); break; }
        await SaveFieldEditService.EditUnitsAsync(args[1], args[2], newUnits);
        break;

    default:
        PrintUsage();
        break;
}

void PrintUsage()
{
    LogService.Write("Usage:");
    LogService.Write("  SaveInspector inspect <path-to-file> [--full] [--extract]");
    LogService.Write("  SaveInspector roundtrip <input.hg> <output.hg>");
    LogService.Write("  SaveInspector edit-units <input.hg> <output.hg> <newUnitsValue>");
}