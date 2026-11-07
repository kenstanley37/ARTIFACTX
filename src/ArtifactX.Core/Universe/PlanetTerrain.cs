namespace ArtifactX.Core.Universe;

/// <summary>One terrain noise layer (TkNoiseUberLayerData), as the terrain settings hold it and as a planet gets it.</summary>
public sealed record TerrainNoiseLayer(UberNoiseSettings Noise, int DebugNoiseType, int Octaves, float Height,
    float HeightOffset, int MaximumLOD, int Offset, float PlateauRegionSize, int PlateauSharpness, float PlateauStratas,
    float RegionGain, float RegionRatio, float RegionScale, int SeedOffset, float SmoothRadius, float TileBlendMeters,
    int VoxelType, int WaterFade, float Width, bool Active, bool Subtract);

/// <summary>The parts of a planet's TkVoxelGeneratorData the terrain height reads: its 8 noise layers, base seed,
/// base level without a sea and sea level.</summary>
public sealed record PlanetTerrainData(TerrainNoiseLayer[] Layers, ulong BaseSeed, float NoSeaBaseLevel, float SeaLevel);

/// <summary>
/// A planet's terrain, from its seed and the terrain settings - a port of NMS.exe build 183791
/// (docs/research/PLANET_BUILDING_PLACEMENT.md):
/// - data: the terrain step 0x1416C6BF0 on the planet's stage-4 stream, after the draws Core's
///   <see cref="PlanetProfileGenerator.TerrainChoiceFor"/> counts: per noise layer the setting's Active flag, one draw
///   against the biome file's chance, then the layer builder 0x1416C4F60 (defaults, a few fields copied from the Min
///   set, the random-rock voxel pick 0x1416C4E30, then 26 fields drawn between Min and Max in the game's order);
///   layer 7's sea rules, layer 4 off without a sea, layer 6 off on moons.
/// - height: 0x14128AA20 (each active layer's uber noise shaped by region mask, gain, plateaus and quantising; layers
///   7 and 6 make the base, 0-3 add or carve), the cheap base height 0x14128CA10 and the flatness test 0x14128C350.
/// Checked 2026-10-28: terrain data byte for byte against the live data of 12 planets (two systems, purple included);
/// heights 450/450 bit-exact against the game's code in an emulator (Python port, which this mirrors).
/// </summary>
public static class PlanetTerrain
{
    private const int LayerCount = 8;
    private const float SeaBelowLayer7 = 30f;              // 0x1416C7067: layer 7's Height on a planet with a sea
    private const float SeedStep = 64f;                     // 0x144BECFF0: seed offset step (core and region mask)
    private const float RatioMinimum = 0.5f;                // max(RegionRatio, this) before the square root
    private const float QuantiseMul = 1024f, QuantiseStep = 0.0009765625f;
    private const int RegionOctaves = 3;                    // global 0x145097F6C
    private const float RingDefault = 64f, SlopeScale = 0.015625f;
    private static readonly float RingStep = TerrainNoise.Bits(0x3F490FDB);   // pi/4

    /// <summary>fBm value range by octave count (0x144AFAD00): min, max for 1, 2, 3 octaves.</summary>
    private static readonly float[] RegionRange =
    {
        TerrainNoise.Bits(0xBF4F5C29), TerrainNoise.Bits(0x3F4F5C29), TerrainNoise.Bits(0xBF3AE148),
        TerrainNoise.Bits(0x3F3AE148), TerrainNoise.Bits(0xBF28F5C3), TerrainNoise.Bits(0x3F2B851F),
    };

    /// <summary>The planet's terrain data; null when the setting has no layer values (an older catalog).</summary>
    public static PlanetTerrainData? Generate(ulong planetSeed, TerrainSetting setting, TerrainChoice choice,
        float seaStandard, bool moon, bool deadToWeird)
    {
        if (setting.MinLayers is not { Length: LayerCount } min || setting.MaxLayers is not { Length: LayerCount } max) return null;
        var rng = PlanetBuildingSites.Mwc.Start(PlanetBuildingSites.StageSeed(planetSeed, 4));
        for (int k = 0; k < choice.DrawsBeforeLayers; k++) rng.Word();
        float sea = choice.Sea;
        float[] chances = choice.BiomeFile.NoiseLayers;
        var layers = new TerrainNoiseLayer[LayerCount];
        for (int i = 0; i < LayerCount; i++)
        {
            bool on = min[i].Active && i < chances.Length && chances[i] > Fraction(rng.Word());
            if (i == 7)
            {
                if (sea > seaStandard) on = false;
                else if (deadToWeird || sea > 1f) on = true;
            }
            layers[i] = on ? Build(ref rng, min[i], max[i], choice.BiomeFile) : min[i] with { Active = false };
            if (i == 7 && on && sea > 1f)
                layers[i] = layers[i] with { PlateauRegionSize = 0f, PlateauSharpness = 4, Height = sea - SeaBelowLayer7 };
        }
        if (!(sea > 0f)) layers[4] = layers[4] with { Active = false };
        if (moon) layers[6] = layers[6] with { Active = false };
        return new PlanetTerrainData(layers, planetSeed, setting.NoSeaBaseLevel, sea);
    }

