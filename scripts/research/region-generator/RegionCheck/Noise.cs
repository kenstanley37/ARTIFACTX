using ArtifactX.Core.Universe;
using System.Text.Json;

/// <summary>Compares the ported noise routines with emulator vectors (noisevectors.py).</summary>
static class Noise
{
    public static void Check(string json)
    {
        int rows = 0, ok2 = 0, ok3 = 0, okd = 0;
        foreach (var r in JsonDocument.Parse(File.ReadAllText(json)).RootElement.EnumerateArray())
        {
            float[] v = r.EnumerateArray().Select(e => e.GetSingle()).ToArray();
            float n2 = GalaxySimplexNoise.Noise2(v[0], v[1]);
            float n3 = GalaxySimplexNoise.Noise(v[0], v[1], v[2], out float dx, out float dy, out float dz);
            if (n2 == v[3]) ok2++; else if (ok2 + 5 > rows) Console.WriteLine($"2D diff at ({v[0]}, {v[1]}): {n2} vs {v[3]}");
            if (n3 == v[4]) ok3++; else if (ok3 + 5 > rows) Console.WriteLine($"3D value diff at ({v[0]}, {v[1]}, {v[2]}): {n3} vs {v[4]}");
            if (n3 == v[5] && dx == v[6] && dy == v[7] && dz == v[8]) okd++;
            rows++;
        }
        Console.WriteLine($"noise vectors {rows}: 2D value {ok2} exact, 3D value (0x142D74E60) {ok3} exact, 3D with derivatives {okd} exact");
    }
}
