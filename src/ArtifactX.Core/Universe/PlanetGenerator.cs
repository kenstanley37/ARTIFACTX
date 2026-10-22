namespace ArtifactX.Core.Universe;

/// <summary>One option in a planet type's file list (GcBiomeFileListOption): its
/// subtype and how likely it is in a purple-star system and anywhere else.</summary>
public sealed record BiomeFileOption(float PurpleSystemWeight, int SubType, float Weight);

/// <summary>
/// The game data the planet-type picker reads, from the player's own game files:
/// metadata/simulation/solarsystem/biomes/biomelistperstartype.mbin (odds per star) and
/// biomefilenames.mbin (valid start/giant/purple-moon types and each type's file options)
/// and biomefilenamesarchive.mbin (the older file options, which decide Infested).
/// </summary>
/// <param name="Normal">BiomeProbability by list: Yellow, Green, Blue, Red, Purple, LushYellow, AbandonedYellow.</param>
/// <param name="Prime">PrimeBiomeProbability, same lists.</param>
/// <param name="ArchiveFiles">The archive list's options per type; null in catalogs built before
/// format 16, which then keep the first pick.</param>
public sealed record BiomeTables(
    float[][] Normal,
    float[][] Prime,
    float ConvertDeadToWeird,
    int[] ValidGiantPlanetBiome,
    int[] ValidPurpleMoonBiome,
    int[] ValidStartPlanetBiome,
    BiomeFileOption[][] Files,
    BiomeFileOption[][]? ArchiveFiles = null);

/// <summary>A body's type (GcBiomeType) and subtype (GcBiomeSubType), in save order, and
/// its class (GcPlanetClass: 1 the safe start planet).</summary>
public sealed record PlanetType(int Biome, int SubType, int Class = 0);

/// <summary>
/// Planet and moon types from a system's address, without visiting it - a port of
/// NMS.exe's solar-system generator (build 180836, read 2026-10-22): the generator's
/// random state starts from the packed system address, the layout (0x14164da40) makes
/// 1 draw, 3 per body that isn't a moon, 1 per body with moons and 1 more, then the
/// picker (0x14164c770) chooses each body's type and subtype. Checked against the game's
/// own code run in an emulator and against 1,073 planets recorded in a real save.
/// Its quirks are the game's and are kept on purpose: two totals that sum the first N
/// weights instead of the listed types', re-rolls for repeated types, a separate
/// random stream for dead-to-weird, and prime planets on a stream of their own.
/// </summary>
public static class PlanetGenerator
{
    private const ulong Multiplier = 0x5A76F899;
    private const double Tiny = 2.3283064365386963e-10;
    private const int Dead = 6, Weird = 7, Waterworld = 14, GasGiant = 15, BiomeCount = 17, Infested = 0x18;

    /// <summary>Galaxy type by galaxy number mod 20 (code data in NMS.exe at 0x144a33d10):
    /// 1 harsh, 2 empty, 3 lush, 4 normal.</summary>
    private static readonly int[] GalaxyTypes = { 4, 4, 2, 4, 4, 4, 1, 4, 4, 3, 4, 1, 4, 4, 2, 4, 4, 4, 3, 4 };

    /// <summary>A galaxy's type (galaxy number 0-based): 1 harsh, 2 empty, 3 lush, 4 normal.</summary>
    public static int GalaxyType(int galaxy) => GalaxyTypes[((galaxy % 20) + 20) % 20];

    private struct Rng
    {
        public uint S0, S1;
        public static Rng Start(ulong seed)
        {
            uint lo = (uint)seed, hi = (uint)(seed >> 32);
            return new Rng { S0 = lo == 0 ? 1u : lo, S1 = ((lo << 16) | (lo >> 16)) ^ hi ^ lo };
        }
        public uint Next()
        {
            ulong t = S0 * Multiplier + S1;
            S0 = (uint)t;
            S1 = (uint)(t >> 32);
            return S0;
        }
        public static float Fraction(uint word) => (float)(word * Tiny);
    }

