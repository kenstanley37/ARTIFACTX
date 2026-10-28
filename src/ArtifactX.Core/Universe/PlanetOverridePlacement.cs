using System.Numerics;

namespace ArtifactX.Core.Universe;

/// <summary>One building class's override rules from planetbuildingtable.mbin (GcBuildingDefinitionData): how many
/// "override" buildings every planet gets (the portal, shelter, base, distress signal and start spawns), where they may
/// stand, and the radius of the flatness ring on prime planets (OverrideRadius, or <paramref name="ModelRadius"/> - half
/// the model's ground diagonal - when that is -1).</summary>
/// <param name="WaterPlacement">TkNoiseFlattenOptions.WaterPlacement: 0 None, 1 OnWater, 2 Underwater, 3 UnderwaterOnly.</param>
public sealed record BuildingOverrideRule(int Class, int Overrides, int OverridesWaterworld, int WaterPlacement,
    float OverrideRadius, float MinHeight, float MaxHeight, bool EnabledWhenPlanetHasNoNPCs, float ModelRadius = 0f);

/// <summary>What placing a planet's override buildings needs, all generated from its address: the planet seed, size,
/// Voronoi grid, prime flag, biome, sea level, centre in system space (on the 1,000-unit grid) and terrain data.</summary>
/// <param name="SeaMoon">GcTerrainGlobals SeaLevelMoon and SeaLevelGasGiant: water-only buildings skip these levels.</param>
public sealed record OverridePlanet(ulong PlanetSeed, float Radius, int Divisions, int SectorSeed, bool Prime, int Biome,
    float SeaLevel, Vector3 Centre, PlanetTerrainData Terrain, float SeaMoon, float SeaGasGiant);

/// <summary>An override building: its class (GcBuildingClassification: 2 Shelter, 16 Portal, 20 DistressSignal,
/// 22 Base, 28 GameStartSpawn, 32 WaterDistressSignal...), site key (the marker's BuildingSeed) and ground position in
/// system space; Placed is false when the game would find no spot either.</summary>
public readonly record struct OverrideBuilding(int Class, ulong Key, Vector3 Position, bool Placed);

/// <summary>
/// Where a planet's portal, shelter, base, distress signal and start spawns stand - a port of NMS.exe's override
/// placement 0x141635D10 (build 183791, docs/research/PLANET_BUILDING_PLACEMENT.md):
/// - records: per class in class order, NumOverridesToGenerate (the Waterworld count on waterworlds when set); none for
///   NPC-only classes without NPCs, or UnderwaterOnly without water (a sea of 0 or exactly the moon/gas giant level);
/// - phase 1: on an MWC seeded with the planet seed, the start-area chain: an anchor at a uniform random direction,
///   then points 500-600 m from it and a last one 800-1,000 m (bands growing with the attempts, reset every 101,
///   20,000 at most); each candidate must pass the accept test 0x141631160 (nearest site, terrain height, flatness,
///   water and height rules) and have a new site key;
/// - phase 2: distress signal (and the water distress signal on waterworlds) take the chain's head, start spawns its
///   back, the shelter the last point, while the chain lasts; everything else (the portal, the base) a random site.
/// Checked 2026-10-28 against the game's live records: 102/102 on 12 planets of two systems (purple, a moon, a
/// waterworld), positions within 0.28 m (float32 at ~2 million units); predicted portal markers written to a test
/// save led to the portal on all 5 planets tried in game.
/// </summary>
public static class PlanetOverridePlacement
{
    private const int Shelter = 2, DistressSignal = 20, GameStartSpawn = 28, WaterDistressSignal = 32, Waterworld = 14;
    private const int UnderwaterOnly = 3;
    private const float Growth = 0.002f, CrashMin = 500f, CrashMax = 600f, ShelterMin = 800f, ShelterMax = 1000f;   // GcBuildingGlobals
    private const float SeaAbove = 5f, WaterOnlyDepth = 35f, MinBelowSea = 60f, MaxSlope = 1f;
    private const int RoundAttempts = 100, MaxAttempts = 20000, RandomTries = 5000;
    private static readonly float TwoPi = TerrainNoise.Bits(0x40C90FDB);

    private readonly record struct Rule(int Class, int WaterPlacement, float MinHeight, float MaxHeight, float Ring);

