using ArtifactX.Core.Language;
using Newtonsoft.Json.Linq;

internal static class LanguageWordStatsChecks
{
    /// <summary>A decrypted save the game itself wrote (`path`, e.g. Slot 8 after
    /// learning Korvax "gift", 2026-11-04): the word counters LanguageWordStats
    /// works out from its known-word list must equal the ones the game saved.</summary>
    public static void Native(string path)
    {
        var player = JObject.Parse(File.ReadAllText(path))["vLc"]?["6f="] ?? throw new InvalidOperationException("No player state in " + path);
        var words = player["MF2"] as JArray ?? throw new InvalidOperationException("No known-word list in " + path);
        var stats = (player["gUR"] as JArray)?.FirstOrDefault(g => g[":rc"]?.Value<string>() == "^GLOBAL_STATS")?["gUR"] as JArray
            ?? throw new InvalidOperationException("No GLOBAL_STATS in " + path);
        int failures = 0;
        foreach (var (id, count) in LanguageWordStats.Counts(words))
        {
            int saved = stats.FirstOrDefault(s => s["b2n"]?.Value<string>() == "^" + id)?[">MX"]?[">vs"]?.Value<int>() ?? 0;
            bool ok = saved == count;
            if (!ok) failures++;
            Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {id}: list {count}, save {saved}");
        }
        if (!JToken.DeepEquals(stats, LanguageWordStats.WithCounts(stats, words)))
        {
            failures++;
            Console.WriteLine("FAIL WithCounts changed a save whose counters already match");
        }
        if (failures > 0) throw new InvalidOperationException($"{failures} language word counter checks failed.");
        Console.WriteLine("Language word counters match the game's save.");
    }
}
