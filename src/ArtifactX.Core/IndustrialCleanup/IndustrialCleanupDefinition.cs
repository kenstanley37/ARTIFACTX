using Newtonsoft.Json.Linq;

namespace ArtifactX.Core.IndustrialCleanup;

public sealed record CleanupInstruction(string Id, string Title, string Text);
public sealed record CleanupCounter(string StatId, string Label);
public sealed record IndustrialCleanupDefinition(string Title, string Description,
    string FindingTitle, string FindingText, CleanupInstruction[] Instructions, CleanupCounter[] Counters);

/// <summary>Read-only lifetime progress, not mission completion or granted rewards.
/// HAUL_LOOP FormatStat conditions map five display counters in build 184271;
/// compared to the owner's mission-log screenshots on 2026-11-03. Empty stat
/// value objects represent zero; absent/unreadable groups remain unavailable.</summary>
public static class IndustrialCleanupProgress
{
    public static Dictionary<string, double> Read(JArray? groups)
    {
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        var global = groups?.FirstOrDefault(g => g[":rc"]?.Value<string>()?.TrimStart('^') == "GLOBAL_STATS")?["gUR"] as JArray;
        if (global is null) return result;
        foreach (var stat in global.OfType<JObject>())
        {
            string id = stat["b2n"]?.Value<string>()?.TrimStart('^') ?? "";
            if (id.Length == 0 || stat[">MX"] is not JObject number) continue;
            var token = number[">vs"] ?? number["eoL"];
            if (token is not null && token.Type is not (JTokenType.Integer or JTokenType.Float)) continue;
            double value = token?.Value<double>() ?? 0;
            if (double.IsFinite(value)) result.TryAdd(id, value);
        }
        return result;
    }
}
