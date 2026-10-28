using System.Numerics;

namespace ArtifactX.Core.Universe;

/// <summary>One building class's row of planetbuildingtable.mbin (GcBuildingDefinitionTable.BuildingPlacement[class]):
/// its density per GcBuildingDensityLevels (8 values) and the planet restrictions and NPC rule that decide whether a
/// planet has the class at all.</summary>
public sealed record BuildingClassPlacement(float[] Density, bool RequiresCorruptSentinels, bool RequiresRelicWorld,
    bool RequiresScrapWorld, bool RequiresWater, bool EnabledWhenPlanetHasNoNPCs);

/// <summary>What the building pass needs to know about a planet, all of it generated: the density level
/// (<see cref="PlanetBuildingSites.DensityLevel"/>), corrupt sentinels and scrap from
/// <see cref="PlanetProfileGenerator"/>, relic from the biome, water from the terrain.</summary>
public sealed record BuildingPlanetFacts(int DensityLevel, bool CorruptSentinels, bool RelicWorld, bool ScrapWorld,
    bool Water, bool HasNpcs = true);

/// <summary>A building site: the cube face and cell of the game's planet Voronoi grid, its key (the per-site seed
/// the scanner saves as a marker's BuildingSeed) and the planet-relative point on the sphere of the planet radius.
/// The building's class is a GcBuildingClassification value (11 Depot, 5 Shop, 24 CrashedFreighter, ...).</summary>
public readonly record struct BuildingSite(int Face, int U, int V, ulong Key, Vector3 Point, int Class)
{
    /// <summary>Latitude and longitude in degrees, the convention of NmsPlanetCoordinates (+y north).</summary>
    public (double Latitude, double Longitude) Coordinates
    {
        get
        {
            double r = Math.Sqrt((double)Point.X * Point.X + (double)Point.Y * Point.Y + (double)Point.Z * Point.Z);
            return (Math.Asin(Math.Clamp(Point.Y / r, -1, 1)) * 180 / Math.PI, Math.Atan2(Point.X, Point.Z) * 180 / Math.PI);
        }
    }
}

/// <summary>
/// Where a planet's buildings stand and which class each is - a port of NMS.exe's building pass (build 180836,
/// traced 2026-10-28, docs/research/PLANET_BUILDING_PLACEMENT.md):
/// - seeds: the planet pipeline 0x14168EE20 makes 26 stage seeds from the planet seed; stage 3 reseeds the MWC for
///   the building stage 0x14160B500, whose two draws are VoronoiPointSeed and VoronoiSectorSeed (Next(10000) + 1);
/// - sites: every cell of a gnomonic cube grid (VoronoiPointDivisions per face edge) is a site; the Voronoi worker
///   0x1419883E0 jitters the cell into a feature point (a Wang hash, scale 0.01) and keys it with three Mix64 and
///   three Hash128to64 against the sector seed. The site's point is the feature point scaled to the planet radius;
/// - choice: the site builder 0x141605800 seeds an MWC from the key (stepped once by its caller 0x14128EB5B),
///   picks slot (int)(Next * 999) of the slot table and builds when the slot has a building and its probability
///   beats the next draw;
/// - slot table: class c owns slots 10c..10c+9: floor(density) slots with probability 1, a fractional rest gives one
///   more slot with that fraction (classes the planet doesn't have: no building).
/// Checked against the running game (read-only memory reads, Oystana, 2026-10-28): stage seeds and Voronoi seeds 5/5
/// planets, 2,592/2,592 live site records keyed and predicted built, site points within 1.5 cm of the game's, slot
/// tables 1000/1000 on 5/5 planets, the Depot and Factory markers on Peak XV exact.
/// Not modelled: terrain may still drop a site (rare inside the loaded area: 1 of 45 enclosed cells), the two
/// settlement hub classes need <see cref="BuildingPlanetFacts.HasNpcs"/>, the building's yaw and cluster layout, and
/// which of the class's models stands there.
/// </summary>
public static class PlanetBuildingSites
{
    private const ulong Multiplier = 0x5A76F899;
    private const ulong HashMultiplier = 0x9DDFEA08EB382D69UL;
    private const int Weird = 7, Dead = 6, Waterworld = 14, GasGiant = 15, Infested = 0x18, Structure = 3;

    /// <summary>Planet radius by GcPlanetSize (Large, Medium, Small, Moon) as read from the running game (5 planets
    /// 2026-10-28); Giant is not known yet.</summary>
    private static readonly float[] Radii = { 130048f, 98304f, 64512f, 15360f };