    /// <summary>The layer builder 0x1416C4F60: the voxel pick, then the fields drawn in the game's order.</summary>
    private static TerrainNoiseLayer Build(ref PlanetBuildingSites.Mwc rng, TerrainNoiseLayer min, TerrainNoiseLayer max, BiomeObjects biome)
    {
        int voxel = min.VoxelType;
        if (voxel is 8 or 9)    // RandomRock, RandomRockOrSubstance
        {
            if (biome.RockTile > Fraction(rng.Word())) voxel = rng.Word() >> 31 == 0 ? 1 : 2;
            else if (voxel == 9 && biome.SubstanceTile > Fraction(rng.Word())) voxel = 5 + (int)(((ulong)rng.Word() * 3) >> 32);
            else voxel = 0;
        }
        float F(float lo, float hi, ref PlanetBuildingSites.Mwc r) => Fraction(r.Word()) * (hi - lo) + lo;
        int I(int lo, int hi, ref PlanetBuildingSites.Mwc r) => (int)(((ulong)r.Word() * (ulong)(hi - lo + 1)) >> 32) + lo;
        UberNoiseSettings a = min.Noise, b = max.Noise;
        float width = F(min.Width, max.Width, ref rng);
        float height = F(min.Height, max.Height, ref rng);
        float regionRatio = F(min.RegionRatio, max.RegionRatio, ref rng);
        float regionScale = F(min.RegionScale, max.RegionScale, ref rng);
        int debugNoiseType = I(min.DebugNoiseType, max.DebugNoiseType, ref rng);
        int octaves = I(min.Octaves, max.Octaves, ref rng);
        float sharp = F(a.SharpToRoundFeatures, b.SharpToRoundFeatures, ref rng);
        float amplify = F(a.AmplifyFeatures, b.AmplifyFeatures, ref rng);
        float perturb = F(a.PerturbFeatures, b.PerturbFeatures, ref rng);
        float altitudeErosion = F(a.AltitudeErosion, b.AltitudeErosion, ref rng);
        float ridgeErosion = F(a.RidgeErosion, b.RidgeErosion, ref rng);
        float slopeErosion = F(a.SlopeErosion, b.SlopeErosion, ref rng);
        float lacunarity = F(a.Lacunarity, b.Lacunarity, ref rng);
        float gain = F(a.Gain, b.Gain, ref rng);
        float remapFromMin = F(a.RemapFromMin, b.RemapFromMin, ref rng);
        float remapFromMax = F(a.RemapFromMax, b.RemapFromMax, ref rng);
        float remapToMin = F(a.RemapToMin, b.RemapToMin, ref rng);
        float remapToMax = F(a.RemapToMax, b.RemapToMax, ref rng);
        float slopeGain = F(a.SlopeGain, b.SlopeGain, ref rng);
        float slopeBias = F(a.SlopeBias, b.SlopeBias, ref rng);
        float tileBlend = F(min.TileBlendMeters, max.TileBlendMeters, ref rng);
        float smoothRadius = F(min.SmoothRadius, max.SmoothRadius, ref rng);
        float heightOffset = F(min.HeightOffset, max.HeightOffset, ref rng);
        float plateauStratas = F(min.PlateauStratas, max.PlateauStratas, ref rng);
        float plateauRegionSize = F(min.PlateauRegionSize, max.PlateauRegionSize, ref rng);
        int plateauSharpness = I(min.PlateauSharpness, max.PlateauSharpness, ref rng);
        var noise = new UberNoiseSettings(altitudeErosion, amplify, gain, lacunarity, perturb, remapFromMax, remapFromMin,
            remapToMax, remapToMin, ridgeErosion, sharp, slopeBias, slopeErosion, slopeGain);
        return new TerrainNoiseLayer(noise, debugNoiseType, octaves, height, heightOffset, min.MaximumLOD, min.Offset,
            plateauRegionSize, plateauSharpness, plateauStratas, 1f, regionRatio, regionScale, min.SeedOffset, smoothRadius,
            tileBlend, voxel, min.WaterFade, width, true, min.Subtract);
    }

