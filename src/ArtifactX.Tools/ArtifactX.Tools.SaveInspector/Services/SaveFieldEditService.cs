using Newtonsoft.Json.Linq;
using ArtifactX.Core;

namespace ArtifactX.Tools.SaveInspector.Services;

/// <summary>
/// Proves a single surgical edit survives the round trip without disturbing
/// anything else: parses into a generic JObject (not the partial NmsSaveFile
/// model, which would drop every unmapped key on reserialize), changes exactly
/// one path, and writes everything else back untouched.
/// </summary>
public static class SaveFieldEditService
{
    public static async Task EditUnitsAsync(string inputPath, string outputPath, long newUnits)
    {
        if (!File.Exists(inputPath))
        {
            ConsoleStyle.Error($"File not found: {inputPath}");
            return;
        }

        ConsoleStyle.Header($"Decompressing: {inputPath}");
        string json = await SaveStreamProcessor.ExtractRawJsonAsync(inputPath);

        // The container's decompressed block leaves trailing null-padding after
        // the JSON text - JObject.Parse won't tolerate garbage after the closing brace.
        string trimmed = json.TrimEnd('\0', ' ', '\r', '\n');

        JObject root = JObject.Parse(trimmed);

        if (root["vLc"]?["6f="] is not JObject universeDetail)
        {
            ConsoleStyle.Error("Couldn't find vLc.6f= - unexpected save structure, aborting.");
            return;
        }

        long? currentUnits = universeDetail["wGS"]?.Value<long>();
        ConsoleStyle.Info($"Current Units (vLc.6f=.wGS): {currentUnits}");

        universeDetail["wGS"] = newUnits;
        ConsoleStyle.Success($"Set Units (vLc.6f=.wGS) to: {newUnits}");

        string editedJson = root.ToString(Newtonsoft.Json.Formatting.None);

        ConsoleStyle.Header($"Recompressing edited save to: {outputPath}");
        await SaveStreamProcessor.WriteSaveContainerAsync(editedJson, outputPath);

        var outputInfo = new FileInfo(outputPath);
        ConsoleStyle.Success($"Wrote {outputInfo.Length:N0} bytes.");
        ConsoleStyle.Info("Every other key was left exactly as parsed - only vLc.wGS changed.");
    }
}