    /// <summary>The planet's override buildings in the game's record order.</summary>
    public static OverrideBuilding[] Place(OverridePlanet planet, IReadOnlyList<BuildingOverrideRule> rules, bool hasNpcs = true)
    {
        var records = Records(planet, rules, hasNpcs);
        var result = new OverrideBuilding[records.Count];
        if (records.Count == 0) return result;
        var rng = PlanetBuildingSites.Mwc.Start(planet.PlanetSeed);
        bool water = planet.Biome == Waterworld;
        int need = records.Count(r => water ? r.Class is GameStartSpawn or WaterDistressSignal
            : r.Class is Shelter or DistressSignal or GameStartSpawn) - 1;

        // Phase 1: the start-area chain.
        var chain = new List<(ulong Key, Vector3 Position)>();
        (ulong Key, Vector3 Position)? last = null;
        int attempts = 0, round = 0;
        bool chainDone = true;
        while (!(chain.Count >= need && last is not null))
        {
            float g = attempts * Growth;
            float s = g * g;
            var dir = Direction(ref rng);
            int have = chain.Count;
            if (have > 0)
            {
                var anchor = chain[0].Position - planet.Centre;
                float dist = s + (have == need ? ShelterMax : CrashMax);
                var q = new Vector3(dir.X * dist + anchor.X, dir.Y * dist + anchor.Y, dir.Z * dist + anchor.Z);
                float m = MathF.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z);
                dir = new Vector3(q.X / m, q.Y / m, q.Z / m);
            }
            var point = new Vector3(dir.X * planet.Radius, dir.Y * planet.Radius, dir.Z * planet.Radius);
            if (Accept(planet, point, records[0], out ulong key, out var position) && chain.All(c => c.Key != key))
            {
                bool good = true;
                if (have > 0)
                {
                    float lo = TerrainNoise.Max(0f, (have == need ? ShelterMin : CrashMin) - s);
                    float hi = s + (have == need ? ShelterMax : CrashMax);
                    var a = chain[0].Position;
                    float dx = position.X - a.X, dy = position.Y - a.Y, dz = position.Z - a.Z;
                    float dd = dx * dx + dy * dy + dz * dz;
                    good = !(lo * lo > dd || dd > hi * hi);
                }
                if (good)
                {
                    if (have == need) last = (key, position);
                    else chain.Add((key, position));
                }
            }
            attempts++; round++;
            if (attempts > MaxAttempts) { chainDone = false; break; }
            if (round > RoundAttempts) { chain.Clear(); last = null; round = 0; }
        }
        if (!chainDone) { chain.Clear(); last = null; }

