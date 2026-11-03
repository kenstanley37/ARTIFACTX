using Newtonsoft.Json.Linq;

namespace ArtifactX.Core.Unlocks;

/// <summary>Builds detached per-save unlock edits. KnownSpecials is the list
/// following KnownProducts in the inspected player-state save (24&lt;); the
/// build-184271 special grant routine updates its runtime set. Recycling title
/// thresholds were checked against installed title data and real integer stats
/// on 2026-11-02. This never writes a file or changes account-wide entitlements.</summary>
public static class VehicleUnlockEdits
{
    public static JArray Specials(JArray current, string id, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("An unlock id is required.", nameof(id));
        id = id.TrimStart('^');
        if (id.Length == 0) throw new ArgumentException("An unlock id is required.", nameof(id));
        var result = (JArray)current.DeepClone();
        if (enabled)
        {
            if (!result.Any(t => t.Value<string>()?.TrimStart('^') == id)) result.Add("^" + id);
        }
        else
            foreach (var item in result.Where(t => t.Value<string>()?.TrimStart('^') == id).ToArray()) item.Remove();
        return result;
    }

    /// <summary>Raises only catalog-declared integer counters. Existing higher
    /// progress, float-valued stats and unrelated fields are preserved. Unknown
    /// prerequisite shapes are rejected before the caller stages any edit.</summary>
    public static JArray RaiseRequirements(JArray current, IEnumerable<TitleUnlockRule> rules)
    {
        var result = (JArray)current.DeepClone();
        foreach (var rule in rules)
        {
            if (rule.StatRank != 0 || rule.MissionId.Length > 0 || rule.ProductId.Length > 0
                || rule.TrophyId.Length > 0 || rule.Interaction != "None"
                || rule.StatId.Length == 0 || !float.IsFinite(rule.StatValue)
                || rule.StatValue < 0 || rule.StatValue != MathF.Truncate(rule.StatValue) || (double)rule.StatValue > int.MaxValue)
                throw new InvalidOperationException("This unlock has prerequisites the editor cannot safely adjust yet.");
            var entries = result.OfType<JObject>().Where(s => s["b2n"]?.Value<string>()?.TrimStart('^') == rule.StatId).ToArray();
            if (entries.Length > 1) throw new InvalidOperationException("Duplicate prerequisite counters need inspection before editing.");
            var stat = entries.SingleOrDefault();
            if (stat?[">MX"]?["eoL"] is not null)
                throw new InvalidOperationException("The prerequisite counter has an unexpected number format.");
            if (stat is null)
            {
                stat = new JObject { ["b2n"] = "^" + rule.StatId, [">MX"] = new JObject() };
                result.Add(stat);
            }
            if (stat[">MX"] is not JObject value)
                throw new InvalidOperationException("The prerequisite counter has an unexpected structure.");
            if (value[">vs"] is JToken number && number.Type != JTokenType.Integer)
                throw new InvalidOperationException("The prerequisite counter is not an integer.");
            long before = value[">vs"]?.Value<long>() ?? 0;
            if (before < (long)rule.StatValue) value[">vs"] = (long)rule.StatValue;
        }
        return result;
    }

    /// <summary>Reverts only entries changed by this editor, retaining later
    /// changes made elsewhere. A null original/applied entry represents absence.</summary>
    public static JArray RestoreEntries(JArray current, IEnumerable<UnlockEntryChange> changes, string? idField)
    {
        var result = (JArray)current.DeepClone();
        foreach (var change in changes)
        {
            string Key(JToken token) => (idField is null ? token.Value<string>() : token[idField]?.Value<string>())?.TrimStart('^') ?? "";
            var matching = result.Where(t => Key(t) == change.Id).ToArray();
            if (matching.Length > 1) continue;
            var value = matching.SingleOrDefault();
            if (!JToken.DeepEquals(value, change.Applied)) continue;
            if (change.Original is null) value?.Remove();
            else if (value is not null) value.Replace(change.Original.DeepClone());
            else result.Add(change.Original.DeepClone());
        }
        return result;
    }
}

public sealed record UnlockEntryChange(string Id, JToken? Original, JToken? Applied);
