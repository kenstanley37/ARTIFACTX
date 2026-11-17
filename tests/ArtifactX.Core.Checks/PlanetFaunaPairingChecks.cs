using ArtifactX.Core.Universe;

internal static class PlanetFaunaPairingChecks
{
    /// <summary>Discoveries pair with predicted creatures by seed first, whatever their order; unpredicted ones
    /// (older game versions) fill the remaining slots in order; any beyond the predicted list get their own card.</summary>
    public static void Check()
    {
        PredictedCreature[] predicted =
        [
            new("BIRD", "air", 5, 0xA),
            new("COW", "ground", 3, 0xB),
            new("FISH", "water", 6, 0xC),
        ];

        // Discovery order is not predicted order: COW's seed first, then an old discovery, then BIRD's.
        var slots = PlanetFaunaPairing.Pair(predicted, [0xB, 0x99, 0xA]);
        Expect(slots.Count == 3, "three slots");
        Expect(slots[0] == new PlanetFaunaPairing.Slot(predicted[0], 2), "BIRD gets its own discovery");
        Expect(slots[1] == new PlanetFaunaPairing.Slot(predicted[1], 0), "COW gets its own discovery");
        Expect(slots[2] == new PlanetFaunaPairing.Slot(predicted[2], 1), "the unpredicted discovery fills FISH's slot");

        slots = PlanetFaunaPairing.Pair(predicted, [0xC]);
        Expect(slots[0].Discovery == -1 && slots[1].Discovery == -1 && slots[2].Discovery == 0, "only FISH discovered");

        slots = PlanetFaunaPairing.Pair(predicted[..1], [0x98, 0xA]);
        Expect(slots.Count == 2 && slots[0].Discovery == 1 && slots[1] == new PlanetFaunaPairing.Slot(null, 0),
            "a discovery beyond the predicted list gets its own card");

        Console.WriteLine("PASS: planet creature discoveries pair with predictions by seed, the rest in order.");
    }

    private static void Expect(bool condition, string what)
    {
        if (!condition) throw new InvalidOperationException("Planet fauna pairing: " + what);
    }
}
