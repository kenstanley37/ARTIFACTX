using ArtifactX.Core.NmsModels;

internal static class DockedStationNameChecks
{
    public static void Check()
    {
        (string State, string? Location, string? Expected)[] cases =
        [
            ("InStation", "Aboard The Nudbered VIII Sphere", "The Nudbered VIII Sphere"), // Slot 8, 2026-11-17.
            ("InStation", "Aboard Hybelosso Station Major", "Hybelosso Station Major"),
            ("InNexus", "Aboard the Space Anomaly", null),
            ("AboardFleet", "Aboard Freighter Trinity", null),
            ("InStation", "Aboard ", null),
            ("InStation", null, null),
            ("OnFoot", "Aboard The Nudbered VIII Sphere", null),
        ];
        foreach (var (state, location, expected) in cases)
            if (NmsSpaceStationPaths.DockedStationName(state, location) != expected)
                throw new InvalidOperationException($"Docked station for {state} / \"{location}\" should be {expected ?? "none"}.");
        Console.WriteLine($"PASS: docked station name from state and location ({cases.Length} cases).");
    }
}
