namespace ArtifactX.Core.Procedural;

/// <summary>
/// The game's procedural texture combine (SHADERS/CODE/FULLSCREEN/TEXTUREMULTIPLYFRAGMENT.SHADER.H, compiled as
/// texture_frag_combine_diffuse_0.spv in NMSARC.Shaders.pak; read from its SPIR-V on 2026-11-13, build 185034).
/// Per layer, with the layer image, its average colour and the palette colour all in sRGB:
///   hue        = fract(image hue - average hue + colour hue)
///   saturation = min(image saturation, colour saturation)
///   value      = image value + 0.476627 * 10^(-10 (image value - 0.5)^2) * (colour value - average value)
/// so a dark image stays dark and only its hue and saturation follow the palette. A "Multiply" option is instead
/// linear(image) x linear(colour); an untinted option keeps its image. The base layer is taken as it is; each later
/// layer is mixed over it by its alpha, (a - 0.004) x 1.004016 clamped, in linear light (sRGB to linear: x^2.2, above 1
/// x^2.4), and the result goes back with x^(1/2.2).
/// </summary>
public static class TextureRecolour
{
    /// <summary>Paints one layer image over <paramref name="acc"/> (linear RGB, size x size) and its coverage.</summary>
    public static void Layer((int W, int H, byte[] Rgba) img, int size, float[]? colour, bool multiply, float[] average,
        bool isBase, float[] acc, float[] cover)
    {
        var (ah, _, av) = Hsv(average[0], average[1], average[2]);
        var (ch, cs, cv) = colour is null ? (0f, 0f, 0f) : Hsv(colour[0], colour[1], colour[2]);
        float[]? colourLinear = colour is null ? null : [Linear(colour[0]), Linear(colour[1]), Linear(colour[2])];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int sx = x * img.W / size, sy = y * img.H / size, k = (sy * img.W + sx) * 4, o = (y * size + x) * 3;
                float r = img.Rgba[k] / 255f, g = img.Rgba[k + 1] / 255f, b = img.Rgba[k + 2] / 255f;
                float lr, lg, lb;
                if (colour is null) (lr, lg, lb) = (Linear(r), Linear(g), Linear(b));
                else if (multiply) (lr, lg, lb) = (Linear(r) * colourLinear![0], Linear(g) * colourLinear[1], Linear(b) * colourLinear[2]);
                else
                {
                    var (h, s, v) = Hsv(r, g, b);
                    float weight = MathF.Pow(10f, -10f * (v - 0.5f) * (v - 0.5f)) * 0.476627f;
                    float nh = h - ah + ch; nh -= MathF.Floor(nh);
                    var (nr, ng, nb) = Rgb(nh, MathF.Min(s, cs), v + weight * (cv - av));
                    (lr, lg, lb) = (Linear(nr), Linear(ng), Linear(nb));
                }
                float a = isBase ? 1f : Math.Clamp(img.Rgba[k + 3] / 255f - 0.004f, 0f, 1f) * 1.004016f;
                acc[o] = acc[o] * (1 - a) + lr * a;
                acc[o + 1] = acc[o + 1] * (1 - a) + lg * a;
                acc[o + 2] = acc[o + 2] * (1 - a) + lb * a;
                cover[y * size + x] = cover[y * size + x] * (1 - a) + a;
            }
    }

    /// <summary>Linear RGB back to sRGB-ish values 0-1, in place (the shader's x^(1/2.2)).</summary>
    public static void ToSrgb(float[] acc)
    {
        for (int i = 0; i < acc.Length; i++) acc[i] = Math.Clamp(MathF.Pow(MathF.Max(acc[i], 0f), 1f / 2.2f), 0f, 1f);
    }

    /// <summary>The shader's sRGB to linear: x^2.2, x^2.4 above 1.</summary>
    public static float Linear(float x) => x <= 0 ? 0 : MathF.Pow(x, x > 1 ? 2.4f : 2.2f);

    /// <summary>RGB to hue (0-1), saturation and value as the shader computes them.</summary>
    public static (float H, float S, float V) Hsv(float r, float g, float b)
    {
        // The usual branch-free form: K = (0, -1/3, 2/3, -1).
        (float x, float y, float z, float w) p = g < b ? (b, g, -1f, 2f / 3f) : (g, b, 0f, -1f / 3f);
        (float x, float y, float z, float w) q = r < p.x ? (p.x, p.y, p.w, r) : (r, p.y, p.z, p.x);
        float d = q.x - MathF.Min(q.w, q.y);
        float h = d == 0 ? 0 : MathF.Abs(q.z + (q.w - q.y) / (6f * d));
        float s = q.x == 0 ? 0 : d / q.x;
        return (h, s, q.x);
    }

    /// <summary>Hue, saturation and value back to RGB, clamped to 0-1 (the shader's form).</summary>
    public static (float R, float G, float B) Rgb(float h, float s, float v)
    {
        float C(float offset)
        {
            float f = h + offset; f -= MathF.Floor(f);
            float c = Math.Clamp(MathF.Abs(f * 6f - 3f) - 1f, 0f, 1f);
            return Math.Clamp((1f + (c - 1f) * s) * v, 0f, 1f);
        }
        return (C(1f), C(2f / 3f), C(1f / 3f));
    }
}
