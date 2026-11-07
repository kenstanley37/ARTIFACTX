using System.Text.Json;
using ArtifactX.Core.Universe;

/// <summary>Final evaluator checks; native fixtures stay local because their flight rules come from game files.</summary>
internal static class StarshipDisplayStatChecks
{
    public static void CheckIntrinsicNative(string path)
    {
        using var vectors = JsonDocument.Parse(File.ReadAllText(path));
        var mappings = vectors.RootElement.GetProperty("Mappings")
            .Deserialize<Dictionary<string, StarshipIntrinsicStatTarget[]>>()!;
        int checks = 0;
        foreach (var row in vectors.RootElement.GetProperty("Rows").EnumerateArray())
        {
            var bonuses = row.GetProperty("Bonuses").Deserialize<KeyValuePair<string, float>[]>()!;
            var actual = StarshipIntrinsicStatGeneration.Apply(row.GetProperty("TechnologyValue").GetSingle(),
                row.GetProperty("StatId").GetInt32(), bonuses, mappings,
                row.GetProperty("UseMultiplierBonus").GetBoolean());
            float expected = row.GetProperty("Expected").GetSingle();
            Require(actual is { } value && BitConverter.SingleToInt32Bits(value) == BitConverter.SingleToInt32Bits(expected),
                $"Intrinsic ship bonus differs: {actual:R}, native {expected:R}");
            checks++;
        }
        Require(checks > 0, "No native intrinsic vectors supplied");
        Console.WriteLine($"PASS: {checks} intrinsic ship stat adjustments match native floats exactly, including repeated targets.");
    }

    public static void CheckSuperchargeNative(string path)
    {
        using var vectors = JsonDocument.Parse(File.ReadAllText(path));
        var rules = vectors.RootElement.GetProperty("Rules").Deserialize<SuperchargedSlotRules>();
        int checks = 0;
        foreach (var row in vectors.RootElement.GetProperty("Rows").EnumerateArray())
        {
            ulong seed = row.GetProperty("Seed").GetUInt64();
            var actual = MultiToolInventoryGeneration.InitialSuperchargedSlots(seed,
                row.GetProperty("Rating").GetInt32(), rules);
            var expected = row.GetProperty("Slots").Deserialize<GeneratedInventoryPosition[]>()!;
            Require(actual is not null && actual.SequenceEqual(expected),
                $"Ship special slots differ from native, seed {seed:X16}");
            checks++;
        }
        Require(checks > 0, "No native ship special-slot vectors supplied");
        Console.WriteLine($"PASS: {checks} initial ship special-slot layouts match native, including coordinate order.");
    }

    public static void CheckInputs()
    {
        Require(StarshipDisplayStatGeneration.ShieldStrength(0f, 0f) is null,
            "A zero shield divisor produced a displayed value");
        Require(StarshipDisplayStatGeneration.HyperdriveRange(float.NaN) is null,
            "Invalid hyperdrive input produced a displayed value");
        Require(StarshipDisplayStatGeneration.DamagePotential(1, 0f, [1f]) is null,
            "Incomplete weapon modes produced damage");
        Require(StarshipDisplayStatGeneration.Maneuverability(1, 6, 3, null, 1f, 1f) is null,
            "Missing extracted flight rules produced maneuverability");
        // Owner's Compare screenshot: Aomoro's Resistance, 2026-10-29.
        // These are supplied evaluated inputs, not a claim about generated loadout support.
        Require(StarshipDisplayStatGeneration.ShieldStrength(74.01944f, .825f)?.ToString("F1",
            System.Globalization.CultureInfo.InvariantCulture) == "305.6", "Recorded shield arithmetic changed");
        Require(StarshipDisplayStatGeneration.HyperdriveRange(189.13568f)?.ToString("F1",
            System.Globalization.CultureInfo.InvariantCulture) == "190.1", "Recorded range arithmetic changed");
    }

    public static void CheckNative(string path)
    {
        using var vectors = JsonDocument.Parse(File.ReadAllText(path));
        var rules = vectors.RootElement.GetProperty("Rules").Deserialize<StarshipFlightRules>()
            ?? throw new InvalidOperationException("Missing extracted flight rules");
        int checks = 0;
        foreach (var row in vectors.RootElement.GetProperty("Rows").EnumerateArray())
        {
            float Number(string key) => row.GetProperty(key).GetSingle();
            ulong seed = row.GetProperty("Seed").GetUInt64();
            void Check(string label, float? actual, string expectedKey)
            {
                float expected = Number(expectedKey);
                Require(actual is { } value && float.IsFinite(value) && float.IsFinite(expected)
                    && BitConverter.SingleToInt32Bits(value) == BitConverter.SingleToInt32Bits(expected),
                    $"{label}, seed {seed:X16}: {actual:R}, native {expected:R}");
                checks++;
            }
            Check("Damage", StarshipDisplayStatGeneration.DamagePotential(seed, Number("DamageBonus"),
                row.GetProperty("WeaponPotentials").EnumerateArray().Select(x => x.GetSingle()).ToArray()), "DamageExpected");
            Check("Shield", StarshipDisplayStatGeneration.ShieldStrength(Number("ShieldBonus"), Number("ShieldStat")),
                "ShieldExpected");
            Check("Range", StarshipDisplayStatGeneration.HyperdriveRange(Number("JumpDistance")), "RangeExpected");
            Check("Maneuverability", StarshipDisplayStatGeneration.Maneuverability(seed,
                row.GetProperty("ShipClass").GetInt32(), row.GetProperty("Rating").GetInt32(), rules,
                Number("PulseSpeed"), Number("BoostManeuverability")), "ManeuverabilityExpected");
        }
        Require(checks > 0, "No native vectors supplied");
        CheckInputs();
        Console.WriteLine($"PASS: {checks} final ship stat values match native floats exactly; generated loadouts are outside this check.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
