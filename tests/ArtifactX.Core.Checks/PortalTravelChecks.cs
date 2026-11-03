using ArtifactX.Core.Universe;

internal static class PortalTravelChecks
{
    public static void Check()
    {
        (string? Code, bool RequiresAccess)[] cases =
        [
            ("64275B6DA013", true), // Owner's purple-star portal test.
            ("102E5B6DA013", false), // The ordinary system reached without access.
            ("64275b6da013", true),
            ("03E800000000", true),
            ("042800000000", true),
            ("03E700000000", false),
            ("042900000000", false),
            ("FFFF00000000", false),
            ("6427ZB6DA013", false),
            ("64275B6DA01", false),
            ("64275B6DA0130", false),
            (" 4275B6DA013", false),
            ("", false),
            (null, false),
        ];
        foreach (var (code, expected) in cases)
            if (PortalTravel.RequiresPurpleStarAccess(code) != expected)
                throw new InvalidOperationException($"Wrong portal requirement for '{code ?? "null"}'.");
        Console.WriteLine($"PASS: {cases.Length} portal travel checks.");
    }
}
