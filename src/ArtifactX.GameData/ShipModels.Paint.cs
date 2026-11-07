using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using libMBIN.NMS.Toolkit;
using Pfim;

namespace ArtifactX.GameData;

/// <summary>A small texture for the 3D preview: 0xAARRGGBB per pixel, row by row.
/// <paramref name="Overlay"/>: see-through where alpha is low (decals such as stripes),
/// drawn over the surface it lies on.</summary>
public sealed record ModelTexture(int Width, int Height, uint[] Pixels, bool Overlay = false)
{
    /// <summary>A decal sheet laid over this texture, read with the second texture coordinates.</summary>
    public ModelTexture? Decal { get; init; }
}

/// <summary>
/// Paints a model's surfaces the way the game does (2026-10-16): a material's
/// procedural texture list is a stack of layers - BASE, then overlays such as
/// PRIMARY, SECONDARY and RUST - each a greyscale image tinted with one palette
/// colour and laid on with its own alpha. A material without a texture list just
/// uses its diffuse image (straps, dark metal). The palette colours come from the
/// caller: a crafted staff keeps its own in the save (SuJ[i].7DH.Aak).
/// </summary>
public sealed partial class ShipModels
{
    private readonly ConcurrentDictionary<string, (int W, int H, byte[] Rgba)?> _images = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Regex Extension = new(@"(\.[^./]*){1,4}$");

    /// <summary>The material painted at <paramref name="size"/> pixels square, or null
    /// when it has nothing to paint with. <paramref name="colour"/> gives a palette
    /// colour (palette name, alt name - "Primary", "Alternative1"...) as RGB 0-1.</summary>
    /// <param name="textureSeed">When set, each layer is painted only if the seed picks it,
    /// with the option it picks (ShipLook's rule: per material a fresh generator; per
    /// layer one draw against its probability, then a weighted pick). Unset: every
    /// layer with its first option (a crafted staff's layers each have just one).</param>
    /// <param name="styles">Option names a ship built at a station was given (its T>1 list,
    /// "STEALTH" for paint style 3 - 2026-10-16): wherever a layer has such an option it is
    /// used, whatever the seed picks. The builder's styles are options found ships never get.</param>
    public ModelTexture? Painted(string material, Func<string, string, float[]?> colour, int size = 512, ulong? textureSeed = null,
        ISet<string>? styles = null)
    {
        if (Decode(material) is not TkMaterialData data) return null;
        var samplers = data.Samplers ?? new();
        TkProceduralTextureList? ListIn(string slot) =>
            samplers.FirstOrDefault(s => (s.Name?.Value ?? "") == slot)?.Map?.Value is { Length: > 0 } map
                ? Decode(Extension.Replace(map.Replace('\\', '/'), "") + ".TEXTURE.MBIN") as TkProceduralTextureList : null;
        // The main paint (gDiffuseMap); a decal material lays a second sheet over it
        // (gDiffuse2Map - the stripes along a tool's top, 2026-10-16).
        if (ListIn("gDiffuseMap") is { } main && Layered(main, colour, size, textureSeed, styles) is { } painted)
        {
            return ListIn("gDiffuse2Map") is { } decal && Layered(decal, colour, size, textureSeed, styles) is { } over
                ? painted with { Decal = over } : painted;
        }
        // No layers: the plain diffuse image.
        var diffuse = samplers.FirstOrDefault(s => (s.Name?.Value ?? "") == "gDiffuseMap")?.Map?.Value;
        return diffuse is { Length: > 0 } && Image(diffuse) is { } img ? Resample(img, size, (r, g, b, _) => (r, g, b)) : null;
    }

