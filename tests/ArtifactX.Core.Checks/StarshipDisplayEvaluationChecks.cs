using System.Text.Json;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using Newtonsoft.Json.Linq;

internal static class StarshipDisplayEvaluationChecks
{
    public static void CheckModelGuards()
    {
        var incomplete = new MultiToolTechnologyTables([], []);
        if (StarshipTechnologyGeneration.ForShipClass(8, incomplete) is not null
            || StarshipTechnologyGeneration.ForShipClass(9, incomplete) is not null)
            throw new InvalidOperationException("Missing Solar metadata or Sentinel category was accepted");
        var tables = new InventoryGenerationTables([], [], [], [], [], 0,
            ShipTechnologyOverrides: new() { ["Shuttle"] = false, ["Fighter"] = true,
                ["Hauler"] = null, ["Solar"] = false, ["SentinelInterceptor"] = false });
        if (!StarshipDisplayEvaluation.SupportsComparisonModel(SystemStarshipType.Shuttle, tables)
            || !StarshipDisplayEvaluation.SupportsComparisonModel(SystemStarshipType.Solar, tables)
            || !StarshipDisplayEvaluation.SupportsComparisonModel(SystemStarshipType.SentinelInterceptor, tables)
            || new[] { SystemStarshipType.Fighter, SystemStarshipType.Hauler, SystemStarshipType.Explorer }.Any(t =>
                    StarshipDisplayEvaluation.SupportsComparisonModel(t, tables))
            || StarshipDisplayEvaluation.SupportsComparisonModel(SystemStarshipType.Shuttle, null))
            throw new InvalidOperationException("Normal ship model guards accepted unresolved or special-category equipment");
    }

    /// <summary>Owned-ship model scenes (NTx.93M, as saved on Slot 8) map to native ship classes.</summary>
    public static void CheckOwnedShipClasses()
    {
        (string Scene, int? Class)[] cases =
        [
            ("MODELS/COMMON/SPACECRAFT/DROPSHIPS/DROPSHIP_PROC.SCENE.MBIN", 1),
            ("MODELS/COMMON/SPACECRAFT/FIGHTERS/FIGHTER_PROC.SCENE.MBIN", 2),
            ("MODELS/COMMON/SPACECRAFT/SCIENTIFIC/SCIENTIFIC_PROC.SCENE.MBIN", 3),
            ("MODELS/COMMON/SPACECRAFT/SHUTTLE/SHUTTLE_PROC.SCENE.MBIN", 4),
            ("MODELS/COMMON/SPACECRAFT/S-CLASS/S-CLASS_PROC.SCENE.MBIN", 6),
            ("MODELS/COMMON/SPACECRAFT/S-CLASS/BIOPARTS/BIOSHIP_PROC.SCENE.MBIN", 7),
            ("MODELS/COMMON/SPACECRAFT/SAILSHIP/SAILSHIP_PROC.SCENE.MBIN", 8),
            ("MODELS/COMMON/SPACECRAFT/SENTINELSHIP/SENTINELSHIP_PROC.SCENE.MBIN", 9),
            ("MODELS/COMMON/SPACECRAFT/BIGGS/BIGGS.SCENE.MBIN", null),
            ("", null),
        ];
        foreach (var (scene, expected) in cases)
            if (NmsStarshipComparisonPaths.NativeShipClass(scene) != expected)
                throw new InvalidOperationException($"Owned ship class for {scene}: expected {expected?.ToString() ?? "none"}");
    }

