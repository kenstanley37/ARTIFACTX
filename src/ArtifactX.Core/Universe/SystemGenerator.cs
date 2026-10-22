namespace ArtifactX.Core.Universe;

// Ported to C# from nms_namegen (MIT) by Stuart Coyle & had.sh & GoodGuysFree:
// https://github.com/hadsh/nms_namegen (system.py, region.py, planet.py) - see
// THIRD-PARTY-NOTICES.md. Accuracy figures below are the reference project's,
// measured against hand-recorded post-Origins systems (2026-08-23 corpus).
// Portal codes are the 12-hex-digit form PSSSYYZZZXXX; galaxy is 0-based here
// (Euclid = 0), matching the reference. Callers holding a 1-based galaxy
// number subtract one.

public enum StarColour { Yellow = 0, Green = 1, Blue = 2, Red = 3, Purple = 4 }
public enum SystemEconomy { None = 0, Trading = 1, AdvancedMaterials = 2, Scientific = 3, Mining = 4, Manufacturing = 5, Technology = 6, PowerGeneration = 7 }
public enum SystemRace { None = 0, Gek = 1, Korvax = 2, Vykeen = 3 }

/// <summary>What the game derives for a star system from its address.</summary>
public sealed record SystemAttributes(
    int PlanetCount,
    int PrimePlanetCount,
    int SafeStartPlanet,
    bool GasGiant,
    StarColour Star,
    SystemEconomy Economy,
    int Wealth,          // 1 low, 2 medium, 3 high
    int Conflict,        // 1 low, 2 medium, 3 high
    SystemRace Race,     // None when uncharted
    bool Uncharted,
    bool Abandoned,
    bool Pirate)
{
    /// <summary>A gas-giant system whose first body is itself the gas giant (and six
    /// prime bodies) - the inner 66% roll. Without it a gas-giant system still lays its
    /// moons around the first body, but that body's type is picked as usual
    /// (NMS.exe attribute output +0x2d vs +0x2c, 2026-10-22).</summary>
    public bool GasGiantPlanet { get; init; }
}

public sealed record VoxelAttributes(int GuideStarCount, int BlackHoleCount, int AtlasStationCount, int InsideGap, int GuideStarRenegadeCount);

/// <summary>Planets and moons the generator lays out for a system.</summary>
/// <param name="Hosts">Per body (index 0 = body 1): 0 for a planet, else the 1-based body a
/// moon orbits. The loops below decide it - a large planet takes up to two moons right
/// after it (Amogat-Aizu XIV: Reroldbur + Ackroit Ugin, Juneapo W40 + Puppinan XIII and
/// Zionehou Omega, as the game's Discoveries page draws them, 2026-10-17).</param>
public sealed record SystemBodies(ulong[] PlanetSeeds, int PlanetCount, int MoonCount, int[] Sizes, int[] Hosts)
{
    public bool IsMoon(int body) => body >= 1 && body <= Hosts.Length && Hosts[body - 1] > 0;
}

public static class SystemGenerator
{
    private const ulong ConstA = 0x64DD81482CBD31D7;
    private const ulong ConstB = 0xE36AA5C613612997;
    private const double TinyDouble = 2.3283064370807974e-10;

    // Per-star-type probability tables, as the exact 32-bit floats the game holds.
    private static readonly double[] AbandonedThreshold = { 0.0, 0.10000000149011612, 0.10000000149011612, 0.0, 0.3499999940395355 };
    private static readonly double[] EmptyThreshold = { 0.0, 0.4000000059604645, 0.4000000059604645, 0.949999988079071, 0.20000000298023224 };
    private static readonly double[] PirateThreshold = { 0.25, 0.15000000596046448, 0.15000000596046448, 0.5, 0.05000000074505806 };

    private static readonly string[] RegionAdornments =
    {
        "{0} Adjunct", "{0} Void", "{0} Expanse", "{0} Terminus", "{0} Boundary", "{0} Fringe", "{0} Cluster", "{0} Mass",
        "{0} Band", "{0} Cloud", "{0} Nebula", "{0} Quadrant", "{0} Sector", "{0} Anomaly", "{0} Conflux", "{0} Instability",
        "Sea of {0}", "The Arm of {0}", "{0} Spur", "{0} Shallows",
    };