    private ModelTexture? Layered(TkProceduralTextureList list, Func<string, string, float[]?> colour, int size, ulong? textureSeed,
        ISet<string>? styles = null)
    {
        var all = (list.Layers ?? Array.Empty<TkProceduralTextureLayer>()).Where(l => (l.Textures?.Count ?? 0) > 0).ToList();
        var picked = textureSeed is ulong seed ? PickLayers(all, seed) : all.Select(l => (l, l.Textures![0])).ToList();
        // A built ship's style is painted on every layer that offers it, picked by the seed
        // or not: Titan Stripes (SEVENTEEN) sets an OVERLAYS layer found ships never get, and
        // without it the hull came out solid orange instead of striped (2026-10-16).
        if (styles is { Count: > 0 })
        {
            var byLayer = picked.ToDictionary(p => p.Item1, p => p.Item2);
            picked = all.Select(l => (l, l.Textures!.FirstOrDefault(t => styles.Contains(t.Name?.Value ?? "")) ?? byLayer.GetValueOrDefault(l)))
                .Where(p => p.Item2 is not null).Select(p => (p.l, p.Item2!)).ToList();
        }
        // The file lists overlays first and BASE last; the game paints BASE first.
        var layers = picked.AsEnumerable().Reverse().ToList();
        if (layers.Count == 0) return null;
        var acc = new float[size * size * 3];
        var cover = new float[size * size];
        // A list with a BASE layer covers its whole surface; one without (decal sheets) only where its images do.
        bool hasBase = layers.Any(l => (l.Item1.Name?.Value ?? "") == "BASE");
        bool any = false;
        foreach (var (layer, texture) in layers)
        {
            if (Image(texture.TextureName?.Value ?? "") is not { } img) continue;
            bool isBase = hasBase && !any;
            var tint = colour(texture.Palette.Palette.ToString(), texture.Palette.ColourAlt.ToString());
            // A Rock layer marked "None" is still tinted with Rock's first colour in game:
            // untinted, Hidden Oboya's front frame came out light grey instead of near black.
            if (tint is null && texture.Palette.Palette == TkPaletteTexture.PaletteEnum.Rock
                && texture.Palette.ColourAlt == TkPaletteTexture.ColourAltEnum.None)
                tint = colour("Rock", "Primary");
            // Some layer images hold two colours (2026-10-16): cyan areas take the layer's
            // own colour, red areas the palette's Unique colour - one coating gives a tool
            // its blue panels and its yellow frame. Grey images just take the one colour.
            var tint2 = tint is null ? null : colour(texture.Palette.Palette.ToString(), "Unique");
            // How bright the image is on average where it shows: a tinted layer is scaled
            // so that average gives the palette colour itself (2026-10-16). Ship hull
            // images are a flat half-bright mask - a fixed "doubled" scale left them
            // near black; multi-tool images average about mid-grey, where both agree.
            float Shade(int k, out float wr, out float wg)
            {
                float r = img.Rgba[k] / 255f, g = img.Rgba[k + 1] / 255f, b = img.Rgba[k + 2] / 255f;
                wr = 1f; wg = 0f;
                if (tint2 is not null && MathF.Max(r, MathF.Max(g, b)) - MathF.Min(r, MathF.Min(g, b)) > 0.25f)
                {
                    float sum = r + MathF.Max(g, b); wg = sum > 0 ? r / sum : 0f; wr = 1f - wg;
                    return MathF.Max(r, MathF.Max(g, b)) * 0.5f;
                }
                return 0.299f * r + 0.587f * g + 0.114f * b;
            }
            float gain = 2f;
            if (tint is not null)
            {
                double sumL = 0, sumA = 0;
                for (int k = 0; k + 3 < img.Rgba.Length; k += 4 * 7)
                {
                    float a = isBase ? 1 : img.Rgba[k + 3] / 255f;
                    sumL += Shade(k, out _, out _) * a; sumA += a;
                }
                if (sumA > 0 && sumL > 0) gain = (float)Math.Clamp(sumA / sumL, 1.0, 4.0);
            }
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int sx = x * img.W / size, sy = y * img.H / size, k = (sy * img.W + sx) * 4, o = (y * size + x) * 3;
                    float r = img.Rgba[k] / 255f, g = img.Rgba[k + 1] / 255f, b = img.Rgba[k + 2] / 255f;
                    float a = isBase ? 1 : img.Rgba[k + 3] / 255f;
                    float lum = Shade(k, out float wr, out float wg);
                    for (int c = 0; c < 3; c++)
                    {
                        // A tinted layer is its grey times the colour, scaled so the image's average gives the colour.
                        float tc = tint is null ? 0 : (tint2 is null ? tint[c] : tint[c] * wr + tint2[c] * wg);
                        float v = tint is null ? (c == 0 ? r : c == 1 ? g : b) : Math.Min(1f, tc * lum * gain);
                        acc[o + c] = acc[o + c] * (1 - a) + v * a;
                    }
                    cover[y * size + x] = cover[y * size + x] * (1 - a) + a;
                }
            any = true;
        }
        if (!any) return null;
        var px = new uint[size * size];
        for (int i = 0; i < px.Length; i++)
            px[i] = (uint)(Math.Clamp(cover[i], 0f, 1f) * 255) << 24
                | (uint)(acc[i * 3] * 255) << 16 | (uint)(acc[i * 3 + 1] * 255) << 8 | (uint)(acc[i * 3 + 2] * 255);
        return new ModelTexture(size, size, px, Overlay: !hasBase);
    }

    /// <summary>The layers a seed paints and the option it picks for each, in file
    /// order - ShipLook.Pick on the game's own types (float maths as the game).</summary>
    private static List<(TkProceduralTextureLayer Layer, TkProceduralTexture Texture)> PickLayers(List<TkProceduralTextureLayer> layers, ulong seed)
    {
        const double Inv = 1.0 / 4294967295.0;
        ulong lo = seed & 0xFFFFFFFF, hi = seed >> 32;
        ulong s0 = lo == 0 ? 1 : lo, s1 = (((lo >> 16) | (lo << 16)) & 0xFFFFFFFF) ^ hi ^ lo;
        ulong Step() { ulong t = s0 * 0x5A76F899UL + s1; s0 = t & 0xFFFFFFFF; s1 = t >> 32; return s0; }
        var rows = new List<(TkProceduralTextureLayer Layer, TkProceduralTexture? Texture, bool Match)>();
        foreach (var layer in layers)
        {
            TkProceduralTexture? chosen = null;
            bool match = false;
            if (layer.Probability > 0)
            {
                float x = (float)(Step() * Inv);
                if (layer.Probability > x && layer.SelectToMatchBase) match = true;
                else if (layer.Probability > x)
                {
                    var options = layer.Textures!.Where(t => (int)t.TextureGameplayUse == 0).ToList();
                    float total = 0f;
                    foreach (var o in options) total += o.Probability;
                    float r = (float)(Step() * Inv) * total, acc = 0f;
                    foreach (var o in options)
                    {
                        acc += o.Probability;
                        if (acc > r) { chosen = o; break; }
                    }
                }
            }
            rows.Add((layer, chosen, match));
        }
        var baseOption = rows.FirstOrDefault(r => (r.Layer.Name?.Value ?? "") == "BASE" && r.Texture is not null).Texture;
        var result = new List<(TkProceduralTextureLayer, TkProceduralTexture)>();
        foreach (var (layer, texture, match) in rows)
        {
            var t = match ? layer.Textures!.FirstOrDefault(o => baseOption is not null && o.Name?.Value == baseOption.Name?.Value) : texture;
            if (t is not null) result.Add((layer, t));
        }
        return result;
    }

    /// <summary>A palette's index in the game's palette table ("Paint" -> 10), or -1.</summary>
    public static int PaletteIndex(string name) => Enum.TryParse<TkPaletteTexture.PaletteEnum>(name, out var p) ? (int)p : -1;

    /// <summary>A colour slot's number ("Primary" 0, "Alternative1" 1... "Unique" 5, "None" 7), or -1.</summary>
    public static int SlotIndex(string name) => Enum.TryParse<TkPaletteTexture.ColourAltEnum>(name, out var a) ? (int)a : -1;

    private static ModelTexture Resample((int W, int H, byte[] Rgba) img, int size, Func<byte, byte, byte, byte, (byte, byte, byte)> map)
    {
        var px = new uint[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int k = ((y * img.H / size) * img.W + x * img.W / size) * 4;
                var (r, g, b) = map(img.Rgba[k], img.Rgba[k + 1], img.Rgba[k + 2], img.Rgba[k + 3]);
                px[y * size + x] = 0xFF000000u | (uint)r << 16 | (uint)g << 8 | b;
            }
        return new ModelTexture(size, size, px);
    }

    /// <summary>A DDS from the game files as RGBA bytes (cached; Pfim decodes BC formats).</summary>
    private (int W, int H, byte[] Rgba)? Image(string path) => _images.GetOrAdd(path.Replace('\\', '/').ToUpperInvariant(), p =>
    {
        if (p.Length == 0 || !_index.TryFind(p, out var pak, out var entry, out var header)) return null;
        try
        {
            var bytes = _extraction.ExtractEntryBytes(pak, entry, header);
            if (bytes is null) return null;
            using IImage img = Pfimage.FromStream(new MemoryStream(bytes));
            int bpp = img.BitsPerPixel / 8;
            if (bpp < 3) return null;
            var rgba = new byte[img.Width * img.Height * 4];
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                {
                    int i = y * img.Stride + x * bpp, o = (y * img.Width + x) * 4;
                    rgba[o] = img.Data[i + 2];
                    rgba[o + 1] = img.Data[i + 1];
                    rgba[o + 2] = img.Data[i];
                    rgba[o + 3] = bpp >= 4 ? img.Data[i + 3] : (byte)255;
                }
            return (img.Width, img.Height, rgba);
        }
        catch { return null; }
    });
}
