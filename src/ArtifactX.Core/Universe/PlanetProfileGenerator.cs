namespace ArtifactX.Core.Universe;

/// <summary>
/// The game data a planet's profile is picked from, from the player's own game files:
/// gcterrainglobals (MiningSubstance* lists), gcsolargenerationglobals (ExtremePlanetChance),
/// each biome file's WeatherOptions (GcBiomeData, by biome and subtype), each weather type's
/// storm chances (GcWeatherProperties, via weatherlist), life odds and sentinel odds.
/// </summary>
/// <param name="BiomeWeather">[biome][subtype] -> weather weights [star][17], or null where the
/// game has no file for that subtype (it then uses the first subtype that has one).</param>
/// <param name="Storms">Per weather type (GcWeatherOptions order): (LowStormsChance, HighStormsChance).</param>
/// <param name="FloraChance">biomelistperstartype LifeChance (4 levels); AbandonedFloraChance is the
/// AbandonedLifeChance used in harsh galaxies; FaunaChance is creaturegenerationdata's LifeChance.</param>
public sealed record PlanetProfileTables(
    string[] MiningBiome,
    string[] MiningRare,
    string[] MiningStar,
    string[] MiningStarExtreme,
    float[] ExtremePlanetChance,
    float[][]?[][] BiomeWeather,
    StormChance[] Storms,
    float EmptyGalaxyExtremeChance,
    float EmptyGalaxyStormChance,
    float[] FloraChance,
    float[] AbandonedFloraChance,
    float[] FaunaChance,
    SentinelTables Sentinels,
    ObjectTables? Objects = null,
    Dictionary<string, string>? HintNames = null,
    float[]? ScrapYardDensity = null);

/// <summary>A weather type's storm odds (GcWeatherProperties LowStormsChance, HighStormsChance).</summary>
public sealed record StormChance(float Low, float High);

/// <summary>Sentinel odds from gcgameplayglobals, and the difficulty slot named by
/// gcsolargenerationglobals' CorruptSentinelBuildingCheckDifficulty (corrupted sentinels and
/// weird weather need that slot to be corrupted too).</summary>
public sealed record SentinelTables(
    float Low, float Aggressive, float LowSurvival, float AggressiveSurvival,
    float NonAggressiveLushSurvival, float Vicious, float Corrupt, int CheckDifficulty);

/// <summary>A planet's mined resources (substance ids), weather, life and sentinels.</summary>
/// <param name="WeatherType">GcWeatherOptions: 0 Clear, 1 Dust, 2 Humid, 3 Snow, 4 Toxic, 5 Scorched,
/// 6 Radioactive, 7-9 Red/Green/Blue, 10 Swamp, 11 Lava, 12 Bubble, 13 Weird, 14 Fire, 15 ClearCold, 16 GasGiant.</param>
/// <param name="Storms">0 none, 1 low, 2 high.</param>
/// <param name="Flora">Life level, 0 none to 3 full (the game words it with a random adjective); Fauna likewise.</param>
/// <param name="Sentinels">Per difficulty slot (GcCombatTimerDifficultyOption order, 2 = normal):
/// 0 low, 1 normal, 2 aggressive, 3 corrupted (dissonant).</param>
/// <param name="ScrapWorld">Scrapyard planet (Salvageable Scrap on the scanner; no bones there).</param>
/// <param name="Hints">Scanner hints (ResourceHint ids, e.g. PLANT_DUST, UI_BONES_HINT), in placing order.</param>
/// <param name="Water">Has a sea (and so underwater objects).</param>
public sealed record PlanetProfile(string Common, string Uncommon, string Rare, int WeatherType, bool Extreme, int Storms,
    int Flora, int Fauna, int[] Sentinels, string[] Hints, bool ScrapWorld = false, bool Water = false);

