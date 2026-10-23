using ArtifactX.Core.Procedural;

namespace ArtifactX.Core.Universe;

/// <summary>
/// Starship classes as generated in system pools.
/// Values match NMS.exe's GcSpaceshipClasses enum:
/// 1=Dropship (Hauler), 2=Fighter, 3=Scientific (Explorer), 4=Shuttle, 6=Royal (Exotic), 8=Sail (Solar).
/// </summary>
public enum SystemStarshipType
{
    Hauler = 1,
    Fighter = 2,
    Explorer = 3,
    Shuttle = 4,
    Exotic = 6,
    Solar = 8,
}

/// <summary>
/// A starship generated in a system's procedural pool of 21 ships.
/// </summary>
/// <param name="Index">1-based index (1 to 21).</param>
/// <param name="Type">The ship class (Hauler, Fighter, Explorer, Shuttle, Exotic, Solar).</param>
/// <param name="Seed">The 64-bit seed determining model parts, accessories, and colors.</param>
/// <param name="DefaultName">The default procedural name the game assigns to this ship.</param>
public sealed record SystemStarship(
    int Index,
    SystemStarshipType Type,
    ulong Seed,
    string DefaultName)
{
    public string TypeLabel => Type switch
    {
        SystemStarshipType.Hauler => "Hauler",
        SystemStarshipType.Fighter => "Fighter",
        SystemStarshipType.Explorer => "Explorer",
        SystemStarshipType.Shuttle => "Shuttle",
        SystemStarshipType.Exotic => "Exotic",
        SystemStarshipType.Solar => "Solar",
        _ => Type.ToString(),
    };

    public string SeedHex => $"0x{Seed:X16}";
}

/// <summary>
/// Generates the fixed pool of 21 starships native to any star system in No Man's Sky.
///
/// Reverse-engineered from NMS.exe (build 180836, 2026-10-22):
/// - System starship routine at 0x14164a4a0 (loop at 0x14164adc0 to 0x14164b220).
/// - Reseeds the universe PRNG from portal address, advances exactly 41 LCG steps.
/// - System pool holds 21 starships:
///   - 7 of the dominant race's specialty type (Vy'keen -> Fighters, Gek -> Haulers, Korvax -> Explorers).
///   - 3 of each of the other two specialty types.
///   - 7 Shuttles (in Outlaw/Pirate systems, Shuttles roll with 85% probability to become Solar sail ships;
///     non-pirate systems have a 10% chance; other classes have 10% in pirate systems).
///   - Exactly 1 Royal / Exotic ship (always S-class).
/// - Each ship seed is generated from 2 PRNG draws mixed through the 64-bit mixer (Mix64).
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
    /// Generates the 21 native starships for the system with the given portal code and attributes.
    /// </summary>
    public static IReadOnlyList<SystemStarship> Generate(
        ulong portalCode,
        SystemRace race,
        bool isPirate,
        Func<string, IReadOnlyList<string>?>? words = null)
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

        var ships = new List<SystemStarship>(21);
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
                ships.Add(new SystemStarship(shipIndex++, shipType, seed, name));
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
            ships.Add(new SystemStarship(shipIndex++, SystemStarshipType.Exotic, seed, name));
        }

        return ships;
    }

    /// <summary>
    /// Overload generating ships using a system's attributes directly.
    /// </summary>
    public static IReadOnlyList<SystemStarship> Generate(
        ulong portalCode,
        SystemAttributes attributes,
        Func<string, IReadOnlyList<string>?>? words = null) =>
        Generate(portalCode, attributes.Race, attributes.Pirate, words);
}
