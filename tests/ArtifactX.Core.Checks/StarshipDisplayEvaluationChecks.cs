using System.Text.Json;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using Newtonsoft.Json.Linq;

internal static class StarshipDisplayEvaluationChecks
{
    public static void CheckModelGuards()
    {
        var tables = new InventoryGenerationTables([], [], [], [], [], 0,
            ShipTechnologyOverrides: new() { ["Shuttle"] = false, ["Fighter"] = true,
                ["Hauler"] = null, ["Solar"] = false, ["SentinelInterceptor"] = false });
        if (!StarshipDisplayEvaluation.SupportsNormalModel(SystemStarshipType.Shuttle, tables)
            || new[] { SystemStarshipType.Fighter, SystemStarshipType.Hauler, SystemStarshipType.Explorer,
                SystemStarshipType.Solar, SystemStarshipType.SentinelInterceptor }.Any(t =>
                    StarshipDisplayEvaluation.SupportsNormalModel(t, tables))
            || StarshipDisplayEvaluation.SupportsNormalModel(SystemStarshipType.Shuttle, null))
            throw new InvalidOperationException("Normal ship model guards accepted unresolved or special-category equipment");
    }

    // Local fixture contains extracted game tables and read-only player inventory;
    // never commit it. Expected values are full native reference evaluations.
    public static void CheckNative(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var tables = root.GetProperty("Tables").Deserialize<StarshipEvaluationTables>()!;
        int checks = 0;
        foreach (var row in root.GetProperty("Rows").EnumerateArray())
        {
            var ship = JObject.Parse(row.GetProperty("PlayerShip").GetRawText());
            var context = NmsStarshipComparisonPaths.Read(ship, tables)
                ?? throw new InvalidOperationException("Reference player inventory was rejected");
            var bonuses = row.GetProperty("Bonuses").Deserialize<KeyValuePair<string, float>[]>()!;
            var actual = StarshipDisplayEvaluation.Evaluate(row.GetProperty("Seed").GetUInt64(),
                row.GetProperty("ModelSeed").GetUInt64(), row.GetProperty("ShipClass").GetInt32(),
                row.GetProperty("Rating").GetInt32(), row.GetProperty("Layout").Deserialize<GeneratedMultiToolLayout>()!,
                bonuses, tables, context, row.GetProperty("Wealth").GetInt32(), row.GetProperty("Level").GetInt32(),
                row.GetProperty("Known").EnumerateArray().Select(x => x.GetString()!).ToHashSet(), false)
                ?? throw new InvalidOperationException("Reference evaluation was rejected");
            var expected = row.GetProperty("Expected").Deserialize<StarshipDisplayedStats>()!;
            foreach (var pair in new[] { (actual.Damage, expected.Damage), (actual.Shield, expected.Shield),
                (actual.Hyperdrive, expected.Hyperdrive), (actual.Maneuverability, expected.Maneuverability) })
            {
                if (BitConverter.SingleToInt32Bits(pair.Item1) != BitConverter.SingleToInt32Bits(pair.Item2))
                    throw new InvalidOperationException($"Composed stat mismatch: {pair.Item1:R} != {pair.Item2:R}");
                checks++;
            }
        }
        if (checks == 0) throw new InvalidOperationException("Empty comparison fixture");
        Console.WriteLine($"Composed ship comparison: {checks} exact native float matches");
    }
}