    // Face axes of the Voronoi worker (NMS.exe tables 0x1450AF140 and 0x1450AF0F0, indexed by face).
    private static readonly sbyte[,] T1 = { { 1, 0, 0 }, { -1, 0, 0 }, { 0, 1, 0 }, { 0, -1, 0 }, { 0, 0, -1 }, { 0, 0, 1 } };
    private static readonly sbyte[,] T2 = { { 0, 1, 0 }, { 0, -1, 0 }, { 0, 0, 1 }, { 0, 0, -1 }, { 1, 0, 0 }, { -1, 0, 0 } };

    /// <summary>Planet radius for a GcPlanetSize, or null for a size not measured yet.</summary>
    public static float? Radius(int planetSize) => planetSize >= 0 && planetSize < Radii.Length ? Radii[planetSize] : null;

    /// <summary>GcPlanetData.BuildingLevel (GcBuildingDensityLevels) from the planet's type, as set at 0x141695B22:
    /// the safe start planet (class 1) Full, then Weird, Infested (HalfWeird), Waterworld, GasGiant, Dead, else Mid.</summary>
    public static int DensityLevel(int biome, int subType, int planetClass) =>
        planetClass == 1 ? 3 : biome == Weird ? 4 : subType == Infested ? 5 : biome == Waterworld ? 6
            : biome == GasGiant ? 7 : biome == Dead ? 0 : 2;

    /// <summary>A relic world for RequiresRelicWorld (0x14160B9C0): not Weird, subtype 3 (Structure).</summary>
    public static bool IsRelicWorld(int biome, int subType) => biome != Weird && subType == Structure;

    /// <summary>VoronoiPointSeed and VoronoiSectorSeed for a planet seed (SystemGenerator.Bodies' PlanetSeeds).</summary>
    public static (int PointSeed, int SectorSeed) VoronoiSeeds(ulong planetSeed)
    {
        var rng = Mwc.Start(StageSeed(planetSeed, 3));
        int point = (int)rng.Next(10000) + 1;
        return (point, (int)rng.Next(10000) + 1);
    }

    /// <summary>VoronoiPointDivisions: 2 * ceil((2R / 250) * pi/6 * 0.5), cells per cube face edge.</summary>
    public static int Divisions(float radius) => 2 * (int)MathF.Ceiling(2f * radius / 250f * 0.5235988f * 0.5f);

    /// <summary>The 1,000-entry slot table by class (-1 = no building) and probability, from the class table and the
    /// planet; a class is present when the planet meets its restrictions and NPC rule.</summary>
    public static (int Class, float Probability)[] Slots(IReadOnlyList<BuildingClassPlacement?> table, BuildingPlanetFacts planet)
    {
        var slots = new (int, float)[1000];
        Array.Fill(slots, (-1, 1f));
        for (int c = 0; c < table.Count && c < 100; c++)
        {
            if (table[c] is not { } row || !Has(row, planet) || planet.DensityLevel >= row.Density.Length) continue;
            float k = row.Density[planet.DensityLevel];
            int whole = (int)MathF.Floor(k);
            float fraction = k - whole;
            for (int j = 0; j < 10; j++)
            {
                if (j < whole) slots[10 * c + j] = (c, 1f);
                else if (j == whole && fraction > 0) slots[10 * c + j] = (c, fraction);
            }
        }
        return slots;
    }

    private static bool Has(BuildingClassPlacement row, BuildingPlanetFacts p) =>
        (!row.RequiresCorruptSentinels || p.CorruptSentinels) && (!row.RequiresRelicWorld || p.RelicWorld)
        && (!row.RequiresScrapWorld || p.ScrapWorld) && (!row.RequiresWater || p.Water)
        && (row.EnabledWhenPlanetHasNoNPCs || p.HasNpcs);

    /// <summary>The class built at a site key, or -1 for no building.</summary>
    public static int ClassAt(ulong key, (int Class, float Probability)[] slots)
    {
        var rng = Mwc.Start(key);
        rng.Word();
        int index = (int)(Fraction(rng.Word()) * 999f);
        var (cls, probability) = slots[index];
        if (cls < 0 || !(probability > Fraction(rng.Word()))) return -1;
        return cls;
    }