    /// <summary>Each body's type, in the save's planet order (planet index - 1).</summary>
    public static PlanetType[] Generate(ulong portalCode, int galaxy, BiomeTables t)
    {
        var attributes = SystemGenerator.Attributes(portalCode, galaxy);
        var bodies = SystemGenerator.Bodies(portalCode, galaxy);
        int primary = attributes.PlanetCount, total = primary + attributes.PrimePlanetCount;
        if (total <= 0) return Array.Empty<PlanetType>();

        // Body classes as the game keeps them: size 0-2, 3 moon, 4 giant; parent of a moon.
        var cls = new int[total];
        var parent = new int[total];
        int size = 0;
        for (int i = 0; i < total; i++)
        {
            int host = i < bodies.Hosts.Length ? bodies.Hosts[i] : 0;
            parent[i] = host > 0 ? host - 1 : -1;
            cls[i] = host > 0 ? 3 : attributes.GasGiant && i == 0 ? 4 : size < bodies.Sizes.Length ? bodies.Sizes[size++] : 0;
        }

        ulong address = ((portalCode >> 32) & 0xFFF) << 40 | (ulong)(galaxy & 0xFF) << 32 | (portalCode & 0xFFFFFFFF);
        int star = (int)attributes.Star;
        int galaxyType = GalaxyTypes[galaxy % 20];
        float[] normal = t.Normal[galaxyType == 3 && star == 0 ? 5 : galaxyType == 1 && star == 0 ? 6 : star];
        float[] prime = t.Prime[galaxyType == 3 && star == 0 ? 5 : galaxyType == 1 && star == 0 ? 6 : star];

        // Mode (0x14164c580): 1 for a guide star or a yellow star with a safe-start planet.
        var va = SystemGenerator.Voxel(portalCode);
        bool guide = (long)((portalCode >> 32) & 0xFFF) - 1 < va.GuideStarCount;
        int mode = guide || (star == 0 && attributes.SafeStartPlanet > 0) ? 1 : 0;

        var biome = new int[total];
        var sub = new int[total];
        var kinds = new int[total];

        // Layout draws.
        var rng = Rng.Start(address);
        rng.Next();
        var moons = new int[Math.Max(primary, 1)];
        for (int i = 0; i < primary; i++)
        {
            if (cls[i] == 3) { if (parent[i] >= 0 && parent[i] < moons.Length) moons[parent[i]]++; }
            else { rng.Next(); rng.Next(); rng.Next(); }
        }
        for (int i = 0; i < primary; i++)
            if (moons[i] > 0) rng.Next();
        rng.Next();

        float sumNormal = Sum(normal, BiomeCount), sumPrime = Sum(prime, BiomeCount);
        float sumStart = 0f;
        foreach (int b in t.ValidStartPlanetBiome) sumStart += normal[b];
        // The game sums the first N weights here, N = the list's length (kept as is).
        float sumMoonN = Sum(normal, t.ValidPurpleMoonBiome.Length), sumMoonP = Sum(prime, t.ValidPurpleMoonBiome.Length);
        float sumGiantN = Sum(normal, t.ValidGiantPlanetBiome.Length), sumGiantP = Sum(prime, t.ValidGiantPlanetBiome.Length);
        int nonZero = normal.Take(BiomeCount).Count(w => w != 0f);

        // Primary bodies: pick, then re-roll a repeat of the previous body's type.
        int index = 0, guard = 0;
        while (index < primary && guard++ < 10_000)
        {
            int kind = mode is 1 or 3 && index == attributes.SafeStartPlanet - 1 ? 1 : mode == 3 ? 2 : 0;
            bool forcedLush = false;
            int pick = 0;
            if (galaxyType == 3 && kind == 1) forcedLush = true;
            else if (kind == 1)
                pick = Cumulative(t.ValidStartPlanetBiome, normal, Rng.Fraction(rng.Next()) * sumStart, 4);
            else if (star == 4 && cls[index] == 3)
                pick = Cumulative(t.ValidPurpleMoonBiome, normal, Rng.Fraction(rng.Next()) * sumMoonN, Dead);
            else
            {
                float f = Rng.Fraction(rng.Next());
                pick = cls[index] == 4
                    ? Cumulative(t.ValidGiantPlanetBiome, normal, f * sumGiantN, Dead)
                    : Cumulative(null, normal, f * sumNormal, 0);
            }
            if (attributes.GasGiantPlanet && index == 0) pick = GasGiant;
            biome[index] = pick;
            kinds[index] = kind;
            sub[index] = SubType(ref rng, t.Files[pick], Archive(t, pick), star, kind == 1 && galaxyType == 4);

            bool differs = index == 0 || biome[index] != biome[index - 1] || nonZero <= 1;
            bool avoid = false;
            if (kind == 1)
            {
                avoid = pick is not (Dead or Weird or GasGiant);
                if (t.ValidStartPlanetBiome.Length == 1) differs = true;
            }
            else if (primary == 1 && !attributes.GasGiantPlanet) avoid = true;
            bool barren = pick is Dead or Weird or Waterworld or GasGiant;
            if ((forcedLush || index == 0 || differs) && !(avoid && barren)) index++;
        }

        // Dead worlds may turn weird, on a stream of their own.
        if (star != 4)
        {
            var weird = Rng.Start(address);
            for (int i = 0; i < primary; i++)
            {
                if (biome[i] != Dead) continue;
                if (!(t.ConvertDeadToWeird > Rng.Fraction(weird.Next()))) continue;
                biome[i] = Weird;
                var options = t.Files[Weird];
                uint word = weird.Next();
                if (options.Length > 0) sub[i] = options[(int)(((ulong)word * (ulong)options.Length) >> 32)].SubType;
            }
        }

        // Prime bodies: a stream of their own, started 3-13 draws on from the main one.
        var p = rng;
        uint skipWord = p.Next();
        for (uint k = skipWord % 11 + 3; k > 0; k--) p.Next();
        for (int j = 0; j < attributes.PrimePlanetCount && primary + j < total; j++)
        {
            int i = primary + j;
            int pick;
            float f = Rng.Fraction(p.Next());
            if (star == 4 && cls[i] == 3) pick = Subtract(t.ValidPurpleMoonBiome, prime, f * sumMoonP, 0);
            else if (cls[i] == 4) pick = Subtract(t.ValidGiantPlanetBiome, prime, f * sumGiantP, 0);
            else pick = Subtract(null, prime, f * sumPrime, 0);
            if (attributes.GasGiantPlanet && i == 0) pick = GasGiant;
            biome[i] = pick;
            sub[i] = SubType(ref p, t.Files[pick], Archive(t, pick), star, false);
        }

        return biome.Select((b, i) => new PlanetType(b, sub[i], kinds[i])).ToArray();
    }