        // Phase 2 (0x14163661C).
        for (int i = 0; i < records.Count; i++)
        {
            var r = records[i];
            int c = r.Class;
            if ((c == DistressSignal || (water && c == WaterDistressSignal)) && chain.Count > 0)
            {
                result[i] = new OverrideBuilding(c, chain[0].Key, chain[0].Position, true);
                chain.RemoveAt(0);
            }
            else if (c == GameStartSpawn && chain.Count > 0)
            {
                result[i] = new OverrideBuilding(c, chain[^1].Key, chain[^1].Position, true);
                chain.RemoveAt(chain.Count - 1);
            }
            else if ((c == Shelter || (water && c is 31 or 24)) && last is { } l)
                result[i] = new OverrideBuilding(c, l.Key, l.Position, true);
            else
            {
                result[i] = new OverrideBuilding(c, 0, default, false);
                for (int t = 0; t < RandomTries; t++)
                {
                    var d = Direction(ref rng);
                    if (Accept(planet, new Vector3(d.X * planet.Radius, d.Y * planet.Radius, d.Z * planet.Radius), r, out ulong k, out var p))
                    {
                        result[i] = new OverrideBuilding(c, k, p, true);
                        break;
                    }
                }
            }
        }
        return result;
    }

    private static List<Rule> Records(OverridePlanet planet, IReadOnlyList<BuildingOverrideRule> rules, bool hasNpcs)
    {
        bool waterworld = planet.Biome == Waterworld;
        float sea = planet.SeaLevel;
        bool noWater = !(sea > 0f) || sea == planet.SeaMoon || sea == planet.SeaGasGiant;
        var list = new List<Rule>();
        foreach (var row in rules.OrderBy(r => r.Class))
        {
            int n = waterworld && row.OverridesWaterworld >= 0 ? row.OverridesWaterworld : row.Overrides;
            if (!row.EnabledWhenPlanetHasNoNPCs && !hasNpcs) n = 0;
            if (row.WaterPlacement == UnderwaterOnly && noWater) n = 0;
            float ring = PlanetTerrain.RingRadius(planet.Prime, row.OverrideRadius > 0f ? row.OverrideRadius : row.ModelRadius);
            for (int k = 0; k < n; k++) list.Add(new Rule(row.Class, row.WaterPlacement, row.MinHeight, row.MaxHeight, ring));
        }
        return list;
    }

    /// <summary>A uniform random direction from two draws (the game's sin/cos).</summary>
    private static Vector3 Direction(ref PlanetBuildingSites.Mwc rng)
    {
        float u1 = Fraction(rng.Word()), u2 = Fraction(rng.Word());
        float y = 1f - (u1 + u1);
        float r = MathF.Sqrt(TerrainNoise.Max(0f, 1f - y * y));
        float phi = u2 * TwoPi;
        return new Vector3(GameTrig.Cos(phi) * r, y, GameTrig.Sin(phi) * r);
    }

    /// <summary>The accept test 0x141631160 for a planet-relative candidate on the sphere: the nearest site, the
    /// terrain height and flatness there, the water and height rules; on success the site key and ground position.</summary>
    private static bool Accept(OverridePlanet planet, Vector3 candidate, Rule rule, out ulong key, out Vector3 position)
    {
        var (siteKey, face, cell) = NearestSite(candidate, planet.SectorSeed, planet.Radius, planet.Divisions);
        key = siteKey;
        position = default;
        var fe = PlanetBuildingSites.Feature(face, cell);
        float ln = MathF.Sqrt(fe.X * fe.X + fe.Y * fe.Y + fe.Z * fe.Z);
        float ax = fe.X / ln * planet.Radius, ay = fe.Y / ln * planet.Radius, az = fe.Z / ln * planet.Radius;
        var terrain = planet.Terrain;
        float h = PlanetTerrain.Height(terrain, ax, ay, az);
        float slope = PlanetTerrain.Flatness(terrain, ax, ay, az, planet.Radius, rule.Ring);
        float sea = planet.SeaLevel;
        bool under = sea + SeaAbove > h && sea > 0f;
        bool ok = planet.Biome == Waterworld ? true
            : rule.WaterPlacement == UnderwaterOnly ? under && !(h > sea - WaterOnlyDepth)
            : !(under && rule.WaterPlacement is not (1 or 2)) && !(sea - MinBelowSea > h);
        if (rule.MinHeight > h || h > rule.MaxHeight || !ok || slope < 0f || MaxSlope < slope) return false;
        float n = MathF.Sqrt(ax * ax + ay * ay + az * az);
        position = new Vector3(ax + ax / n * h + planet.Centre.X, ay + ay / n * h + planet.Centre.Y, az + az / n * h + planet.Centre.Z);
        return true;
    }

    // Face axis and sign of the Voronoi worker's faces (0 -Z, 1 +Z, 2 +X, 3 -X, 4 -Y, 5 +Y).
    private static readonly int[] Axis = { 2, 2, 0, 0, 1, 1 };
    private static readonly int[] Sign = { -1, 1, 1, -1, -1, 1 };

    /// <summary>The site a planet-relative point falls in (the Voronoi query 0x142DE7B30): for each face the point
    /// faces, the cells around its projection; a cell counts only if its jittered feature point stays on that face; the
    /// nearest feature point on the sphere wins. Checked 600/600 against the game's code in an emulator.</summary>
    private static (ulong Key, int Face, Vector3 Cell) NearestSite(Vector3 p, int sectorSeed, float radius, int divisions)
    {
        // Float32 where the verified port computes in float32 (the projection, each distance term and their sum) and
        // double where it does (the feature point's length and scaling), so ties break the same way.
        int half = divisions / 2;
        float best = float.MaxValue;
        bool found = false;
        (ulong, int, Vector3) result = default;
        float[] pv = { p.X, p.Y, p.Z };
        for (int face = 0; face < 6; face++)
        {
            int ax = Axis[face];
            if (pv[ax] * Sign[face] <= 0) continue;
            int o1 = ax == 0 ? 1 : 0, o2 = ax == 2 ? 1 : 2;
            float axis = MathF.Abs(pv[ax]);
            int f1 = (int)MathF.Floor(pv[o1] / axis * half), f2 = (int)MathF.Floor(pv[o2] / axis * half);
            for (int du = -2; du <= 2; du++)
                for (int dv = -2; dv <= 2; dv++)
                {
                    int u = f1 + du, v = f2 + dv;
                    if (Math.Abs(u) > half || Math.Abs(v) > half) continue;
                    var c = new float[3];
                    c[ax] = Sign[face] * half; c[o1] = u; c[o2] = v;
                    var cell = new Vector3(c[0], c[1], c[2]);
                    var fp = PlanetBuildingSites.Feature(face, cell);
                    float[] f = { fp.X, fp.Y, fp.Z };
                    if (!(f[ax] * Sign[face] > 0 && Math.Abs(f[ax]) > Math.Abs(f[o1]) && Math.Abs(f[ax]) > Math.Abs(f[o2]))) continue;
                    double m = Math.Sqrt((double)f[0] * f[0] + (double)f[1] * f[1] + (double)f[2] * f[2]);
                    float dist = 0f;
                    for (int j = 0; j < 3; j++)
                    {
                        float e = (float)(f[j] / m * radius) - pv[j];
                        dist += e * e;
                    }
                    if (!found || dist < best)
                    {
                        found = true;
                        best = dist;
                        result = (PlanetBuildingSites.Key(fp, sectorSeed), face, cell);
                    }
                }
        }
        return result;
    }

    private static float Fraction(uint word) => (float)(word * 2.3283064370807974e-10);
}