    private static readonly string[] PlanetAdornments = { "Prime", "Major", "Minor", "Alpha", "Beta", "Gamma", "Delta", "Omega", "Sigma", "Tau" };

    private static readonly string[] PlanetStyles =
    {
        "%PROCNORM%", "%PROCNORM% %ADORNMENT%", "%PROCNORM% %NUMERAL%", "%PROCNORM% %SHORTCODE%",
        "%PROCLONG% %PROCSHORT%", "%PROCSHORT% %LONGCODE%", "%PROCNORM%", "%PROCNORM% %ADORNMENT%",
        "%PROCNORM% %NUMERAL%", "Style 9", "New %PROCNORM%",
    };

    private static readonly string[] Roman =
        { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII", "XIII", "XIV", "XV", "XVI", "XVII", "XVIII", "XIX", "XX" };

    /// <summary>A 32-bit draw read the way the generator compares it: as a float32 in [0,1).</summary>
    private static double Probability(ulong word) => (float)((word & 0xFFFFFFFF) / 4294967296.0);

    private static ulong Mix64(ulong register)
    {
        register = ((register >> 33) ^ register) * ConstA;
        register = ((register >> 33) ^ register) * ConstB;
        return (register >> 33) ^ register;
    }

    // ------------------------------------------------------------------ voxel

    public static VoxelAttributes Voxel(ulong portalCode)
    {
        long x = (long)(portalCode & 0xFFF);
        long y = (long)((portalCode & 0xFF000000) >> 24);
        long z = (long)((portalCode & 0xFFF000) >> 12);
        if (x > 0x7FF) x -= 0x1000;
        if (z > 0x7FF) z -= 0x1000;
        if (y > 0x7F) y -= 0x100;

        int guide = 0x78, blackHoles = 1, atlas = 1, gap = 0, renegade = 0;
        long distance = (long)Math.Sqrt(x * x + y * y + z * z);
        if (distance < 8)
        {
            guide = 0; blackHoles = 0; atlas = 0; gap = 1;
        }
        if (distance > 8 && distance < 1440)
        {
            int diff = (int)((distance - 8) * 120 / 1440.0);
            diff = Math.Clamp(diff, 0, 0x78);
            renegade = 0x78 - diff;
        }
        return new VoxelAttributes(guide, blackHoles, atlas, gap, renegade);
    }

    // ------------------------------------------------------------------ system attributes

    public static SystemAttributes Attributes(ulong portalCode, int galaxy)
    {
        portalCode &= 0xFFFFFFFFFFF;
        long systemId = (long)((portalCode & 0xFFF00000000) >> 32);
        ulong universal = ((((ulong)systemId << 8) | ((ulong)galaxy & 0xFF)) << 32) | (portalCode & 0xFFFFFFFF);
        var va = Voxel(portalCode);
        systemId -= 1; // the generator decrements before every comparison below

        ulong systemSeed = UniverseHash.IndexPrimed(universal) & 0xFFFFFFFF;
        ulong rol16 = (((systemSeed & 0x0000FFFF) << 16) | ((systemSeed & 0xFFFF0000) >> 16)) ^ systemSeed;
        rol16 &= 0xFFFFFFFF;
        ulong seed = systemSeed == 0 ? (systemSeed + 1) * UniverseRandom.Multiplier + rol16 : systemSeed * UniverseRandom.Multiplier + rol16;

        var rng = new UniverseRandom(seed);
        int starType = 0, safeStart = 0, primeCount = 2, planetCount, anomaly = 0;

        if (systemId < va.GuideStarCount)
        {
            planetCount = (int)(((seed & 0xFFFFFFFF) * 4) >> 32) + 3;
            safeStart = (int)rng.Next((uint)planetCount) + 1;
        }
        else
        {
            if ((((seed & 0xFFFFFFFF) * 0x64) >> 32) < 0x1E)
                starType = (int)rng.Next(3) + 1;

            long anomalyDiff = systemId - va.GuideStarCount;
            if (va.BlackHoleCount > 0 && anomalyDiff >= 0 && anomalyDiff < va.BlackHoleCount)
            {
                anomaly = 2;
                starType = 0;
            }
            if (va.AtlasStationCount > 0 && anomalyDiff - va.BlackHoleCount >= 0 && anomalyDiff - va.BlackHoleCount < va.AtlasStationCount)
            {
                anomaly = 1;
                starType = 0;
            }

            planetCount = (int)rng.Next(6) + 1;
            safeStart = va.GuideStarRenegadeCount >= 10 || starType != 0 || anomaly != 0
                ? 0
                : (int)rng.Next((uint)(planetCount + 1));
        }

        rng.Step();
        uint economyWord = rng.CurrentWord;
        int economy = ((ulong)economyWord * 7 >> 32) switch { 0 => 4, 1 => 6, 2 => 1, 3 => 5, 4 => 2, 5 => 3, _ => 7 };

        rng.Step();
        uint wealthWord = rng.CurrentWord;
        ulong wealthPct = (ulong)wealthWord * 100 >> 32;
        int wealth = wealthPct < 10 ? 3 : wealthPct < 30 ? 1 : 2;

        rng.Step();
        int conflict = (int)((ulong)rng.CurrentWord * 3 >> 32) + 1;

        rng.Step();
        int race = ((ulong)rng.CurrentWord * 3 >> 32) switch { 0 => 1, 1 => 3, _ => 2 };

        if (systemId < va.GuideStarRenegadeCount)
            starType = (int)rng.Next(3) + 1;

        if (systemId > 0x3E7 && systemId < 0x429)
            starType = 4; // purple

        rng.Step();
        bool abandoned = Probability(rng.Seed) < AbandonedThreshold[starType];

        bool uncharted = false;
        if (!abandoned)
        {
            rng.Step();
            uncharted = Probability(rng.Seed) < EmptyThreshold[starType];
        }
        if (uncharted) race = 0;
        if (abandoned)
        {
            wealth = 1;
            conflict = 1;
        }

        int diffCount = 6 - planetCount;
        if (diffCount < 1) primeCount = 0;
        else if (rng.Next(100) >= 33 || diffCount < 2) primeCount = 1;

        bool gasGiant = false, gasGiantPlanet = false;
        if (starType == 4)
        {
            primeCount += planetCount;
            planetCount = 0;
            if (rng.Next(100) < 15)
            {
                gasGiant = true;
                if (rng.Next(100) < 66)
                {
                    gasGiantPlanet = true;
                    planetCount = 0;
                    primeCount = 6;
                }
            }
        }

        bool pirate = false;
        if (!abandoned && !uncharted && (starType != 0 || safeStart <= 0))
        {
            var peek = new UniverseRandom(rng.Seed);
            peek.Step();
            pirate = Probability(peek.Seed) < PirateThreshold[starType];
        }

        return new SystemAttributes(planetCount, primeCount, safeStart, gasGiant, (StarColour)starType,
            (SystemEconomy)economy, wealth, conflict, (SystemRace)race, uncharted, abandoned, pirate) { GasGiantPlanet = gasGiantPlanet };
    }

    // ------------------------------------------------------------------ planets and moons

    private static ulong BodySeed(UniverseRandom rng)
    {
        ulong low = rng.NextWord();
        ulong high = rng.NextWord();
        return Mix64((high << 32) | low);
    }

    public static SystemBodies Bodies(ulong portalCode, int galaxy)
    {
        var attributes = Attributes(portalCode, galaxy);
        var seeds = new List<ulong>();
        var sizes = new List<int>();
        ulong galacticCoords = portalCode & 0xFFFFFFFF;
        ulong systemIndex = ((portalCode & 0x0FFF00000000) >> 24) | (ulong)galaxy;

        ulong register = Mix64((systemIndex << 32) | galacticCoords);
        ulong seedH = ((((register & 0xFFFF0000) >> 16) | ((register & 0x0000FFFF) << 16)) ^ (register & 0xFFFFFFFF) ^ (register >> 32)) & 0xFFFFFFFF;
        ulong seedL = register & 0xFFFFFFFF;
        if (seedH == 0) seedH = 1;
        var rng = new UniverseRandom((seedH << 32) | seedL);

        int primary = attributes.PlanetCount;
        int total = primary + attributes.PrimePlanetCount;
        int stop = attributes.SafeStartPlanet - 1;
        int moons = 0;
        var hosts = new List<int>();
        void Host(int body, int host) { while (hosts.Count < body) hosts.Add(0); hosts[body - 1] = host; }

        if (attributes.GasGiant)
        {
            for (int k = 0; k < total; k++) seeds.Add(BodySeed(rng));
            for (int b = 2; b <= total; b++) Host(b, 1);
            return new SystemBodies(seeds.ToArray(), 1, total - 1, sizes.ToArray(), PadHosts(hosts, total));
        }

        int i = 0;
        while (i < primary)
        {
            i++;
            int size = (int)rng.Next(3);
            sizes.Add(size);
            if (size == 0)
            {
                int m = Math.Clamp(primary - i, 0, 2);
                int moonRoll = (int)rng.Next((uint)(m + 1));
                if (moonRoll > 0)
                {
                    int host = i;
                    while (i != stop)
                    {
                        i++;
                        Host(i, host);
                        moonRoll--;
                        moons++;
                        if (moonRoll <= 0) break;
                    }
                }
            }
        }

        i = 0;
        while (i < primary)
        {
            seeds.Add(BodySeed(rng));
            i++;
        }

        while (i < total)
        {
            int size = (int)rng.Next(3);
            sizes.Add(size);
            seeds.Add(BodySeed(rng));
            i++;
            if (size == 0)
            {
                int m = Math.Clamp(total - i, 0, 2);
                int moonRoll = (int)rng.Next((uint)(m + 1));
                int host = i;
                while (moonRoll > 0 && i != stop)
                {
                    Host(i + 1, host);
                    seeds.Add(BodySeed(rng));
                    moons++;
                    moonRoll--;
                    i++;
                }
            }
        }
        return new SystemBodies(seeds.ToArray(), total - moons, moons, sizes.ToArray(), PadHosts(hosts, total));
    }

    private static int[] PadHosts(List<int> hosts, int total)
    {
        while (hosts.Count < total) hosts.Add(0);
        return hosts.ToArray();
    }

    // ------------------------------------------------------------------ names

    public static string RegionName(ulong portalCode, int galaxy)
    {
        ulong register = (ulong)galaxy >> 1;
        register ^= ((ulong)galaxy << 32) | (portalCode & 0xFFFFFFFF);
        register *= ConstA;
        register = ((register >> 33) ^ register) * ConstB;
        register = (register >> 33) ^ register;
        ulong seedH = ((((register & 0xFFFF0000) >> 16) | ((register & 0x0000FFFF) << 16)) ^ (register & 0xFFFFFFFF) ^ (register >> 32)) & 0xFFFFFFFF;
        ulong seed = register & 0xFFFFFFFF;
        if (seedH == 0) seedH = 1;
        var rng = new UniverseRandom(seed | (seedH << 32));

        int maxLength = (int)rng.Next(4) + 6;
        string name = NameGenerator.Capitalize(NameGenerator.Generate(rng, 0, 6, maxLength));
        if (rng.Next(0x64) < 0x50)
            name = string.Format(RegionAdornments[rng.Next(0x14)], name);
        return name;
    }

    public static string SystemName(ulong portalCode, int galaxy)
    {
        ulong galacticCoords = portalCode & 0xFFFFFFFF;
        ulong systemIndex = ((portalCode & 0x0FFF00000000) >> 24) | (ulong)galaxy;
        ulong rolCoords = (((galacticCoords & 0x0000FFFF) << 16) | ((galacticCoords & 0xFFFF0000) >> 16)) ^ galacticCoords ^ systemIndex;
        rolCoords &= 0xFFFFFFFF;
        ulong seed = galacticCoords == 0 ? (galacticCoords + 1) * UniverseRandom.Multiplier + rolCoords : galacticCoords * UniverseRandom.Multiplier + rolCoords;
        var rng = new UniverseRandom(seed);

        ulong alphasetReg = ((seed & 0xFFFFFFFF) * 5) >> 32;
        int alphaset = alphasetReg == 0 ? 2 : (alphasetReg & 0xFF) < 4 ? (int)(alphasetReg & 0xFF) + 2 : 7;
        int maxLength = (int)rng.Next(4) + 6;
        string name = NameGenerator.Capitalize(NameGenerator.Generate(rng, alphaset, 6, maxLength));

        if (name.Length < 8 && rng.Next(4) < 2)
        {
            uint reg = rng.Next(5);
            int alphaset2 = reg == 0 ? 2 : reg < 4 ? (int)reg + 2 : 7;
            name = $"{name}-{NameGenerator.Capitalize(NameGenerator.Generate(rng, alphaset2, 3, 5))}";
        }

        if (rng.Next(0x0A) < 3)
        {
            int n = Math.Min((int)rng.Next(19) + 1, 19);
            name = $"{name} {Roman[n - 1]}";
        }
        return name;
    }

    /// <summary>Planet name from its 64-bit seed (as stored in the save's discovery records).</summary>
    public static string PlanetName(ulong planetSeed)
    {
        ulong low = planetSeed & 0xFFFFFFFF;
        ulong high = planetSeed >> 32;
        ulong rol16 = ((((low & 0x0000FFFF) << 16) | ((low & 0xFFFF0000) >> 16)) ^ low ^ high) & 0xFFFFFFFF;
        ulong seed = low == 0 ? (low + 1) * UniverseRandom.Multiplier + rol16 : low * UniverseRandom.Multiplier + rol16;
        var rng = new UniverseRandom(seed);

        int adornment = (int)(((rng.Seed & 0xFFFFFFFF) * 10) >> 32);
        int code = (int)rng.Next(50) + 1;
        int shortcode = (int)rng.Next(0x1A) + 0x41;
        int numeral = (int)rng.Next(0x12) + 2;
        int digit = (int)rng.Next(0x09) + 1;
        int alpha = (int)rng.Next(0x1A) + 0x41;
        int longcode = (int)rng.Next(0x59) + 0xB;

        string procNorm = NameGenerator.Generate(rng, 7, 4, 8);
        string procShort = NameGenerator.Generate(rng, 5, 4, 5);
        string procLong = NameGenerator.Generate(rng, 7, 6, 10);

        int style = (int)rng.Next(9);
        double target = rng.NextWord() * TinyDouble;
        if (!(0.0350000001 <= target)) style = 10;

        return PlanetStyles[style]
            .Replace("%PROCNORM%", NameGenerator.Capitalize(procNorm))
            .Replace("%PROCSHORT%", NameGenerator.Capitalize(procShort))
            .Replace("%PROCLONG%", NameGenerator.Capitalize(procLong))
            .Replace("%ADORNMENT%", PlanetAdornments[adornment])
            .Replace("%SHORTCODE%", $"{(char)shortcode}{code % 0x50}")
            .Replace("%NUMERAL%", Roman[numeral - 1])
            .Replace("%LONGCODE%", $"{longcode}/{(char)alpha}{digit}");
    }

    /// <summary>Planet name for a portal code (its planet digit picks the body).</summary>
    public static string PlanetName(ulong portalCode, int galaxy)
    {
        int planetIndex = (int)((portalCode & 0xF00000000000) >> 44);
        var seeds = Bodies(portalCode, galaxy).PlanetSeeds;
        // Same as the reference: planet digit 0 indexes from the end (Python's seeds[-1]).
        int index = planetIndex - 1;
        if (index < 0) index += seeds.Length;
        return index >= 0 && index < seeds.Length ? PlanetName(seeds[index]) : "";
    }
}
