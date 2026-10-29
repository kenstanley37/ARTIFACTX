using System.Numerics;

namespace ArtifactX.Core.Universe;

/// <summary>
/// Where a system's starship pool starts in its generation stream, and which pool ship lies at a crashed-ship site -
/// NMS build 183791 (Forsaken 7.10), docs/research/CRASHED_SHIPS.md. Traced 2026-10-28/29 with owner-approved
/// observe-only hooks on the Slot 8 test save and read-only memory reads.
/// </summary>
public static class SystemShipPool
{
    private const int LocatorStart = 38;     // the builder's second stream reset: 10 draws, then the step's fixed 28
    private const int AfterLocators = 3;     // draws between the locator step and the first ship
    private const float LocatorRadius = 500f;                  // 0x144BED144
    private const int Shuttle = 4, Sail = 8;
    private static readonly float TwoPi = TerrainNoise.Bits(0x40C90FDB);
    private const ulong HashMultiplier = 0x9DDFEA08EB382D69UL;

    /// <summary>
    /// The number of draws before the first pool ship: the locator step 0x141684990 scatters 30-65 locators around each
    /// primary body (moons included, prime bodies not), 4 draws each, and one more for every locator that doesn't fall
    /// within 500 + 1.5 x shell radius of any body. Checked 8/8 systems against the running game (2026-10-29).
    /// Null without the catalog's ship pool settings.
    /// </summary>
    public static int? PoolStart(SolarLayoutGeneration.SystemLayout layout, SolarLayoutTables tables)
    {
        if (tables.ShipPool is not { } pool) return null;
        var rng = SolarLayoutGeneration.RandomFor(layout.Address);
        for (int i = 0; i < LocatorStart; i++) rng.NextWord();
        int count = layout.Centers.Length;
        var radius = new float[count];
        for (int i = 0; i < count; i++)
        {
            float shell = SolarLayoutGeneration.ShellRadius(layout.Sizes[i], tables.MassiveSolarSystems);
            radius[i] = shell * 0.5f + shell;
        }
        int draws = LocatorStart;
        int span = pool.LocatorScatterMaxCount - pool.LocatorScatterMinCount + 1;
        for (int p = 0; p < layout.PrimaryCount; p++)
        {
            int locators = (int)(((ulong)rng.NextWord() * (ulong)span) >> 32) + pool.LocatorScatterMinCount;
            draws++;
            float near = radius[p], range = near + pool.LocatorScatterMaxDistanceFromPlanet - near;
            var centre = layout.Centers[p];
            for (int k = 0; k < locators; k++)
            {
                float u1 = Fraction(rng.NextWord()), u2 = Fraction(rng.NextWord()), u3 = Fraction(rng.NextWord());
                rng.NextWord();                                   // its type (capital ship / plain)
                draws += 4;
                float y = 1f - (u1 + u1);
                float r = MathF.Sqrt(MathF.Max(0f, 1f - y * y));
                float phi = u2 * TwoPi;
                float distance = u3 * range + near;
                var at = new Vector3(GameTrig.Cos(phi) * r * distance + centre.X, y * distance + centre.Y,
                    GameTrig.Sin(phi) * r * distance + centre.Z);
                bool blocked = false;
                for (int j = 0; j < count && !blocked; j++)
                {
                    float reach = LocatorRadius + radius[j];
                    blocked = reach * reach >= Vector3.DistanceSquared(layout.Centers[j], at);
                }
                if (!blocked) { rng.NextWord(); draws++; }            // pirate chance
            }
        }
        return draws + AfterLocators;
    }

    /// <summary>
    /// The pool ship lying at a crashed-ship (DistressSignal) site, picked by 0x1417550E0 from the site key: a stream
    /// seeded with the key draws a class by the race's SpaceshipWeightings row; a Shuttle (or any class in a pirate
    /// system) may become a Sail on a second stream keyed by Hash(key, system address); the next draw picks one of the
    /// pool's ships of that class. Checked 3/3 against crashed ships opened in game (Ribhakyam, 2026-10-29: Final
    /// Eokak III, Exhausted Song LK1, Iwada's Swift Pathfinder). Null when the catalog lacks the race's row or no ship fits.
    /// </summary>
    /// <param name="pool">The system's pool (<see cref="SystemStarshipGenerator.GenerateForSystem"/>); its standard ships take part.</param>
    /// <param name="systemAddress">The packed system address (SolarLayoutGeneration.SystemLayout.Address).</param>
    public static SystemStarship? CrashedShip(ulong siteKey, IReadOnlyList<SystemStarship> pool, SystemRace race, bool pirate,
        ulong systemAddress, ShipPoolSettings settings)
    {
        // GcAlienRace rows: Traders 0, Warriors 1, Explorers 2 ... None 7 (uninhabited systems)
        int row = race switch { SystemRace.Gek => 0, SystemRace.Vykeen => 1, SystemRace.Korvax => 2, _ => 7 };
        if (row >= settings.ClassWeightings.Length) return null;
        var weights = settings.ClassWeightings[row];
        var rng = SolarLayoutGeneration.RandomFor(siteKey);
        float total = 0f;
        foreach (float w in weights) total += w;
        float x = total * Fraction(rng.NextWord());
        int cls = weights.Length;
        for (int i = 0; i < weights.Length; i++)
        {
            if (x > weights[i]) { x -= weights[i]; continue; }
            cls = i;
            break;
        }
        if (pirate || cls == Shuttle)
        {
            float chance = pirate && cls == Shuttle ? settings.SailChancePirateSystem : settings.SailChance;
            ulong h = (systemAddress ^ siteKey) * HashMultiplier;
            h ^= h >> 47;
            h ^= systemAddress;
            h *= HashMultiplier;
            h ^= h >> 47;
            h *= HashMultiplier;
            if (chance > Fraction(SolarLayoutGeneration.RandomFor(h).NextWord())) cls = Sail;
        }
        var candidates = pool.Where(s => s.IsStandardStarship && (int)s.Type == cls).ToList();
        if (candidates.Count == 0) return null;
        return candidates[(int)(((ulong)rng.NextWord() * (ulong)candidates.Count) >> 32)];
    }

    /// <summary>
    /// A crashed ship's class, C=0/B=1/A=2/S=3: the game's inventory class roll (0x1404D8C00 in Steam build 26295378)
    /// seeded with the site key itself, on the system's GcInventoryTable class row (wealth Low/Medium/High; the pirate
    /// row in pirate systems - not checked in game yet). The class therefore goes with the site, and since the same key
    /// picks the ship's type, a pool ship tends to come out in the same class wherever it lies. Hook 2026-10-29 (Slot 8,
    /// observe-only): the key going into the pick was the seed of the class roll; checked 5/5 on Ribhakyam (medium
    /// wealth: C, C, A, A, C). Null without the catalog's class rows.
    /// </summary>
    public static int? CrashedShipClass(ulong siteKey, int wealth, bool pirate, InventoryGenerationTables? tables) =>
        MultiToolSiteGeneration.InventoryClass(siteKey, pirate ? 3 : Math.Clamp(wealth - 1, 0, 2), tables);

    private static float Fraction(uint word) => (float)(word * 2.3283064370807974e-10);
}