    /// <summary>The terrain height above the planet radius at a planet-relative point (0x14128AA20).</summary>
    public static float Height(PlanetTerrainData t, float x, float y, float z)
    {
        uint hash = BaseHash(t.BaseSeed);
        var l = t.Layers;
        float h7 = l[7].Active ? LayerValue(l[7], x, y, z, Seed(l[7], 7, hash)) : 0f;
        float h6 = l[6].Active ? LayerValue(l[6], x, y, z, Seed(l[6], 6, hash)) : 0f;
        float b = TerrainNoise.Max(h6, 0f);
        if (l[7].Active) b = h7 / l[7].Height * b + h7;
        float acc = t.NoSeaBaseLevel;
        for (int i = 0; i < 4; i++)
        {
            if (!l[i].Active) continue;
            float v = LayerValue(l[i], x, y, z, Seed(l[i], i, hash));
            acc = l[i].Subtract ? TerrainNoise.Min(acc - v, acc) : TerrainNoise.Max(acc, v);
        }
        return acc + b;
    }

    /// <summary>The cheap base height 0x14128CA10: layers 7 and 6 only, at the point projected onto the sphere.</summary>
    public static float BaseHeight(PlanetTerrainData t, float x, float y, float z, float radius)
    {
        float inv = 1f / MathF.Sqrt(x * x + y * y + z * z);
        float qx = x * inv * radius, qy = y * inv * radius, qz = z * inv * radius;
        uint hash = BaseHash(t.BaseSeed);
        var l = t.Layers;
        float h7 = l[7].Active ? LayerValue(l[7], qx, qy, qz, Seed(l[7], 7, hash)) : 0f;
        float h6 = l[6].Active ? LayerValue(l[6], qx, qy, qz, Seed(l[6], 6, hash)) : 0f;
        float b = TerrainNoise.Max(h6, 0f);
        if (l[7].Active) b = h7 / l[7].Height * b + h7;
        return b;
    }

    /// <summary>The site flatness test 0x14128C350 (non-settlement path): the largest height difference between the
    /// site and 8 points on a ring around it, per 64 m. The ring is 64 m, or the building's radius on prime planets.</summary>
    public static float Flatness(PlanetTerrainData t, float px, float py, float pz, float planetRadius, float ringRadius)
    {
        float ln = MathF.Sqrt(pz * pz + (py * py + px * px));
        float inv = 1f / ln;
        float n0 = inv * px, n1 = inv * py, n2 = inv * pz, n3 = inv * 1f;
        float t0 = n0 * n1 - 0f, t1 = -n2 * n2 - n0 * n0, t2 = 0f - -n2 * n1, t3 = n3 * 1f - n3 * 1f;
        _ = t3;
        float h0 = BaseHeight(t, px, py, pz, planetRadius);
        float tl = MathF.Sqrt(t2 * t2 + (t1 * t1 + t0 * t0));
        float tinv = 1f / tl;
        float T0 = tinv * t0, T1 = tinv * t1, T2 = tinv * t2;
        float a20 = n2 * n2, a2_0 = n2 * n0, a21 = n2 * n1, a11 = n1 * n1, a10 = n1 * n0, a00 = n0 * n0;
        float maxSlope = 0f;
        for (int i = 0; i < 8; i++)
        {
            float th = i * RingStep, s = GameTrig.Sin(th), c = GameTrig.Cos(th), omc = 1f - c;
            float c00 = omc * a00 + c, c01 = n2 * s + omc * a10, c02 = omc * a2_0 - n1 * s;
            float c10 = omc * a10 - n2 * s, c11 = omc * a11 + c, c12 = n0 * s + omc * a21;
            float c20 = n1 * s + omc * a2_0, c21 = omc * a21 - n0 * s, c22 = omc * a20 + c;
            float qx = ((c00 * T0 + c10 * T1) + c20 * T2) * ringRadius + px;
            float qy = ((c01 * T0 + c11 * T1) + c21 * T2) * ringRadius + py;
            float qz = ((c02 * T0 + c12 * T1) + c22 * T2) * ringRadius + pz;
            float d = MathF.Abs(h0 - BaseHeight(t, qx, qy, qz, planetRadius)) * SlopeScale;
            maxSlope = TerrainNoise.Max(d, maxSlope);
        }
        return maxSlope;
    }