    /// <summary>Freighter panel arithmetic (0x1406BAD90): jump distance + 1, and the six fleet multipliers averaged x 20,
    /// with intrinsic bonuses applied as percentages (Fuel less-is-better). Synthetic tables, not game data.</summary>
    public static void CheckFreighterPanel()
    {
        int[] fleet = [176, 177, 178, 179, 180, 181];
        var parents = new Dictionary<int, int> { [170] = 170, [171] = 170 };
        foreach (int f in fleet) parents[f] = f;
        var tables = new MultiToolTechnologyTables([], [],
            [new("DRIVE", "DRIVE", 0, 0, "", true, 170, [171], [new(171, 100f, 1)], "")],
            StatParents: parents, ScannerRules: new(0, 0f, 1.25f, 0.04f, 0.03f, 3, 0.06f, 0.05f));
        var rules = new StarshipEvaluationTables(tables, new([], []), new()
            {
                ["FREI_HYPERDRIVE"] = [new(171, false)],
                ["FREI_FLEET"] = fleet.Select(f => new StarshipIntrinsicStatTarget(f, f == 177)).ToArray(),
            }, true, new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), [], new(0, 0, 0), 0f, Freighter: new(171, fleet));
        var cell = new GeneratedInventoryPosition(0, 0);
        var inventory = new GeneratedMultiToolInventory(new(1, 1, 1, [cell]), [new("DRIVE", cell)], []);
        var none = FreighterDisplayEvaluation.Evaluate(inventory, new HashSet<GeneratedInventoryPosition>(), [], rules);
        // 10% hyperdrive bonus: 100 + 10 = 110 (+1); 10% fleet bonus: five stats 1.1, Fuel 0.9 -> mean 1.0667 x 20.
        var boosted = FreighterDisplayEvaluation.Evaluate(inventory, new HashSet<GeneratedInventoryPosition>(),
            [new("FREI_HYPERDRIVE", 10f), new("FREI_FLEET", 10f)], rules);
        if (none is not { Hyperdrive: 101f, FleetCoordination: 20f } || boosted is null
            || Math.Abs(boosted.Hyperdrive - 111f) > 1e-3f || Math.Abs(boosted.FleetCoordination - (5 * 1.1f + 0.9f) / 6f * 20f) > 1e-3f
            || FreighterDisplayEvaluation.Evaluate(inventory, new HashSet<GeneratedInventoryPosition>(), [], rules with { Freighter = null }) is not null)
            throw new InvalidOperationException($"Freighter panel arithmetic: {none} / {boosted}");
        Console.WriteLine("PASS: freighter panel - hyperdrive + 1, fleet multipliers averaged x 20, intrinsic bonuses, missing ids rejected.");
    }

    /// <summary>includeAdjacency=false (corvette weapons in Damage Potential) drops the adjacency bonus of two
    /// adjacent cannon technologies but keeps supercharge. Synthetic tables, not game data.</summary>
    public static void CheckAdjacencySwitch()
    {
        var parents = new Dictionary<int, int> { [119] = 119 };
        var tables = new MultiToolTechnologyTables([], [],
            [new("GUN", "GUN", 0, 0, "", true, 7, [119], [new(119, 100f, 1)], ""),
             new("GUN_UP", "GUN_UP", 0, 0, "", false, 7, [119], [new(119, 20f, 1)], "")],
            StatParents: parents, ScannerRules: new(0, 0f, 1.25f, 0.04f, 0.03f, 3, 0.06f, 0.05f));
        GeneratedInventoryPosition a = new(0, 0), b = new(1, 0);
        var inventory = new GeneratedMultiToolInventory(new(2, 1, 2, [a, b]), [new("GUN", a), new("GUN_UP", b)], [b]);
        float? with = MultiToolStatGeneration.Evaluate(inventory, tables, 119, MultiToolStatCombination.Add);
        float? without = MultiToolStatGeneration.Evaluate(inventory, tables, 119, MultiToolStatCombination.Add, includeAdjacency: false);
        // Supercharged upgrade 20 x 1.25 = 25, plus the cannon's 100; only the adjacent evaluation adds a bonus.
        if (without is not { } w || Math.Abs(w - 125f) > 1e-3f || with is not { } x || x <= 125.5f)
            throw new InvalidOperationException($"Adjacency switch: with {with}, without {without}");
        Console.WriteLine("PASS: stat evaluation without adjacency keeps supercharge and drops the adjacency bonus.");
    }

    /// <summary>Freighter-template modules roll two draws earlier than others (module generator 0x140EE20D0). Three
    /// Slot 8 UP_FRHYP4 modules showed 256 / 276 / 309 ly in game, supercharged (x1.25). Ranges as in the 2026-11
    /// game data (jump 200-250 on curve index 1, warp efficiency 1-1); the same module without the flag rolls
    /// differently, so the flag is what's checked.</summary>
    public static void CheckFreighterModuleRolls()
    {
        int[] curves = [0, 16, 19, 25, 17, 20, 26];
        var module = new ProceduralModuleDefinition("UP_FRHYP4", 3, 2, 2, 0,
            [new(171, 200f, 250f, 1, false), new(172, 1f, 1f, 0, false)], FreighterTemplate: true);
        foreach (var (suffix, shown, jumpFirst) in new[] { (18794u, 256, false), (30348u, 276, false), (17621u, 309, true) })
        {
            var stats = MultiToolProceduralModuleGeneration.RollOrdinaryStats(suffix, module, curves)
                ?? throw new InvalidOperationException("Freighter module roll rejected");
            float jump = stats.First(s => s.Stat == 171).Value;
            if ((int)MathF.Round(jump * 1.25f) != shown || (stats[0].Stat == 171) != jumpFirst)
                throw new InvalidOperationException($"UP_FRHYP4#{suffix}: {jump * 1.25f} supercharged, expected {shown}");
        }
        var notFreighter = MultiToolProceduralModuleGeneration.RollOrdinaryStats(18794u, module with { FreighterTemplate = false }, curves)!;
        if ((int)MathF.Round(notFreighter.First(s => s.Stat == 171).Value * 1.25f) == 256)
            throw new InvalidOperationException("Freighter flag had no effect");
        Console.WriteLine("PASS: freighter upgrade modules roll as in game (256 / 276 / 309 ly supercharged).");
    }

    // Local fixture contains extracted game tables and read-only player inventory;
    // never commit it. Expected values are native floats unless the fixture explicitly
    // requests comparison at the game screenshots' one-decimal display precision.
    public static void CheckNative(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var tables = root.GetProperty("Tables").Deserialize<StarshipEvaluationTables>()!;
        bool displayed = root.TryGetProperty("CompareDisplayedValues", out var displayMode) && displayMode.GetBoolean();
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
                bool matches = displayed
                    ? pair.Item1.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                        == pair.Item2.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                    : BitConverter.SingleToInt32Bits(pair.Item1) == BitConverter.SingleToInt32Bits(pair.Item2);
                if (!matches)
                    throw new InvalidOperationException($"Composed stat mismatch: {pair.Item1:R} != {pair.Item2:R}");
                checks++;
            }
        }
        if (checks == 0) throw new InvalidOperationException("Empty comparison fixture");
        Console.WriteLine($"Composed ship comparison: {checks} {(displayed ? "displayed screenshot values" : "exact native floats")} match");
    }
}
