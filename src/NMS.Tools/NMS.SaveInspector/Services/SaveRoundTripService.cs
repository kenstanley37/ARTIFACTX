using NMS.Core;

namespace NMS.Tools.SaveInspector.Services;

/// <summary>
/// Isolates one question at a time: decompresses a real save file and
/// immediately recompresses the identical JSON back out, with zero edits.
/// If the game can load the result, the container writer itself is correct —
/// independent of whether the mf_ manifest turns out to matter.
/// </summary>
public static class SaveRoundTripService
{
    public static async Task RoundTripAsync(string inputPath, string outputPath)
    {
        if (!File.Exists(inputPath))
        {
            ConsoleStyle.Error($"File not found: {inputPath}");
            return;
        }

        ConsoleStyle.Header($"Decompressing: {inputPath}");
        string json = await SaveStreamProcessor.ExtractRawJsonAsync(inputPath);
        ConsoleStyle.Success($"Decompressed {json.Length:N0} characters of JSON.");

        ConsoleStyle.Header($"Recompressing, unmodified, to: {outputPath}");
        await SaveStreamProcessor.WriteSaveContainerAsync(json, outputPath);

        var outputInfo = new FileInfo(outputPath);
        ConsoleStyle.Success($"Wrote {outputInfo.Length:N0} bytes.");

        ConsoleStyle.Info("Next: copy this over a sacrificial save slot's .hg file, leave its mf_ manifest untouched, and try loading that slot in-game.");
    }
}