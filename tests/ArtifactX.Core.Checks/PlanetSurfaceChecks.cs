using ArtifactX.Core.Universe;

/// <summary>
/// PlanetTerrain.CombineLayers (the voxel density pass's noise-layer combination, 0x1412B05D0 in build 184271) against
/// the game's own code. Vectors from the owner's Arkans (505200FF67B291, Steam slot 8) on build 185034, 2026-11-10: the
/// live Far region was regenerated in an emulator (scripts/research/region-generator: read_far_region_live.py,
/// emu_far.py, caves removed) with the planet's real layers and with single settings patched; each node gives layers
/// 0-4's columns, the base height and the game's surface above the base. The voxel grid quantises to about 1 cm.
/// </summary>
internal static class PlanetSurfaceChecks
{
    private sealed record Node(float[] Columns, float Base, float Expected);
    private sealed record Case(string Name, TerrainNoiseLayer[] Layers, float NoSea, float Sea, float WaterFade, Node[] Nodes);

    private static Node N(float[] columns, float baseHeight, float expected) => new(columns, baseHeight, expected);

    private static TerrainNoiseLayer L(float height, float heightOffset, int maximumLod, int offset, float smoothRadius,
        int waterFade, bool active, bool subtract) =>
        new(default, 0, 0, height, heightOffset, maximumLod, offset, 0f, 0, 0f, 1f, 0f, 0f, 0, smoothRadius, 0f, 0,
            waterFade, 0f, active, subtract);

