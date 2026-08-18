using Newtonsoft.Json.Linq;
using ArtifactX.Core;
using System.Linq;

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

    /// <summary>One-off diagnostic: appends a raw ("^"-prefixed) id to the
    /// per-slot "known/discovered technology" array at vLc.6f=.4kj, to test
    /// whether that's the real structure behind the in-game Catalog & Guide
    /// -> Catalogue screen's silhouette/revealed state (accountdata.hg's B1h
    /// was tested and ruled out - see project_account_data_page.md).</summary>
    public static async Task AddToKnownTechAsync(string inputPath, string outputPath, string rawId)
    {
        if (!File.Exists(inputPath))
        {
            ConsoleStyle.Error($"File not found: {inputPath}");
            return;
        }

        ConsoleStyle.Header($"Decompressing: {inputPath}");
        string json = await SaveStreamProcessor.ExtractRawJsonAsync(inputPath);
        string trimmed = json.TrimEnd('\0', ' ', '\r', '\n');
        JObject root = JObject.Parse(trimmed);

        if (root["vLc"]?["6f="] is not JObject stateData || stateData["4kj"] is not JArray known)
        {
            ConsoleStyle.Error("Couldn't find vLc.6f=.4kj - unexpected save structure, aborting.");
            return;
        }

        string prefixed = rawId.StartsWith('^') ? rawId : "^" + rawId;
        ConsoleStyle.Info($"Current vLc.6f=.4kj length: {known.Count}");

        if (known.Any(t => t.Value<string>() == prefixed))
        {
            ConsoleStyle.Info($"{prefixed} is already present - nothing to change.");
        }
        else
        {
            known.Add(prefixed);
            ConsoleStyle.Success($"Added {prefixed} - new length: {known.Count}");
        }

        string editedJson = root.ToString(Newtonsoft.Json.Formatting.None);

        ConsoleStyle.Header($"Recompressing edited save to: {outputPath}");
        await SaveStreamProcessor.WriteSaveContainerAsync(editedJson, outputPath);

        var outputInfo = new FileInfo(outputPath);
        ConsoleStyle.Success($"Wrote {outputInfo.Length:N0} bytes.");
    }
}