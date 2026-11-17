using ArtifactX.Core.Procedural;

/// <summary>ShipLook.PickMerged - one pick for a whole creature model (the real 42/42 check against the running game is
/// in its doc comment). Here what the merge guarantees on any lists: a layer gets one option name for every list, the
/// name is one some list offers, and the same seed always gives the same picks.</summary>
internal static class CreatureTextureChecks
{
    public static void Check()
    {
        static TextureOption O(string name) => new(name, 0, 0, 1f, 0);
        static TextureLayer L(string name, params string[] options) => new(name, "", 1f, false, options.Select(O).ToList());
        // Body, tail and ear: the body and tail share three patterns, the ear offers two of its own.
        IReadOnlyList<TextureLayer>[] lists =
        [
            [L("MARKINGS", "STRIPES", "HALF", "SCALES"), L("UNDERBELLY", ""), L("BASE", "FUR")],
            [L("MARKINGS", "STRIPES", "HALF", "SCALES"), L("BASE", "FUR")],
            [L("MARKINGS", "HALF", "GRADIENT"), L("BASE", "FUR")],
        ];
        var offered = new HashSet<string> { "STRIPES", "HALF", "SCALES", "GRADIENT" };
        var seen = new HashSet<string>();
        for (ulong seed = 1; seed <= 300; seed++)
        {
            ulong s = seed * 0x9E3779B97F4A7C15UL;
            var picks = ShipLook.PickMerged(lists, s);
            if (picks.GetValueOrDefault("MARKINGS") is not { } mark || !offered.Contains(mark))
                throw new InvalidOperationException($"seed {seed}: MARKINGS pick {picks.GetValueOrDefault("MARKINGS")} isn't an offered pattern");
            if (picks.GetValueOrDefault("BASE") != "FUR") throw new InvalidOperationException($"seed {seed}: BASE should be FUR");
            if (!ShipLook.PickMerged(lists, s).SequenceEqual(picks)) throw new InvalidOperationException($"seed {seed}: picks not repeatable");
            seen.Add(mark);
        }
        if (seen.Count != offered.Count) throw new InvalidOperationException($"only {string.Join(",", seen)} ever picked");
        Console.WriteLine($"Creature textures: one MARKINGS pick per model, all {seen.Count} patterns reachable OK");
    }
}
