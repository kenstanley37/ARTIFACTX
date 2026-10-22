using ArtifactX.WinUI3.Models;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// An inventory's stat-bonus array (@bB: entries of QL1 key and &gt;MX value) - a
/// Multi-Tool's Damage/Mining/Scanner, a Starship's Damage/Shield/Hyperdrive/
/// Maneuverability, a freighter's Hyperdrive/Fleet Coordination.
///
/// Writes rebuild and stage the WHOLE array: a deeper leaf-only stage isn't seen by
/// SaveSessionManager's staged-edit lookup, which only matches at the exact path queried.
/// Entries nobody shows (e.g. a Living Ship's "^ALIEN_SHIP") pass through untouched.
/// </summary>
public static class StatBonusService
{
    /// <summary>The base stats each kind of equipment shows (details card, template preview).</summary>
    public static readonly (string Label, string Key)[] MultiToolStats = { ("Damage", "^WEAPON_DAMAGE"), ("Mining", "^WEAPON_MINING"), ("Scanner", "^WEAPON_SCAN") };
    public static readonly (string Label, string Key)[] ShipStats = { ("Damage", "^SHIP_DAMAGE"), ("Shield", "^SHIP_SHIELD"), ("Hyperdrive", "^SHIP_HYPERDRIVE"), ("Maneuverability", "^SHIP_AGILE") };
    public static readonly (string Label, string Key)[] FreighterStats = { ("Hyperdrive", "^FREI_HYPERDRIVE"), ("Fleet Coordination", "^FREI_FLEET") };

    /// <summary>The base stats for a template's SourceKind ("MultiTool", "Ship", "Freighter").</summary>
    public static (string Label, string Key)[] StatsFor(string sourceKind) => sourceKind switch
    {
        "Ship" => ShipStats,
        "Freighter" => FreighterStats,
        _ => MultiToolStats,
    };

    public static double? Read(string[] bonusesPath, string key) =>
        (SaveSessionManager.GetValue(bonusesPath) as JArray)?
            .FirstOrDefault(b => b["QL1"]?.Value<string>() == key)?[">MX"]?.Value<double>();

    /// <summary>Every entry, for a Full Build template - not just the ones shown, so
    /// nothing present gets dropped on save.</summary>
    public static List<NmsLoadoutStat> ReadAll(string[] bonusesPath)
    {
        var result = new List<NmsLoadoutStat>();
        if (SaveSessionManager.GetValue(bonusesPath) is not JArray bonuses) return result;

        foreach (var entry in bonuses)
        {
            string? key = entry["QL1"]?.Value<string>();
            double? value = entry[">MX"]?.Value<double>();
            if (key is not null && value is not null)
                result.Add(new NmsLoadoutStat { Key = key, Value = value.Value });
        }
        return result;
    }

    public static void Write(string[] bonusesPath, string key, double value) =>
        WriteAll(bonusesPath, new[] { new NmsLoadoutStat { Key = key, Value = value } });

    /// <summary>Sets the given entries' values (by key); the rest stay as they are.</summary>
    public static void WriteAll(string[] bonusesPath, IEnumerable<NmsLoadoutStat> stats)
    {
        var byKey = stats.ToDictionary(s => s.Key, s => s.Value);
        if (byKey.Count == 0 || SaveSessionManager.GetValue(bonusesPath) is not JArray bonuses) return;

        var updated = new JArray();
        foreach (var entry in bonuses)
        {
            if (entry is JObject obj && obj["QL1"]?.Value<string>() is string key && byKey.TryGetValue(key, out double value))
            {
                var clone = (JObject)obj.DeepClone();
                clone[">MX"] = value;
                updated.Add(clone);
            }
            else
            {
                updated.Add(entry.DeepClone());
            }
        }
        SaveSessionManager.StageEdit(updated, bonusesPath);
    }
}
