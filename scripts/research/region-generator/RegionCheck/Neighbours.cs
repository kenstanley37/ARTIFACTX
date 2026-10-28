using ArtifactX.Core.Universe;

/// <summary>"neighbours x y z name": the stars nearest a named system by the ported positions, to compare with
/// the names the game's galaxy map shows around it.</summary>
static class Neighbours
{
    public static void Show(int x, int y, int z, string name, GalaxyGenerationSettings settings, GalaxyPlacementSettings placement)
    {
        var stars = RegionStarPositions.Stars(0, x, y, z, settings, placement);
        if (stars is null) { Console.WriteLine("no positions for this region"); return; }
        ulong low = ((ulong)(uint)y << 24) | ((ulong)(uint)z << 12) | (uint)x;
        string Name(int system) => SystemGenerator.SystemName(((ulong)system << 32) | low, 0);
        int index = stars.ToList().FindIndex(s => Name(s.System) == name);
        if (index < 0) { Console.WriteLine($"{name} not found among {stars.Count}"); return; }
        var target = stars[index];
        Console.WriteLine($"{name} = system {target.System} at ({target.X:F1}, {target.Y:F1}, {target.Z:F1})");
        foreach (var s in stars.Where(s => s.System != target.System)
            .Select(s => (s, d: MathF.Sqrt((s.X - target.X) * (s.X - target.X) + (s.Y - target.Y) * (s.Y - target.Y) + (s.Z - target.Z) * (s.Z - target.Z))))
            .OrderBy(p => p.d).Take(Environment.GetEnvironmentVariable("ALL") is null ? 25 : 9999))
            Console.WriteLine($"{s.d,6:F2}  {s.s.System,5}  {Name(s.s.System)}");
    }
}
