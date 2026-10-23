using ArtifactX.Core.Procedural;

namespace ArtifactX.Core.Universe;

/// <summary>
/// Starship and fleet vehicle classes as generated in system pools.
/// Values match NMS.exe's GcSpaceshipClasses enum:
/// 0=Freighter, 1=Dropship (Hauler), 2=Fighter, 3=Scientific (Explorer), 4=Shuttle,
/// 6=Royal (Exotic), 8=Sail (Solar), 9=Robot (Sentinel Interceptor), 10=Corvette (Pirate Dreadnought).
/// </summary>
public enum SystemStarshipType
{
    Freighter = 0,
    Hauler = 1,
    Fighter = 2,
    Explorer = 3,
    Shuttle = 4,
    Exotic = 6,
    Solar = 8,
    SentinelInterceptor = 9,
    PirateDreadnought = 10,
}

/// <summary>
/// A starship or fleet vehicle generated in a system's procedural pool.
/// </summary>
/// <param name="Index">1-based vehicle index in the system pool (1 to 50).</param>
/// <param name="Type">The vehicle class (Hauler, Fighter, Explorer, Shuttle, Exotic, Solar, Sentinel, Freighter, Dreadnought).</param>
/// <param name="Seed">The 64-bit seed determining model parts, accessories, and colors.</param>
/// <param name="DefaultName">The default procedural name the game assigns to this vehicle.</param>
/// <param name="Category">Vehicle category (Starship, Sentinel, Freighter, Dreadnought).</param>
public sealed record SystemStarship(
    int Index,
    SystemStarshipType Type,
    ulong Seed,
    string DefaultName,
    string Category = "Starship")
{
    public string TypeLabel => Type switch
    {
        SystemStarshipType.Freighter => "Freighter",
        SystemStarshipType.Hauler => "Hauler",
        SystemStarshipType.Fighter => "Fighter",
        SystemStarshipType.Explorer => "Explorer",
        SystemStarshipType.Shuttle => "Shuttle",
        SystemStarshipType.Exotic => "Exotic",
        SystemStarshipType.Solar => "Solar",
        SystemStarshipType.SentinelInterceptor => "Sentinel",
        SystemStarshipType.PirateDreadnought => "Dreadnought",
        _ => Type.ToString(),
    };

    public string SeedHex => $"0x{Seed:X16}";

    public bool IsStandardStarship => Index <= 21;
    public bool IsSentinel => Type == SystemStarshipType.SentinelInterceptor;
    public bool IsFreighter => Type == SystemStarshipType.Freighter;
    public bool IsDreadnought => Type == SystemStarshipType.PirateDreadnought;
}

/// <summary>
/// Generates the fixed pool of native starships and fleet vehicles native to any star system in No Man's Sky.
///
/// Reverse-engineered from NMS.exe (build 180836, 2026-10-22):
/// - System starship routine at 0x14164a4a0 (loop at 0x14164adc0 to 0x14164b220).
/// - Reseeds the universe PRNG from portal address, advances exactly 41 LCG/MWC steps.
/// - System pool holds 50 total vehicles:
///   - 1..21: Standard starships (7 specialty, 3 each of other two, 7 shuttles/solars, 1 exotic).
///   - 22..27: 6 System Freighters (seeds for the civilian freighter fleet models).
///   - 28..35: 8 System Frigates.
///   - 36: Sentinel Interceptor (Robot class, category Sentinel) native to this system!
///   - 37: Sentinel Capital Ship.
///   - 38..42: Pirate Fighter Squadron (5 fighters).
///   - 43..48: Frigates and Freighters.
///   - 49: Swarm Drone.
///   - 50: Pirate Dreadnought (Corvette class, extra 7) native to this system!
/// - Each seed is generated from 2 PRNG draws mixed through the 64-bit mixer (Mix64).
/// - Names are generated via ShipNames from the game's word lists.
/// - Verified 100% bit-exact against Unicorn emulator running NMS.exe across multiple star systems.
/// </summary>
public static class SystemStarshipGenerator
{
    private const ulong ConstA = 0x64DD81482CBD31D7UL;
    private const ulong ConstB = 0xE36AA5C613612997UL;
    private const ulong HashConst = 0x9DDFEA08EB382D69UL;
    private const double TinyDouble = 2.3283064370807974e-10;

