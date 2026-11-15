using System.Globalization;
using ArtifactX.Core.Universe;
using Newtonsoft.Json.Linq;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Reads a saved technology inventory (a starship's PMT, a multi-tool's OsQ) into the shape the stat evaluators
/// take: unlocked cells (hl?), installed technology with rolled module stats (:No, Technology slots: b2n id,
/// 3ZH position, eVk damage), supercharged cells (MMm, TechBonus) and the stat-bonus array (@bB: QL1 / >MX).
/// Slot 8 inspection 2026-10-31 confirmed these keys on ships; the same GcInventoryStore layout holds for
/// multi-tools. Anything it can't resolve returns null rather than silently leaving a technology out.
/// </summary>
public static class NmsTechnologyInventoryPaths
{
    public sealed record TechnologyInventory(GeneratedMultiToolInventory Inventory,
        IReadOnlySet<GeneratedInventoryPosition> DamagedPositions);

    public static TechnologyInventory? ReadTechnology(JObject? store, MultiToolTechnologyTables tables)
    {
        if (store?["hl?"] is not JArray unlocked || store[":No"] is not JArray slots || store["MMm"] is not JArray special
            || tables.Definitions is not { } definitions || tables.ProceduralModules is not { } modules
            || tables.WeightingCurves is not { } curves) return null;
        var cells = unlocked.Select(Position).ToArray();
        if (cells.Length == 0 || cells.Any(p => p is null || p.Value.X < 0 || p.Value.Y < 0)) return null;
        var positions = cells.Select(p => p!.Value).ToArray();
        var installed = new List<GeneratedInstalledTechnology>();
        var damaged = new HashSet<GeneratedInventoryPosition>();
        foreach (var slot in slots)
        {
            if (slot["Vn8"]?["elv"]?.Value<string>() != "Technology") continue;
            string? id = slot["b2n"]?.Value<string>()?.TrimStart('^');
            if (string.IsNullOrEmpty(id) || Position(slot["3ZH"]) is not { } position
                || !positions.Contains(position) || slot["eVk"]?.Type is not (JTokenType.Float or JTokenType.Integer)) return null;
            string[] parts = id.Split('#');
            if (parts.Length > 2 || definitions.Count(d => d.Id == parts[0]) != 1) return null;
            IReadOnlyList<GeneratedModuleStat>? rolled = null;
            if (parts.Length == 2)
            {
                var matches = modules.Where(m => m.Id == parts[0]).ToArray();
                if (matches.Length != 1 || !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint suffix)
                    || MultiToolProceduralModuleGeneration.UsesHighGradeRoll(matches[0])
                    || MultiToolProceduralModuleGeneration.RollOrdinaryStats(suffix, matches[0], curves,
                        tables.IllegalModuleHighGradeChance) is not { } stats) return null;
                rolled = stats;
            }
            installed.Add(new(id, position, rolled));
            if (slot["eVk"]!.Value<float>() > 0f) damaged.Add(position);
        }
        var charged = special.Where(s => s["Vn8"]?["QA1"]?.Value<string>() == "TechBonus")
            .Select(s => Position(s["3ZH"])).ToArray();
        if (charged.Any(p => p is null)) return null;
        return new(new(new(positions.Max(p => p.X) + 1, positions.Max(p => p.Y) + 1, positions.Length, positions),
            installed, charged.Select(p => p!.Value).ToArray()), damaged);
    }

    /// <summary>The stat-bonus array (@bB) in save order, ids without the leading caret; null when malformed.</summary>
    public static List<KeyValuePair<string, float>>? ReadBonuses(JToken? bonuses)
    {
        if (bonuses is not JArray array) return null;
        var result = new List<KeyValuePair<string, float>>();
        foreach (var bonus in array)
        {
            if (bonus["QL1"]?.Type != JTokenType.String
                || bonus[">MX"]?.Type is not (JTokenType.Float or JTokenType.Integer)) return null;
            float value = bonus[">MX"]!.Value<float>();
            if (!float.IsFinite(value)) return null;
            result.Add(new(bonus["QL1"]!.Value<string>()!.TrimStart('^'), value));
        }
        return result;
    }

    private static GeneratedInventoryPosition? Position(JToken? value) =>
        value?[">Qh"]?.Type == JTokenType.Integer && value["XJ>"]?.Type == JTokenType.Integer
            ? new(value[">Qh"]!.Value<int>(), value["XJ>"]!.Value<int>()) : null;
}
