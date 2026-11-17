using ArtifactX.Core.Procedural;

/// <summary>TextureRecolour - the game's texture combine shader (texture_frag_combine_diffuse). Checks what its formula
/// guarantees: HSV round-trips, a layer recoloured to its own average colour is unchanged, a grey image stays grey
/// (saturation is capped by the image's own), and a dark image stays dark under a bright colour.</summary>
internal static class TextureRecolourChecks
{
    public static void Check()
    {
        foreach (var (r, g, b) in new[] { (0.2f, 0.4f, 0.6f), (0.9f, 0.1f, 0.3f), (0.5f, 0.5f, 0.5f), (0f, 0f, 0f), (1f, 0.8f, 0.1f) })
        {
            var (h, s, v) = TextureRecolour.Hsv(r, g, b);
            var (r2, g2, b2) = TextureRecolour.Rgb(h, s, v);
            if (MathF.Abs(r - r2) > 1e-4f || MathF.Abs(g - g2) > 1e-4f || MathF.Abs(b - b2) > 1e-4f)
                throw new InvalidOperationException($"HSV round trip of {r},{g},{b} gave {r2},{g2},{b2}");
        }
        float[] One(byte r, byte g, byte b, float[] colour, float[] average)
        {
            var acc = new float[3]; var cover = new float[1];
            TextureRecolour.Layer((1, 1, [r, g, b, 255]), 1, colour, false, average, true, acc, cover);
            TextureRecolour.ToSrgb(acc);
            return acc;
        }
        // Recoloured to its own average: unchanged (up to the gamma round trip).
        var same = One(51, 102, 153, [0.2f, 0.4f, 0.6f], [0.2f, 0.4f, 0.6f]);
        if (MathF.Abs(same[0] - 0.2f) > 0.01f || MathF.Abs(same[2] - 0.6f) > 0.01f)
            throw new InvalidOperationException($"recolour to its own average changed it: {same[0]},{same[1]},{same[2]}");
        var grey = One(128, 128, 128, [1f, 0.8f, 0.1f], [0.5f, 0.5f, 0.5f]);
        if (MathF.Abs(grey[0] - grey[2]) > 0.01f) throw new InvalidOperationException("a grey image picked up colour");
        var dark = One(30, 50, 80, [1f, 0.9f, 0.2f], [0.2f, 0.3f, 0.45f]);
        if (dark.Max() > 0.45f) throw new InvalidOperationException($"a dark image came out bright: {dark.Max()}");
        if (!(dark[0] > dark[2])) throw new InvalidOperationException("a blue image recoloured yellow kept its blue hue");
        Console.WriteLine("Texture recolour: HSV round trip, own average unchanged, grey stays grey, dark stays dark OK");
    }
}