    /// <summary>The flatness ring radius: the building's own on prime planets, else the default.</summary>
    internal static float RingRadius(bool prime, float buildingRadius) => prime ? buildingRadius : RingDefault;

    private static int Seed(TerrainNoiseLayer layer, int index, uint hash) => unchecked((int)((uint)layer.SeedOffset + (uint)index + hash));

    private static (float X, float Z) SeedOffsets(int seed) => ((seed % 13) * SeedStep, (seed % 17) * SeedStep);

    /// <summary>The region mask 0x142DE3E70 -> 0x1419BE730: fBm at (p + offsets) / scale, normalised by its range.</summary>
    private static float RegionMask(float x, float y, float z, float scale, int octaves, int seed)
    {
        var (o13, o17) = SeedOffsets(seed);
        float inv = 1f / scale;
        float v = TerrainNoise.Fbm((x + o13) * inv, (y + o13) * inv, (z + o17) * inv, octaves);
        float lo = RegionRange[2 * octaves - 2], hi = RegionRange[2 * octaves - 1];
        return (v - lo) / (hi - lo);
    }

    /// <summary>A gain curve; the game's powf (a double result rounded to float, as the CRT gives it).</summary>
    private static float Gain(float m, float power)
    {
        float t = 0.5f > m ? m : 1f - m;
        float g = (float)Math.Pow(t + t, power) * 0.5f;
        return 0.5f > m ? g : 1f - g;
    }

    /// <summary>One layer's value: the evaluator 0x142C3D4E0 (core 0x142DE3F20 and the region step 0x142C37340).</summary>
    private static float LayerValue(TerrainNoiseLayer l, float x, float y, float z, int seed)
    {
        var (o13, o17) = SeedOffsets(seed);
        float inv = 1f / l.Width;
        float noise = TerrainNoise.Uber((x + o13) * inv, (y + o13) * inv, (z + o17) * inv, l.Octaves, l.Noise);
        float sr = MathF.Sqrt(TerrainNoise.Max(l.RegionRatio, RatioMinimum));
        float c = TerrainNoise.Min(1f, TerrainNoise.Max(0f, noise));
        float v = (c - (1f - sr)) * (1f / sr);
        float m = 1f;
        if (l.RegionScale > 1f && l.RegionRatio > 0f && 1f > l.RegionRatio)
        {
            float srr = MathF.Sqrt(l.RegionRatio);
            float mask = RegionMask(x, y, z, l.RegionScale * l.Width, RegionOctaves, seed);
            float mm = TerrainNoise.Min(1f, TerrainNoise.Max(0f, (mask - (1f - srr)) * (1f / srr)));
            m = Gain(mm, l.RegionGain);
        }
        v *= m;
        if (l.PlateauStratas != 0f)
        {
            float pm = 0f;
            if (l.PlateauRegionSize != 0f) pm = RegionMask(x, y, z, l.PlateauRegionSize, 1, seed + 1);
            float s = l.PlateauStratas, sv = s * v, fl = MathF.Floor(sv);
            float frac = TerrainNoise.Min(1f, TerrainNoise.Max(0f, sv - fl));
            float plateau = (Gain(frac, l.PlateauSharpness) + fl) * (1f / s);
            v = (v - plateau) * pm + plateau;
        }
        v = v * l.Height * QuantiseMul + 0.5f;
        int q = (int)v;
        float fv = q - (q > v ? 1f : 0f);
        return fv * QuantiseStep;
    }

    private static uint BaseHash(ulong seed)
    {
        uint lo = (uint)seed, hi = (uint)(seed >> 32);
        uint ecx = (lo & 0xFFFF) + 0x1B873593;
        uint edx = ((lo >> 5) & 0x7FFF800) ^ (ecx << 16) ^ ecx;
        ecx = edx; edx += hi & 0xFFFF; edx += ecx >> 11;
        uint r8 = ((hi >> 5) & 0x7FFF800) ^ (edx << 16) ^ edx;
        ecx = (r8 >> 11) + r8;
        edx = (ecx << 3) ^ ecx;
        ecx = (edx >> 5) + edx;
        edx = (ecx << 4) ^ ecx;
        ecx = (edx >> 17) + edx;
        uint eax = (ecx << 25) ^ ecx;
        return (eax >> 6) + eax;
    }

    private static float Fraction(uint word) => (float)(word * 2.3283064370807974e-10);
}
