using System.Collections.Concurrent;

namespace ArtifactX.Core.Universe;

/// <summary>
/// Which body in a system has planetary rings. The game picks exactly one primary planet per
/// system on the solar layout stream (<see cref="SolarLayoutGeneration"/>), not from the planet's
/// own seed: NMS build 184271, 0x141683f50, after the primary planets are placed (three draws each)
/// and each primary host's moon directions are drawn (one draw per host), one more draw
/// (0x14168493c) is scaled by the primary planet count (moons skipped) and that planet's
/// GcPlanetGenerationInputData.HasRings (+0x4D, planet slot +0x21CD) is set
/// (0x14168499e). Child seed 22 only shapes the rings of a planet that already has them
/// (0x1416cf92a); PlanetRingProbability (GcSolarGenerationGlobals +0x254) is not read on this path.
/// Gas-giant systems (system type 4) re-pick over primary and prime bodies after the prime
/// layout (0x141684d96); their layout isn't ported, so they report no rings.
/// Verified 2026-11-08 against the game's Discoveries page for Apolok-Sats (0xB016F9FF9007,
/// Steam slot 1): only Seburydes (body 1) is ringed; the old child-seed-22 test ringed bodies 2, 4
/// and 5 instead.
/// </summary>
public static class PlanetRings
{
    private static readonly ConcurrentDictionary<(ulong, int), int> Cache = new();

    /// <summary>The 1-based body that has rings, or 0 when none is known (gas-giant systems,
    /// systems without planets, invalid input).</summary>
    public static int RingedBody(ulong portalCode, int galaxy) =>
        Cache.GetOrAdd((portalCode, galaxy), key => Compute(key.Item1, key.Item2));

    /// <summary>Whether body <paramref name="planetIndex"/> (from 1) has rings.</summary>
    public static bool HasRings(ulong portalCode, int galaxy, int planetIndex) =>
        planetIndex > 0 && RingedBody(portalCode, galaxy) == planetIndex;

    private static int Compute(ulong portalCode, int galaxy)
    {
        if (galaxy is < 0 or > 255 || ((portalCode >> 32) & 0xFFF) == 0) return 0;
        try
        {
            var attributes = SystemGenerator.Attributes(portalCode, galaxy);
            if (attributes.GasGiant) return 0;
            var bodies = SystemGenerator.Bodies(portalCode, galaxy);
            int primary = Math.Min(attributes.PlanetCount, bodies.Hosts.Length);
            var planets = new List<int>();
            var moons = new int[primary];
            for (int i = 0; i < primary; i++)
            {
                int host = bodies.Hosts[i];
                if (host == 0) planets.Add(i + 1);
                else if (host <= primary) moons[host - 1]++;
            }
            if (planets.Count == 0) return 0;

            ulong address = ((portalCode >> 32) & 0xFFF) << 40
                | (ulong)(uint)galaxy << 32 | (portalCode & 0xFFFFFFFF);
            var rng = SolarLayoutGeneration.RandomFor(address);
            for (int i = 0; i < planets.Count * 3; i++) rng.NextWord();   // angle, elevation, spacing
            foreach (int count in moons)
                if (count > 0) rng.NextWord();                            // moon directions
            return planets[(int)rng.Next((uint)planets.Count)];
        }
        catch
        {
            return 0;
        }
    }
}

