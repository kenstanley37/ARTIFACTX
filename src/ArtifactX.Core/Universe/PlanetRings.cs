using System.Collections.Concurrent;

namespace ArtifactX.Core.Universe;

/// <summary>
/// Which body in a system has planetary rings. The game picks one planet per system on the solar
/// layout stream (<see cref="SolarLayoutGeneration"/>), not from the planet's own seed: NMS build
/// 184271, 0x141683f50, draws the initial orbit angle (0x141683fff), three words per primary
/// planet (increment 0x141684460, elevation 0x1416845b7, spacing 0x14168474a) and one word per
/// primary host with moons (0x141684820), then draws one more word (0x141684941) scaled by the
/// primary planet count (moons skipped) and sets that planet's
/// GcPlanetGenerationInputData.HasRings (+0x4D, planet slot +0x21CD) at 0x14168499e.
/// Child seed 22 only shapes the rings of a planet that already has them (0x1416cf92a).
/// Verified 2026-11-08 against the game's Discoveries page on Steam slot 1:
///   - Apolok-Sats (0xB016F9FF9007, 3 primary + 2 prime): body 1 (Seburydes) has rings.
///   - Upusichrot (0x0EA0205003FFA, 3 primary + 1 prime): body 2 (Edmo IX) has rings.
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
            rng.NextWord();                                               // initial orbit angle (0x141683fff)
            for (int i = 0; i < planets.Count * 3; i++) rng.NextWord();   // increment, elevation, spacing
            foreach (int count in moons)
                if (count > 0) rng.NextWord();                            // moon directions
            int ringed = planets[(int)rng.Next((uint)planets.Count)];

            if (attributes.Star == StarColour.Purple && attributes.PrimePlanetCount > 0)
            {
                int total = Math.Min(primary + attributes.PrimePlanetCount, bodies.Hosts.Length);
                int allPlanets = planets.Count;
                for (int i = primary; i < total; i++)
                    if (bodies.Hosts[i] == 0) allPlanets++;
                int pick = (int)rng.Next((uint)allPlanets);
                ringed = pick < planets.Count ? planets[pick] : 0;
            }
            return ringed;
        }
        catch
        {
            return 0;
        }
    }
}