    private static readonly Case[] Cases =
    [
        new("planet 5", [L(32.0f, 0.0f, 4, 1, 0.0f, 0, true, false), L(8.0f, 0.0f, 4, 1, 0.0f, 0, true, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false)], 20.0f, 0.0f, 100.0f,
            [N([11.978515625f, 1.5927734375f, 0.0f, 0.0f, 0.0f], 247.1220703125f, 33.56287384033203f), N([14.693359375f, 0.3916015625f, 0.0f, 0.0f, 0.0f], 256.7001953125f, 35.07693099975586f), N([15.4580078125f, 0.0771484375f, 0.0f, 0.0f, 0.0f], 261.2548828125f, 35.52625274658203f), N([13.1748046875f, 0.0f, 0.0f, 0.0f, 0.0f], 261.0048828125f, 33.17216110229492f)]),
        new("layer 1 Zero", [L(32.0f, 0.0f, 4, 1, 0.0f, 0, true, false), L(8.0f, 0.0f, 4, 0, 0.0f, 0, true, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false)], 20.0f, 0.0f, 100.0f,
            [N([11.978515625f, 1.5927734375f, 0.0f, 0.0f, 0.0f], 247.1220703125f, 31.97071075439453f), N([14.693359375f, 0.3916015625f, 0.0f, 0.0f, 0.0f], 256.7001953125f, 34.68620300292969f), N([15.4580078125f, 0.0771484375f, 0.0f, 0.0f, 0.0f], 261.2548828125f, 35.45366287231445f), N([13.1748046875f, 0.0f, 0.0f, 0.0f, 0.0f], 261.0048828125f, 33.17216110229492f)]),
        new("layer 1 All", [L(32.0f, 0.0f, 4, 1, 0.0f, 0, true, false), L(8.0f, 0.0f, 4, 2, 0.0f, 0, true, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false)], 20.0f, 0.0f, 100.0f,
            [N([11.978515625f, 1.5927734375f, 0.0f, 0.0f, 0.0f], 247.1220703125f, 33.56287384033203f), N([14.693359375f, 0.3916015625f, 0.0f, 0.0f, 0.0f], 256.7001953125f, 35.07693099975586f), N([15.4580078125f, 0.0771484375f, 0.0f, 0.0f, 0.0f], 261.2548828125f, 35.52625274658203f), N([13.1748046875f, 0.0f, 0.0f, 0.0f, 0.0f], 261.0048828125f, 33.17216110229492f)]),
        new("layer 1 SeaLevel", [L(32.0f, 0.0f, 4, 1, 0.0f, 0, true, false), L(8.0f, 0.0f, 4, 3, 0.0f, 0, true, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false)], 20.0f, 0.0f, 100.0f,
            [N([11.978515625f, 1.5927734375f, 0.0f, 0.0f, 0.0f], 247.1220703125f, 31.97071075439453f), N([14.693359375f, 0.3916015625f, 0.0f, 0.0f, 0.0f], 256.7001953125f, 34.68620300292969f), N([15.4580078125f, 0.0771484375f, 0.0f, 0.0f, 0.0f], 261.2548828125f, 35.45366287231445f), N([13.1748046875f, 0.0f, 0.0f, 0.0f, 0.0f], 261.0048828125f, 33.17216110229492f)]),
        new("layer 1 Subtract", [L(32.0f, 0.0f, 4, 1, 0.0f, 0, true, false), L(8.0f, 0.0f, 4, 1, 0.0f, 0, true, true), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false)], 20.0f, 0.0f, 100.0f,
            [N([11.978515625f, 1.5927734375f, 0.0f, 0.0f, 0.0f], 247.1220703125f, 30.378509521484375f), N([14.693359375f, 0.3916015625f, 0.0f, 0.0f, 0.0f], 256.7001953125f, 34.29549026489258f), N([15.4580078125f, 0.0771484375f, 0.0f, 0.0f, 0.0f], 261.2548828125f, 35.379737854003906f), N([13.1748046875f, 0.0f, 0.0f, 0.0f, 0.0f], 261.0048828125f, 33.17216110229492f)]),
        new("layer 1 smooth 5", [L(32.0f, 0.0f, 4, 1, 0.0f, 0, true, false), L(8.0f, 0.0f, 4, 1, 5.0f, 0, true, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false)], 20.0f, 0.0f, 100.0f,
            [N([11.978515625f, 1.5927734375f, 0.0f, 0.0f, 0.0f], 247.1220703125f, 34.148948669433594f), N([14.693359375f, 0.3916015625f, 0.0f, 0.0f, 0.0f], 256.7001953125f, 36.141632080078125f), N([15.4580078125f, 0.0771484375f, 0.0f, 0.0f, 0.0f], 261.2548828125f, 36.74144744873047f), N([13.1748046875f, 0.0f, 0.0f, 0.0f, 0.0f], 261.0048828125f, 34.4224739074707f)]),
        new("layer 1 offset 3", [L(32.0f, 0.0f, 4, 1, 0.0f, 0, true, false), L(8.0f, 3.0f, 4, 1, 0.0f, 0, true, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false)], 20.0f, 0.0f, 100.0f,
            [N([11.978515625f, 1.5927734375f, 0.0f, 0.0f, 0.0f], 247.1220703125f, 36.56584167480469f), N([14.693359375f, 0.3916015625f, 0.0f, 0.0f, 0.0f], 256.7001953125f, 38.07805252075195f), N([15.4580078125f, 0.0771484375f, 0.0f, 0.0f, 0.0f], 261.2548828125f, 38.53479766845703f), N([13.1748046875f, 0.0f, 0.0f, 0.0f, 0.0f], 261.0048828125f, 36.17093276977539f)]),
        new("layer 1 water fade above", [L(32.0f, 0.0f, 4, 1, 0.0f, 0, true, false), L(8.0f, 0.0f, 4, 1, 0.0f, 1, true, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false)], 20.0f, 0.0f, 100.0f,
            [N([11.978515625f, 1.5927734375f, 0.0f, 0.0f, 0.0f], 247.1220703125f, 33.56287384033203f), N([14.693359375f, 0.3916015625f, 0.0f, 0.0f, 0.0f], 256.7001953125f, 35.07693099975586f), N([15.4580078125f, 0.0771484375f, 0.0f, 0.0f, 0.0f], 261.2548828125f, 35.52625274658203f), N([13.1748046875f, 0.0f, 0.0f, 0.0f, 0.0f], 261.0048828125f, 33.17216110229492f)]),
        new("layer 1 water fade below", [L(32.0f, 0.0f, 4, 1, 0.0f, 0, true, false), L(8.0f, 0.0f, 4, 1, 0.0f, 2, true, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false)], 20.0f, 0.0f, 100.0f,
            [N([11.978515625f, 1.5927734375f, 0.0f, 0.0f, 0.0f], 247.1220703125f, 31.97071075439453f), N([14.693359375f, 0.3916015625f, 0.0f, 0.0f, 0.0f], 256.7001953125f, 34.68620300292969f), N([15.4580078125f, 0.0771484375f, 0.0f, 0.0f, 0.0f], 261.2548828125f, 35.45366287231445f), N([13.1748046875f, 0.0f, 0.0f, 0.0f, 0.0f], 261.0048828125f, 33.17216110229492f)]),
        new("layer 0 off", [L(32.0f, 0.0f, 4, 1, 0.0f, 0, false, false), L(8.0f, 0.0f, 4, 1, 0.0f, 0, true, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false)], 20.0f, 0.0f, 100.0f,
            [N([0.0f, 1.5927734375f, 0.0f, 0.0f, 0.0f], 247.1220703125f, 21.587312698364258f), N([0.0f, 0.3916015625f, 0.0f, 0.0f, 0.0f], 256.7001953125f, 20.385826110839844f), N([0.0f, 0.0771484375f, 0.0f, 0.0f, 0.0f], 261.2548828125f, 20.07325553894043f), N([0.0f, 0.0f, 0.0f, 0.0f, 0.0f], 261.0048828125f, 19.9951114654541f)]),
        new("layer 2 All 200", [L(32.0f, 0.0f, 4, 1, 0.0f, 0, true, false), L(8.0f, 0.0f, 4, 1, 0.0f, 0, true, false), L(10.0f, 0.0f, 4, 2, 0.0f, 0, true, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false), L(10.0f, 0.0f, 4, 1, 0.0f, 0, false, false)], 20.0f, 0.0f, 100.0f,
            [N([11.978515625f, 1.5927734375f, 10.0f, 0.0f, 0.0f], 247.1220703125f, 43.56532669067383f), N([14.693359375f, 0.3916015625f, 10.0f, 0.0f, 0.0f], 256.7001953125f, 45.07936096191406f), N([15.4580078125f, 0.0771484375f, 10.0f, 0.0f, 0.0f], 261.2548828125f, 45.52870559692383f), N([13.1748046875f, 0.0f, 10.0f, 0.0f, 0.0f], 261.0048828125f, 43.170738220214844f)]),
    ];

    public static void Check()
    {
        int nodes = 0;
        foreach (var c in Cases)
            foreach (var n in c.Nodes)
            {
                float got = PlanetTerrain.CombineLayers(c.Layers, n.Columns, n.Base, c.NoSea, c.Sea, c.WaterFade, 0);
                if (MathF.Abs(got - n.Expected) > 0.02f)
                    throw new InvalidOperationException($"Planet surface '{c.Name}': expected {n.Expected}, got {got}");
                nodes++;
            }
        Console.WriteLine($"Planet surface: {Cases.Length} layer cases, {nodes} nodes within 2 cm of the game's Far pass OK");
    }
}