    private static float Sum(float[] weights, int count)
    {
        float s = 0f;
        for (int i = 0; i < count && i < weights.Length; i++) s += weights[i];
        return s;
    }

    /// <summary>The first type whose running total reaches the target (list order, or all 17).</summary>
    private static int Cumulative(int[]? list, float[] weights, float target, int fallback)
    {
        float cum = 0f;
        int n = list?.Length ?? BiomeCount;
        for (int k = 0; k < n; k++)
        {
            int b = list?[k] ?? k;
            if (!(target < cum) && cum + weights[b] >= target) return b;
            cum += weights[b];
        }
        return fallback;
    }

    /// <summary>The first type whose weight is above what's left of the target.</summary>
    private static int Subtract(int[]? list, float[] weights, float target, int fallback)
    {
        int n = list?.Length ?? BiomeCount;
        for (int k = 0; k < n; k++)
        {
            int b = list?[k] ?? k;
            if (weights[b] > target) return b;
            target -= weights[b];
        }
        return fallback;
    }

    private static int SubtractOption(BiomeFileOption[] options, Func<BiomeFileOption, float> weight, float target)
    {
        for (int k = 0; k < options.Length; k++)
        {
            float w = weight(options[k]);
            if (w > target) return k;
            target -= w;
        }
        return options.Length - 1;
    }

    private static BiomeFileOption[]? Archive(BiomeTables t, int biome) =>
        t.ArchiveFiles is { } a && biome < a.Length ? a[biome] : null;

    /// <summary>
    /// A body's subtype (0x14164d890 and what follows it, for primary and prime bodies alike):
    /// one draw on the current file options. Outside purple systems the game then looks
    /// again from the state before that draw, on the archive options: Infested there wins;
    /// an Infested first pick is re-drawn on the archive options without Infested (which is
    /// how Swamp and Lava subtypes come about, 2026-10-22). The look-ahead doesn't move the
    /// stream on.
    /// </summary>
    private static int SubType(ref Rng rng, BiomeFileOption[] options, BiomeFileOption[]? archive, int star, bool noInfested)
    {
        var before = rng;
        int result = 1;
        if (options.Length > 0)
        {
            Func<BiomeFileOption, float> weight = o => o.SubType == Infested && noInfested ? 0f : star == 4 ? o.PurpleSystemWeight : o.Weight;
            result = options[SubtractOption(options, weight, Rng.Fraction(rng.Next()) * Total(options, weight))].SubType;
        }
        if (star == 4 || archive is not { Length: > 0 }) return result;

        var look = before;
        Func<BiomeFileOption, float> plain = o => o.SubType == Infested && noInfested ? 0f : o.Weight;
        if (archive[SubtractOption(archive, plain, Rng.Fraction(look.Next()) * Total(archive, plain))].SubType == Infested) return Infested;
        if (result != Infested) return result;
        Func<BiomeFileOption, float> noInf = o => o.SubType == Infested ? 0f : o.Weight;
        return archive[SubtractOption(archive, noInf, Rng.Fraction(look.Next()) * Total(archive, noInf))].SubType;
    }

    private static float Total(BiomeFileOption[] options, Func<BiomeFileOption, float> weight)
    {
        float total = 0f;
        foreach (var o in options) total += weight(o);
        return total;
    }
}
