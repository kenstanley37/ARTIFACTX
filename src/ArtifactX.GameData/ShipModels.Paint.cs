using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using libMBIN.NMS.Toolkit;
using Pfim;

namespace ArtifactX.GameData;

/// <summary>A small RGB texture for the 3D preview: 0xRRGGBB per pixel, row by row.</summary>
public sealed record ModelTexture(int Width, int Height, uint[] Pixels);

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
    public ModelTexture? Painted(string material, Func<string, string, float[]?> colour, int size = 512)
    {
        if (Decode(material) is not TkMaterialData data) return null;
        var samplers = data.Samplers ?? new();
        foreach (var sampler in samplers)
        {
            string map = (sampler.Map?.Value ?? "").Replace('\\', '/');
            if (map.Length == 0) continue;
            if (Decode(Extension.Replace(map, "") + ".TEXTURE.MBIN") is TkProceduralTextureList list)
                return Layered(list, colour, size);
        }
        // No layers: the plain diffuse image.
        var diffuse = samplers.FirstOrDefault(s => (s.Name?.Value ?? "") == "gDiffuseMap")?.Map?.Value;
        return diffuse is { Length: > 0 } && Image(diffuse) is { } img ? Resample(img, size, (r, g, b, _) => (r, g, b)) : null;
    }

    private ModelTexture? Layered(TkProceduralTextureList list, Func<string, string, float[]?> colour, int size)
    {
        // The file lists overlays first and BASE last; the game paints BASE first.
        var layers = (list.Layers ?? Array.Empty<TkProceduralTextureLayer>()).Where(l => (l.Textures?.Count ?? 0) > 0).Reverse().ToList();
        if (layers.Count == 0) return null;
        var acc = new float[size * size * 3];
        bool any = false;
        foreach (var layer in layers)
        {
            var texture = layer.Textures![0];
            if (Image(texture.TextureName?.Value ?? "") is not { } img) continue;
            bool isBase = !any;
            var tint = colour(texture.Palette.Palette.ToString(), texture.Palette.ColourAlt.ToString());
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int sx = x * img.W / size, sy = y * img.H / size, k = (sy * img.W + sx) * 4, o = (y * size + x) * 3;
                    float r = img.Rgba[k] / 255f, g = img.Rgba[k + 1] / 255f, b = img.Rgba[k + 2] / 255f;
                    float a = isBase ? 1 : img.Rgba[k + 3] / 255f;
                    float lum = 0.299f * r + 0.587f * g + 0.114f * b;
                    for (int c = 0; c < 3; c++)
                    {
                        // A tinted layer is its grey times the colour (doubled, so mid-grey gives the colour itself).
                        float v = tint is null ? (c == 0 ? r : c == 1 ? g : b) : Math.Min(1f, tint[c] * lum * 2f);
                        acc[o + c] = acc[o + c] * (1 - a) + v * a;
                    }
                }
            any = true;
        }
        if (!any) return null;
        var px = new uint[size * size];
        for (int i = 0; i < px.Length; i++)
            px[i] = (uint)(acc[i * 3] * 255) << 16 | (uint)(acc[i * 3 + 1] * 255) << 8 | (uint)(acc[i * 3 + 2] * 255);
        return new ModelTexture(size, size, px);
    }

    private static ModelTexture Resample((int W, int H, byte[] Rgba) img, int size, Func<byte, byte, byte, byte, (byte, byte, byte)> map)
    {
        var px = new uint[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int k = ((y * img.H / size) * img.W + x * img.W / size) * 4;
                var (r, g, b) = map(img.Rgba[k], img.Rgba[k + 1], img.Rgba[k + 2], img.Rgba[k + 3]);
                px[y * size + x] = (uint)r << 16 | (uint)g << 8 | b;
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