    /// <summary>The site of a cell: face 0 -Z, 1 +Z, 2 +X, 3 -X, 4 -Y, 5 +Y; u and v are the cell's two other
    /// coordinates in face units (-divisions/2 .. divisions/2), in x, y, z order.</summary>
    public static BuildingSite Site(int face, int u, int v, int divisions, float radius, int sectorSeed,
        (int Class, float Probability)[] slots)
    {
        int half = divisions / 2;
        var cell = face switch
        {
            0 => new Vector3(u, v, -half), 1 => new Vector3(u, v, half),
            2 => new Vector3(half, u, v), 3 => new Vector3(-half, u, v),
            4 => new Vector3(u, -half, v), _ => new Vector3(u, half, v),
        };
        var feature = Feature(face, cell);
        ulong key = Key(feature, sectorSeed);
        return new BuildingSite(face, u, v, key, Vector3.Normalize(feature) * radius, ClassAt(key, slots));
    }

    /// <summary>Every built site of the class(es) wanted on one face (divisions² cells; about 1.2 million on a
    /// Large planet's face, so callers should run this off the UI thread).</summary>
    public static IEnumerable<BuildingSite> Sites(int face, int divisions, float radius, int sectorSeed,
        (int Class, float Probability)[] slots, Func<int, bool>? wanted = null)
    {
        int half = divisions / 2;
        for (int u = -half; u <= half; u++)
            for (int v = -half; v <= half; v++)
            {
                var site = Site(face, u, v, divisions, radius, sectorSeed, slots);
                if (site.Class >= 0 && (wanted is null || wanted(site.Class))) yield return site;
            }
    }

    /// <summary>The planet facts the slot table needs, generated from the address: density level, corrupt sentinels,
    /// relic, scrap, water and NPCs (an abandoned system has none). Null when the planet can't be generated.</summary>
    public static BuildingPlanetFacts? FactsFor(ulong portalCode, int galaxy, int planetIndex, BiomeTables biomes,
        PlanetProfileTables profiles)
    {
        var types = PlanetGenerator.Generate(portalCode, galaxy, biomes);
        var seeds = SystemGenerator.Bodies(portalCode, galaxy).PlanetSeeds;
        if (planetIndex < 0 || planetIndex >= types.Length || planetIndex >= seeds.Length) return null;
        var body = types[planetIndex];
        var attributes = SystemGenerator.Attributes(portalCode, galaxy);
        var profile = PlanetProfileGenerator.Generate(seeds[planetIndex], planetIndex, body, (int)attributes.Star,
            PlanetGenerator.GalaxyType(galaxy), attributes.GasGiantPlanet, profiles, attributes.Uncharted, attributes.Abandoned);
        return new BuildingPlanetFacts(DensityLevel(body.Biome, body.SubType, body.Class),
            profile.Sentinels[Math.Clamp(profiles.Sentinels.CheckDifficulty, 0, 3)] == 3, IsRelicWorld(body.Biome, body.SubType),
            profile.ScrapWorld, profile.Water, !attributes.Abandoned);
    }

    /// <summary>
    /// The built sites of the wanted class(es) nearest a direction from the planet's centre, nearest first: only the
    /// cells within <paramref name="cellRange"/> of the point on each face it faces (a cell counts for the face its
    /// jittered point lies on, as in the game's Voronoi query), so it stays cheap on any planet. Checked 2026-10-28:
    /// the crashed ship Final Eokak III on Ribhakyam stands at the predicted DistressSignal site (+66.88, +81.50; the
    /// Analysis Visor read +66.88, +81.49 at its cockpit).
    /// </summary>
    public static List<BuildingSite> SitesNear(Vector3 direction, int divisions, float radius, int sectorSeed,
        (int Class, float Probability)[] slots, Func<int, bool> wanted, int count, int cellRange = 60)
    {
        int half = divisions / 2;
        var dir = Vector3.Normalize(direction);
        var found = new List<BuildingSite>();
        for (int face = 0; face < 6; face++)
        {
            int axis = face switch { 0 or 1 => 2, 2 or 3 => 0, _ => 1 };
            int sign = face is 1 or 2 or 5 ? 1 : -1;
            float along = axis == 0 ? dir.X : axis == 1 ? dir.Y : dir.Z;
            if (along * sign <= 0) continue;
            // The face's two other coordinates in x, y, z order, as Site() takes them.
            float a = axis == 0 ? dir.Y : dir.X, b = axis == 2 ? dir.Y : dir.Z;
            int cu = (int)MathF.Floor(a / MathF.Abs(along) * half), cv = (int)MathF.Floor(b / MathF.Abs(along) * half);
            for (int u = Math.Max(-half, cu - cellRange); u <= Math.Min(half, cu + cellRange); u++)
                for (int v = Math.Max(-half, cv - cellRange); v <= Math.Min(half, cv + cellRange); v++)
                {
                    var site = Site(face, u, v, divisions, radius, sectorSeed, slots);
                    if (site.Class < 0 || !wanted(site.Class)) continue;
                    var f = Vector3.Normalize(site.Point);
                    float fa = MathF.Abs(axis == 0 ? f.X : axis == 1 ? f.Y : f.Z);
                    if ((axis == 0 ? f.X : axis == 1 ? f.Y : f.Z) * sign <= 0 || (axis != 0 && MathF.Abs(f.X) >= fa)
                        || (axis != 1 && MathF.Abs(f.Y) >= fa) || (axis != 2 && MathF.Abs(f.Z) >= fa)) continue;
                    found.Add(site);
                }
        }
        return found.OrderByDescending(s => Vector3.Dot(Vector3.Normalize(s.Point), dir)).Take(count).ToList();
    }

