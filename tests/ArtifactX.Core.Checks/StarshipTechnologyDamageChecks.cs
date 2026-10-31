using System.Text.Json;
using ArtifactX.Core.Universe;

internal static class StarshipTechnologyDamageChecks
{
    public static void CheckInputs()
    {
        foreach (float chance in new[] { float.NaN, float.PositiveInfinity, -.1f, 1.1f })
            if (StarshipTechnologyDamageGeneration.InitialDamage(1, ["SHIPSHIELD"], chance) is not null)
                throw new InvalidOperationException("Invalid damage chance produced flags");
        if (StarshipTechnologyDamageGeneration.InitialDamage(1, [""], .5f) is not null)
            throw new InvalidOperationException("Empty technology ID produced flags");
        if (StarshipTechnologyDamageGeneration.InitialDamage(1, ["SHIPJUMP1", "HYPERDRIVE", "SHIPSHIELD"], 0f)
            is not [true, true, false])
            throw new InvalidOperationException("Forced damage or zero-chance handling changed");
    }

    public static void CheckShieldNative(string path)
    {
        using var vectors = JsonDocument.Parse(File.ReadAllText(path));
        var root = vectors.RootElement;
        var rules = root.GetProperty("Rules").Deserialize<MultiToolScannerRules>()!;
        var stats = root.GetProperty("Stats").Deserialize<GeneratedModuleStat[]>()!;
        var parents = root.GetProperty("Parents").Deserialize<Dictionary<int, int>>()!;
        var definition = new TechnologySelectionDefinition("SHIPSHIELD", "SHIPSHIELD", 6, 0, "", true,
            root.GetProperty("BaseStat").GetInt32(), stats.Select(s => s.Stat).ToArray(), stats,
            root.GetProperty("Group").GetString());
        var tables = new MultiToolTechnologyTables([], [], Definitions: [definition],
            StatParents: parents, ScannerRules: rules);
        int checks = 0;
        foreach (var row in root.GetProperty("Rows").EnumerateArray())
        {
            // Compare passes false: include adjacency, subject to damage.
            if (row.GetProperty("ignoreAdjacency").GetBoolean()) continue;
            GeneratedInventoryPosition[] Positions(string key) => row.GetProperty(key).EnumerateArray()
                .Select(p => new GeneratedInventoryPosition(p[0].GetInt32(), p[1].GetInt32())).ToArray();
            var positions = Positions("positions");
            var damaged = row.GetProperty("damaged").EnumerateArray().Select(x => x.GetBoolean()).ToArray();
            var inventory = new GeneratedMultiToolInventory(new(10, 3, positions.Length, positions),
                positions.Select(p => new GeneratedInstalledTechnology("SHIPSHIELD", p)).ToArray(), Positions("charged"));
            var actual = MultiToolStatGeneration.Evaluate(inventory, tables, 143, MultiToolStatCombination.Add,
                damagedTechnologyPositions: positions.Where((_, i) => damaged[i]).ToHashSet(),
                includeDamagedTechnology: !row.TryGetProperty("excludeDamaged", out var exclude) || !exclude.GetBoolean());
            float expected = row.GetProperty("value").GetSingle();
            if (actual is null || BitConverter.SingleToInt32Bits(actual.Value) != BitConverter.SingleToInt32Bits(expected))
                throw new InvalidOperationException($"Shield damage/adjacency differs: {actual:R}, native {expected:R}");
            checks++;
        }
        if (checks == 0) throw new InvalidOperationException("No native shield vectors supplied");
        Console.WriteLine($"PASS: {checks} shield damage, adjacency and supercharge totals match native floats exactly.");
    }

    public static void CheckNative(string path)
    {
        CheckInputs();
        using var vectors = JsonDocument.Parse(File.ReadAllText(path));
        int checks = 0;
        foreach (var row in vectors.RootElement.EnumerateArray())
        {
            ulong seed = row.GetProperty("Seed").GetUInt64();
            var ids = row.GetProperty("Ids").EnumerateArray().Select(x => x.GetString()!).ToArray();
            var expected = row.GetProperty("Damaged").EnumerateArray().Select(x => x.GetBoolean()).ToArray();
            var actual = StarshipTechnologyDamageGeneration.InitialDamage(seed, ids,
                row.GetProperty("Chance").GetSingle());
            if (actual is null || !actual.SequenceEqual(expected))
                throw new InvalidOperationException($"Technology damage differs from native, seed {seed:X16}");
            checks += expected.Length;
        }
        if (checks == 0) throw new InvalidOperationException("No native technology damage vectors supplied");
        Console.WriteLine($"PASS: {checks} initial technology damage flags match native; adjacency is outside this check.");
    }
}
