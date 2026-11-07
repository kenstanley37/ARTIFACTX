using System.Globalization;
using ArtifactX.Core.Universe;
using Newtonsoft.Json.Linq;

namespace ArtifactX.Core.NmsModels;

/// <summary>Read-only comparison context from one owned ship. Slot 8 inspection on
/// 2026-10-31 confirmed NTx/@EL, PMT slot positions, damage, special cells and cargo
/// intrinsic bonuses against both native reference loadouts. Unsupported equipment
/// returns no result rather than silently omitting its contribution.</summary>
public static class NmsStarshipComparisonPaths
{
    public static StarshipComparisonContext? Read(JObject? ship, StarshipEvaluationTables rules)
    {
        if (ship?["NTx"]?["@EL"] is not JArray seed || seed.Count != 2
            || !ulong.TryParse(seed[1]?.Value<string>()?.Replace("0x", "", StringComparison.OrdinalIgnoreCase),
                NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong resource)
            || ship["PMT"] is not JObject tech || ship[";l5"] is not JObject cargo
            || tech["hl?"] is not JArray unlocked || tech[":No"] is not JArray slots
            || tech["MMm"] is not JArray special || cargo["@bB"] is not JArray bonuses
            || rules.Technology.Definitions is not { } definitions
            || rules.Technology.ProceduralModules is not { } modules
            || rules.Technology.WeightingCurves is not { } curves) return null;
        // Cargo technology requires the native dual-inventory evaluator.
        if (cargo[":No"] is not JArray cargoSlots
            || cargoSlots.Any(s => s["Vn8"]?["elv"]?.Value<string>() == "Technology")) return null;
        GeneratedInventoryPosition? Position(JToken? value) =>
            value?[">Qh"]?.Type == JTokenType.Integer && value["XJ>"]?.Type == JTokenType.Integer
                ? new(value[">Qh"]!.Value<int>(), value["XJ>"]!.Value<int>()) : null;
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
                if (matches.Length != 1 || !uint.TryParse(parts[1], out uint suffix)
                    || MultiToolProceduralModuleGeneration.UsesHighGradeRoll(matches[0])
                    || MultiToolProceduralModuleGeneration.RollOrdinaryStats(suffix, matches[0], curves,
                        rules.Technology.IllegalModuleHighGradeChance) is not { } stats) return null;
                rolled = stats;
            }
            installed.Add(new(id, position, rolled));
            if (slot["eVk"]!.Value<float>() > 0f) damaged.Add(position);
        }
        var charged = special.Where(s => s["Vn8"]?["QA1"]?.Value<string>() == "TechBonus")
            .Select(s => Position(s["3ZH"])).ToArray();
        if (charged.Any(p => p is null)) return null;
        var intrinsic = new List<KeyValuePair<string, float>>();
        foreach (var bonus in bonuses)
        {
            if (bonus["QL1"]?.Type != JTokenType.String
                || bonus[">MX"]?.Type is not (JTokenType.Float or JTokenType.Integer)) return null;
            float value = bonus[">MX"]!.Value<float>();
            if (!float.IsFinite(value)) return null;
            intrinsic.Add(new(bonus["QL1"]!.Value<string>()!.TrimStart('^'), value));
        }
        return new(resource, new(new(positions.Max(p => p.X) + 1, positions.Max(p => p.Y) + 1,
            positions.Length, positions), installed, charged.Select(p => p!.Value).ToArray()), intrinsic, damaged);
    }
}