    /// <summary>The worker's jittered feature point of a cell.</summary>
    public static Vector3 Feature(int face, Vector3 cell)
    {
        float x = cell.X, y = cell.Y, z = cell.Z;
        float a = Jitter(Bits(x * 113f + y + z * 57f));
        float b = Jitter(Bits(y * 113f + x * 57f + z));
        float c = Jitter(Bits(y * 57f + x + z * 113f));
        return new Vector3((T2[face, 0] + T1[face, 0]) * c + x, (T2[face, 1] + T1[face, 1]) * b + y,
            (T2[face, 2] + T1[face, 2]) * a + z);
    }

    /// <summary>A site's key from its feature point and the planet's sector seed.</summary>
    public static ulong Key(Vector3 feature, int sectorSeed)
    {
        ulong hx = Mix64(((ulong)Bits(feature.X) << 32) | Bits(feature.X * -2f));
        ulong hy = Mix64(((ulong)Bits(feature.Y) << 32) | Bits(feature.Y * -2f));
        ulong hz = Mix64(((ulong)Bits(feature.Z) << 32) | Bits(feature.Z * -2f));
        return Combine((uint)sectorSeed, Combine(hz, Combine(hy, hx)));
    }

    private static float Jitter(uint u)
    {
        uint x = ((u ^ 0x3D0000u) >> 16) ^ u;
        x *= 9;
        x ^= x >> 4;
        x *= 0x1B873593u;
        x ^= x >> 15;
        return (BitConverter.UInt32BitsToSingle((x & 0x7FFFFF) | 0x3F800000) - 1f) * 0.01f;
    }

    private static uint Bits(float f) => BitConverter.SingleToUInt32Bits(f);

    internal static ulong StageSeed(ulong planetSeed, int stage)
    {
        var rng = Mwc.Start(planetSeed);
        ulong seed = 0;
        for (int i = 0; i <= stage; i++)
        {
            uint first = rng.Word(), second = rng.Word();
            seed = Mix64(((ulong)second << 32) | first);
        }
        return seed;
    }

    private static ulong Mix64(ulong r)
    {
        r = ((r >> 33) ^ r) * 0x64DD81482CBD31D7UL;
        r = ((r >> 33) ^ r) * 0xE36AA5C613612997UL;
        return (r >> 33) ^ r;
    }

    private static ulong Combine(ulong u, ulong v)
    {
        ulong a = (u ^ v) * HashMultiplier;
        a ^= a >> 47;
        ulong b = (v ^ a) * HashMultiplier;
        b ^= b >> 47;
        return b * HashMultiplier;
    }

    private static float Fraction(uint word) => (float)(word * 2.3283064370807974e-10);

    internal struct Mwc
    {
        private uint _lo, _hi;
        public static Mwc Start(ulong seed)
        {
            uint lo = (uint)seed;
            return new Mwc { _lo = lo == 0 ? 1u : lo, _hi = ((lo >> 16) | (lo << 16)) ^ (uint)(seed >> 32) ^ lo };
        }
        public uint Word()
        {
            ulong t = _lo * Multiplier + _hi;
            _lo = (uint)t; _hi = (uint)(t >> 32);
            return _lo;
        }
        public uint Next(uint range) => (uint)(((ulong)Word() * range) >> 32);
    }
}
