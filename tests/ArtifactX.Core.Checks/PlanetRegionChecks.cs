using System.Numerics;
using ArtifactX.Core.Universe;

/// <summary>
/// PlanetRegions.Seed against the RegionSeed ("ofi") of Save Beacons the game built itself (Steam slot 8, read from the
/// saves 2026-11-10; two placed by the owner while the code was traced): two planet sizes, four systems,
/// positive and negative cube faces.
/// </summary>
internal static class PlanetRegionChecks
{
    public static void Check()
    {
        var beacons = new (string Name, ulong Seed, float Radius, Vector3 Position)[]
        {
            ("Riwor V (owner)", 0x7B89DC37B31051CB, 130048f, new(97617.9375f, 77318.6875f, 39094.68359375f)),
            ("Riwor V (placed 1)", 0x25F52B83E59D71C0, 130048f, new(97275.484375f, 77255.265625f, 39653.734375f)),
            ("Riwor V (placed 2)", 0x4D1624773F218412, 130048f, new(97024.484375f, 77194.3203125f, 40546.30078125f)),
            ("Riwor V (crashed ship)", 0xA2002E18F2476BE1, 130048f, new(97706.140625f, 76957.2421875f, 39378.4375f)),
            ("Goyab 47/E2", 0x0A1E2F0281649242, 130048f, new(-91607.90625f, 80545.6484375f, 45298.23828125f)),
            ("Ribhakyam", 0xC65ED963C88454A2, 98304f, new(38191.625f, 90423.0f, 5717.81298828125f)),
            ("Arkans", 0x540CE87FE07759C4, 98304f, new(65075.38671875f, -491.587890625f, 74054.0f)),
        };
        foreach (var b in beacons)
        {
            ulong got = PlanetRegions.Seed(b.Position, b.Radius);
            if (got != b.Seed) throw new InvalidOperationException($"Region seed {b.Name}: expected {b.Seed:X16}, got {got:X16}");
            if (PlanetBuildingSites.NearestRadius(b.Position.Length()) != b.Radius)
                throw new InvalidOperationException($"Nearest radius {b.Name}");
        }
        Console.WriteLine($"Planet regions: {beacons.Length} game-built beacons' RegionSeeds reproduced exactly OK");
    }
}