using ArtifactX.Core.Universe;

/// <summary>"spirallist file": for lines "galaxy x y z ..." (hex x/y/z), prints each region's name and whether it is a
/// spiral region (the port returns null without the spiral settings but places it with them).</summary>
static class SpiralList
{
    public static void Show(string file, GalaxyGenerationSettings settings, GalaxyPlacementSettings placement)
    {
        foreach (string line in File.ReadLines(file))
        {
            var f = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (f.Length < 4) continue;
            int g = int.Parse(f[0]), x = Convert.ToInt32(f[1], 16), y = Convert.ToInt32(f[2], 16), z = Convert.ToInt32(f[3], 16);
            bool spiral = RegionStarPositions.Stars(g, x, y, z, settings, placement with { SpiralTwistMult = float.NaN }) is null
                && RegionStarPositions.Stars(g, x, y, z, settings, placement) is not null;
            ulong code = ((ulong)(uint)y << 24) | ((ulong)(uint)z << 12) | (uint)x;
            Console.WriteLine($"{(spiral ? "SPIRAL" : "      ")}  {SystemGenerator.RegionName(code, g)} (galaxy {g + 1})");
        }
    }
}