    private static ulong Mix64(ulong register)
    {
        register = ((register >> 33) ^ register) * ConstA;
        register = ((register >> 33) ^ register) * ConstB;
        return (register >> 33) ^ register;
    }

    /// <summary>
    /// Generates native starships for the system with the given portal code and attributes.
    /// By default includes the 21 standard starships (plus the native Sentinel Interceptor #36 when
    /// <paramref name="includeSentinel"/> is true). Set <paramref name="includeFleet"/> to true to also
    /// include the 6 system freighters and pirate dreadnought.
    /// </summary>
    public static IReadOnlyList<SystemStarship> Generate(
        ulong portalCode,
        SystemRace race,
        bool isPirate,
        Func<string, IReadOnlyList<string>?>? words = null,
        bool includeFleet = false,
        bool includeSentinel = false)
    {
        uint lo = (uint)portalCode;
        uint hi = (uint)(portalCode >> 32);
        uint s0 = lo == 0 ? 1 : lo;
        uint s1 = ((lo >> 16) | (lo << 16)) ^ hi ^ lo;

        var rng = new UniverseRandom(((ulong)s1 << 32) | s0);

        // Advance exactly 41 LCG steps to reach the start of the starship pool loop
        for (int i = 0; i < 41; i++)
            rng.Step();

        // Ship counts per class based on system race
        int haulerCount = 3, fighterCount = 3, explorerCount = 3, shuttleCount = 7;
        switch (race)
        {
            case SystemRace.Gek:
                haulerCount = 7;
                break;
            case SystemRace.Vykeen:
                fighterCount = 7;
                break;
            case SystemRace.Korvax:
                explorerCount = 7;
                break;
            default: // Uncharted or None: equal distribution
                haulerCount = 5;
                fighterCount = 5;
                explorerCount = 5;
                shuttleCount = 5;
                break;
        }

        var ships = new List<SystemStarship>(includeFleet ? 29 : 21);
        int shipIndex = 1;

        void AddShips(SystemStarshipType baseType, int count)
        {
            for (int i = 0; i < count; i++)
            {
                uint low = rng.NextWord();
                uint high = rng.NextWord();
                ulong seed = Mix64(((ulong)high << 32) | low);

                SystemStarshipType shipType = baseType;

                // Solar ship override check (0x14164af50 - 0x14164b00e)
                float threshold = 0f;
                if (isPirate)
                {
                    threshold = baseType == SystemStarshipType.Shuttle ? 0.85f : 0.10f;
                }
                else if (baseType == SystemStarshipType.Shuttle)
                {
                    threshold = 0.10f;
                }

                if (threshold > 0f)
                {
                    ulong hash = (portalCode ^ seed) * HashConst;
                    hash ^= hash >> 47;
                    hash ^= portalCode;
                    hash *= HashConst;
                    hash ^= hash >> 47;
                    hash *= HashConst;

                    uint r8d = (uint)hash;
                    uint rax = (uint)(hash >> 32);
                    uint ecx = ((r8d >> 16) | (r8d << 16)) ^ rax ^ r8d;
                    uint lcg = r8d == 0 ? 1 : r8d;
                    lcg = lcg * 0x5A76F899 + ecx;
                    float roll = (float)((double)lcg * TinyDouble);

                    if (threshold > roll)
                        shipType = SystemStarshipType.Solar;
                }

                string name = words != null ? ShipNames.Generate(seed, words) ?? $"Starship {seed:X16}" : $"Starship {seed:X16}";
                ships.Add(new SystemStarship(shipIndex++, shipType, seed, name, "Starship"));
            }
        }

        AddShips(SystemStarshipType.Hauler, haulerCount);
        AddShips(SystemStarshipType.Fighter, fighterCount);
        AddShips(SystemStarshipType.Explorer, explorerCount);
        AddShips(SystemStarshipType.Shuttle, shuttleCount);

        // Ship #21: Exotic (Royal) - 0x14164b0e0
        {
            uint low = rng.NextWord();
            uint high = rng.NextWord();
            ulong seed = Mix64(((ulong)high << 32) | low);
            string name = words != null ? ShipNames.Generate(seed, words) ?? $"Starship {seed:X16}" : $"Starship {seed:X16}";
            ships.Add(new SystemStarship(shipIndex++, SystemStarshipType.Exotic, seed, name, "Starship"));
        }

        if (!includeFleet && !includeSentinel)
            return ships;

        // Ships #22..#27: 6 System Freighters
        for (int i = 0; i < 6; i++)
        {
            uint low = rng.NextWord();
            uint high = rng.NextWord();
            if (includeFleet)
            {
                ulong seed = Mix64(((ulong)high << 32) | low);
                string name = words != null ? ShipNames.Generate(seed, words) ?? $"Freighter {seed:X16}" : $"Freighter {seed:X16}";
                ships.Add(new SystemStarship(shipIndex, SystemStarshipType.Freighter, seed, name, "Freighter"));
            }
            shipIndex++;
        }

        // Ships #28..#35: 8 System Frigates (advance PRNG 16 words)
        for (int i = 0; i < 16; i++)
            rng.Step();
        shipIndex += 8;

        // Ship #36: Sentinel Interceptor (Robot Class)
        {
            uint low = rng.NextWord();
            uint high = rng.NextWord();
            if (includeSentinel || includeFleet)
            {
                ulong seed = Mix64(((ulong)high << 32) | low);
                string name = words != null ? ShipNames.Generate(seed, words) ?? $"Sentinel Interceptor {seed:X16}" : $"Sentinel Interceptor {seed:X16}";
                ships.Add(new SystemStarship(shipIndex, SystemStarshipType.SentinelInterceptor, seed, name, "Sentinel"));
            }
            shipIndex++;
        }

        if (!includeFleet)
            return ships;

        // Ship #37: Sentinel Capital Ship (2 words)
        rng.Step(); rng.Step();
        shipIndex++;

        // Ships #38..#42: Pirate Fighters (5 ships * 2 words = 10 words)
        for (int i = 0; i < 10; i++)
            rng.Step();
        shipIndex += 5;

        // Gap between #42 and #43 (2 words)
        rng.Step(); rng.Step();

        // Ships #43..#49: Frigates, Pirate Freighters, Swarm Drone (7 ships * 2 words = 14 words)
        for (int i = 0; i < 14; i++)
            rng.Step();
        shipIndex += 7;

        // Ship #50: Pirate Dreadnought (Corvette Class)
        {
            uint low = rng.NextWord();
            uint high = rng.NextWord();
            ulong seed = Mix64(((ulong)high << 32) | low);
            string name = $"Pirate Dreadnought {seed:X16}";
            ships.Add(new SystemStarship(shipIndex++, SystemStarshipType.PirateDreadnought, seed, name, "Dreadnought"));
        }

        return ships;
    }

    /// <summary>
    /// Overload generating ships using a system's attributes directly.
    /// </summary>
    public static IReadOnlyList<SystemStarship> Generate(
        ulong portalCode,
        SystemAttributes attributes,
        Func<string, IReadOnlyList<string>?>? words = null,
        bool includeFleet = false,
        bool includeSentinel = false) =>
        Generate(portalCode, attributes.Race, attributes.Pirate, words, includeFleet, includeSentinel);
}