/// <summary>
/// A planet's resources, weather, life and sentinels from its seed - a port of NMS.exe's
/// planet-data routine (build 180836, 0x1416a7880, read 2026-10-22): the planet seed starts
/// a random state that gives 26 child seeds (two draws, joined and mixed, like body seeds);
/// the life step (0x1416956c0: flora, fauna, sentinels) restarts from child 0, the weather
/// step (0x141696760) from child 12 and the resource step (0x141696dc0) from child 19.
/// Checked against the game's own code run in an emulator (808 planets: sentinels 807,
/// weather type 807, resources 803, the rest the emulator's own gaps) and against a real
/// save's screen-reader records (extreme weather right on all but 5 of ~800). Terrain (child 4)
/// and the placed objects behind scanner hints (child 7) are in PlanetProfileGenerator.Objects.cs.
/// Left out: engine-state special cases (the player's start system, settlements, debug options).
/// </summary>
public static partial class PlanetProfileGenerator
{
    private const ulong Multiplier = 0x5A76F899;
    private const double Fraction = 2.3283064370807974e-10;
    private const int Lush = 0, Barren = 5, Dead = 6, Weird = 7, Swamp = 12, Lava = 13, Waterworld = 14, GasGiant = 15;
    private const int DustWeather = 1, HumidWeather = 2, ScorchedWeather = 5, WeirdWeather = 13;
    private const int SwampSub = 0x19, LavaSub = 0x1A;

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
        public float NextFraction() => (float)(Next() * Fraction);
    }

    private static ulong Mix64(ulong v)
    {
        v = (v ^ (v >> 33)) * 0x64DD81482CBD31D7;
        v = (v ^ (v >> 33)) * 0xE36AA5C613612997;
        return v ^ (v >> 33);
    }

    /// <summary>The 26 child seeds each planet step restarts from.</summary>
    private static ulong[] ChildSeeds(ulong planetSeed)
    {
        var rng = Rng.Start(planetSeed);
        var seeds = new ulong[26];
        for (int i = 0; i < seeds.Length; i++)
        {
            ulong lo = rng.Next(), hi = rng.Next();
            seeds[i] = Mix64((hi << 32) | lo);
        }
        return seeds;
    }

    /// <param name="planetIndex">The body's 0-based index in its system (save planet index - 1).</param>
    /// <param name="body">The body's type from PlanetGenerator (biome, the game's subtype - not the
    /// save's -, class: 1 the start planet, body class, prime, dead turned weird).</param>
    /// <param name="galaxyType">1 harsh, 2 empty, 3 lush, 4 normal (PlanetGenerator.GalaxyType).</param>
    /// <param name="inGasGiantSystem">The system's first body is a gas giant (SystemGenerator
    /// GasGiantPlanet - the flag the system hands each planet, 0x755): its other planets roll
    /// no extreme weather or storms.</param>
    public static PlanetProfile Generate(ulong planetSeed, int planetIndex, PlanetType body, int star,
        int galaxyType, bool inGasGiantSystem, PlanetProfileTables t)
    {
        int biome = body.Biome, subType = body.SubType, planetClass = body.Class;
        var seeds = ChildSeeds(planetSeed);
        var (flora, fauna, sentinels, scrap) = Life(Rng.Start(seeds[0]), planetIndex, biome, subType, planetClass, star, galaxyType, t);
        bool corrupted = sentinels[Math.Clamp(t.Sentinels.CheckDifficulty, 0, 3)] == 3;
        var (weather, extreme, storms) = Weather(Rng.Start(seeds[12]), biome, subType, planetClass, star, galaxyType, inGasGiantSystem, corrupted, t);

        // Resources (0x141696dc0): the rare one by the first draw, the second a fraction
        // that splits swamp and lava worlds' uncommon resource.
        var rng = Rng.Start(seeds[19]);
        uint first = rng.Next();
        float f = rng.NextFraction();
        int rareIndex = (int)(((ulong)first * (ulong)t.MiningRare.Length) >> 32);
        string uncommon = biome == Lava || subType == LavaSub
            ? (0.33f > f ? t.MiningBiome[Lava] : t.MiningBiome[Barren])
            : biome == Swamp || subType == SwampSub
                ? (0.15f > f ? t.MiningBiome[Swamp] : 0.33f > f ? t.MiningBiome[16] : 0.66f > f ? t.MiningBiome[0] : t.MiningBiome[Swamp])
                : t.MiningBiome[Math.Clamp(biome, 0, t.MiningBiome.Length - 1)];
        string common = extreme ? t.MiningStarExtreme[star] : t.MiningStar[star];

        string[] hints = Array.Empty<string>();
        bool water = false;
        if (t.Objects is { } objects && BiomeFile(objects, biome, subType) is { } file)
        {
            var terrain = Terrain(Rng.Start(seeds[4]), file, objects.Terrain, body, star);
            water = terrain.Sea > 0f;
            var planet = new PlanetFacts(biome, subType, planetClass, sentinels.Length > 2 ? sentinels[2] : 1,
                ExtremeLooking(biome, subType, weather, extreme), scrap, terrain.Sea, terrain.Caves, objects.Terrain.SeaStandard);
            hints = PlacedHints(Rng.Start(seeds[7]), file, objects, planet);
        }
        return new PlanetProfile(common, uncommon, t.MiningRare[rareIndex], weather, extreme, storms, flora, fauna, sentinels,
            hints, scrap, water);
    }

    private const int Infested = 0x18;

    /// <summary>The life step (0x1416956c0): flora, fauna, a resource draw, then sentinels.</summary>
    private static (int Flora, int Fauna, int[] Sentinels, bool Scrap) Life(Rng rng, int planetIndex, int biome, int subType, int planetClass,
        int star, int galaxyType, PlanetProfileTables t)
    {
        bool harsh = galaxyType == 1;
        int flora = planetClass == 1 ? (harsh ? 2 : 3)
            : biome is Dead or GasGiant or Waterworld ? 0
            : Level(ref rng, harsh ? t.AbandonedFloraChance : t.FloraChance);
        int fauna = planetClass == 1 ? (harsh ? 1 : 2)
            : biome is Dead or GasGiant ? 0
            : biome == Waterworld ? 3
            : Level(ref rng, t.FaunaChance);
        rng.Next(); // a "rich in resources" roll the planet data keeps; nothing here shows it

        // Every difficulty slot starts from the same state, so the slots differ only in their odds.
        var s = t.Sentinels;
        int Slot(int slot)
        {
            if (slot == 0) return 0;
            float low = slot >= 3 ? s.LowSurvival : s.Low;
            float aggressive = slot >= 3 ? s.AggressiveSurvival : slot <= 1 ? 0f : s.Aggressive;
            var r = rng;
            int level;
            if (planetClass == 1) level = low <= r.NextFraction() ? 1 : 0;
            else if (star == 4 && planetIndex == 1) level = 3;
            else if (slot >= 3 && biome == Lush && subType != SwampSub) level = s.NonAggressiveLushSurvival <= r.NextFraction() ? 2 : 1;
            else if (planetClass == 2) level = 0;
            else if (aggressive > r.NextFraction()) level = 2;
            else level = low <= r.NextFraction() ? 1 : 0;
            if (galaxyType == 2 && planetClass != 1 && s.Vicious > r.NextFraction()) level = 2;
            if (biome == GasGiant && level >= 2) level = 0;
            if (level == 2 && s.Corrupt > r.NextFraction()) level = 3;
            return level;
        }
        int check = Slot(s.CheckDifficulty);
        var sentinels = new int[4];
        for (int slot = 0; slot < 4; slot++)
        {
            int level = Slot(slot);
            sentinels[slot] = level == 3 && check != 3 ? 2 : level;
        }
        // Scrap world: the next draw against 0.2 (a constant in NMS.exe), where scrapyards may
        // stand at this planet's building density (planetbuildingtable.mbin ScrapYard Density).
        bool scrap = false;
        if (biome != Weird && 0.2f >= rng.NextFraction())
        {
            int density = planetClass == 1 ? 3 : biome == Weird ? 4 : subType == Infested ? 5
                : biome == Waterworld ? 6 : biome == GasGiant ? 7 : biome == Dead ? 0 : 2;
            scrap = t.ScrapYardDensity is { } d && density < d.Length && d[density] != 0f;
        }
        return (flora, fauna, sentinels, scrap);
    }

    /// <summary>A 4-level weighted pick: the first level whose running total passes the target.</summary>
    private static int Level(ref Rng rng, float[] weights)
    {
        float total = 0f;
        foreach (var w in weights) total += w;
        float target = rng.NextFraction() * total, cum = 0f;
        for (int k = 0; k < weights.Length; k++)
        {
            if (!(target < cum) && cum + weights[k] > target) return k;
            cum += weights[k];
        }
        return 0;
    }

    /// <summary>The weather step (0x141696760) up to storms; rainbows (its last draw) are left out.</summary>
    private static (int Weather, bool Extreme, int Storms) Weather(Rng rng, int biome, int subType, int planetClass, int star,
        int galaxyType, bool inGasGiantSystem, bool corruptedSentinels, PlanetProfileTables t)
    {
        int weather = 0;
        var weights = BiomeWeather(t, biome, subType)?[star];
        float total = 0f;
        if (weights is not null) foreach (var w in weights) total += w;
        float target = rng.NextFraction() * total;
        if (weights is not null)
        {
            float cum = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                cum += weights[i];
                if (cum >= target) { weather = i; break; }
            }
        }
        if (planetClass == 1) { if (biome != Barren && weather == DustWeather) weather = ScorchedWeather; }
        else if (weather == HumidWeather && corruptedSentinels) weather = WeirdWeather;

        bool extreme = false;
        int storms = 0;
        if ((!inGasGiantSystem || biome == GasGiant) && planetClass != 1 && planetClass != 2)
        {
            extreme = t.ExtremePlanetChance[star] > rng.NextFraction();
            var (low, high) = weather < t.Storms.Length ? (t.Storms[weather].Low, t.Storms[weather].High) : (0f, 0f);
            if (low > rng.NextFraction()) storms = 1;
            else storms = high > rng.NextFraction() || extreme ? 2 : 0;
        }
        if (galaxyType == 2)
        {
            if (planetClass == 1) extreme = true;
            if (t.EmptyGalaxyExtremeChance > rng.NextFraction()) extreme = true;
            if (t.EmptyGalaxyStormChance > rng.NextFraction()) storms = 2;
        }
        if (biome is Dead or Weird) { extreme = false; storms = 0; }
        else if (biome == GasGiant) { extreme = true; storms = 0; }
        return (weather, extreme, storms);
    }

    /// <summary>The biome file the game uses: the subtype's, else the first subtype that has one (0x14024b270).</summary>
    private static float[][]? BiomeWeather(PlanetProfileTables t, int biome, int subType)
    {
        if (biome < 0 || biome >= t.BiomeWeather.Length) biome = 0;
        var bySub = t.BiomeWeather[biome];
        if (subType >= 0 && subType < bySub.Length && bySub[subType] is { } own) return own;
        foreach (var w in bySub) if (w is not null) return w;
        return null;
    }
}
