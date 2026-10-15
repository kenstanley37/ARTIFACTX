namespace ArtifactX.Core.Procedural;

// Ported to C# from NMS Shipwright's nms_procgen/textures.py (MIT, (c) 2026
// Shikhar Tiwari): https://github.com/S-T-0-7/NMS-Shipwright - see
// THIRD-PARTY-NOTICES.md. Their notes: the game's palette-table generator
// fills each base palette's colours from the ship's seed; "verified vs
// nms.center colors for 5 fighter seeds". The maths must match the game bit
// for bit - float32 comparisons included.

/// <summary>One of the game's base colour palettes: 64 colours and how many of them it uses.</summary>
public sealed record ColourPalette(float[][] Colours, string NumColours);

/// <summary>A ship's paint as its seed sets it (before any recolouring the player did in game).</summary>
public sealed record ShipPaint(float[] Primary, float[] Secondary, float[] Decal1, float[] Decal2, float[] Undercoat);

public static class ShipColours
{
    /// <summary>The "Paint" and "Undercoat" palettes (GcPaletteList order).</summary>
    public const int PaintPalette = 10, UndercoatPalette = 20;

    private const ulong M32 = 0xFFFFFFFF;

    /// <summary>The paint colours a ship's seed gives. <paramref name="palettes"/>
    /// are the game's 66 base palettes in file order.</summary>
    public static ShipPaint Generate(ulong seed, IReadOnlyList<ColourPalette> palettes)
    {
        // Every palette before Paint takes 5 colours x 2 random steps (re-rolls
        // take none), so the stream is skipped ahead rather than generating them.
        ulong lo = seed & M32, hi = seed >> 32;
        ulong s0 = lo == 0 ? 1 : lo;
        ulong s1 = (((lo >> 16) | (lo << 16)) & M32) ^ hi ^ lo;
        Skip(ref s0, ref s1, PaintPalette * 10);
        var paint = Pick(palettes[PaintPalette], ref s0, ref s1, 5);
        Skip(ref s0, ref s1, (UndercoatPalette - PaintPalette - 1) * 10);
        var under = Pick(palettes[UndercoatPalette], ref s0, ref s1, 1);
        return new ShipPaint(paint[0], paint[1], paint[2], paint[3], under[0]);
    }

    private static ulong Step(ref ulong s0, ref ulong s1)
    {
        ulong t = s0 * 0x5A76F899UL + s1;
        s0 = t & M32;
        s1 = t >> 32;
        return s0;
    }

    private static void Skip(ref ulong s0, ref ulong s1, int steps)
    {
        for (int i = 0; i < steps; i++) Step(ref s0, ref s1);
    }

    /// <summary>n colours from one palette: two 3-bit picks each (row/column of
    /// the 8x8 grid), narrowed by how many colours the palette uses; a colour
    /// repeating an earlier pick moves on to the next one.</summary>
    private static List<float[]> Pick(ColourPalette palette, ref ulong s0, ref ulong s1, int n)
    {
        var picked = new List<float[]>();
        for (int k = 0; k < n; k++)
        {
            int a = (int)(Step(ref s0, ref s1) >> 29), b = (int)(Step(ref s0, ref s1) >> 29);
            int i = palette.NumColours switch
            {
                "_1" => 0,
                "_4" => ((b >> 2) + (a >> 2) * 8) * 4,
                "_8" => b,
                "_16" => ((b >> 1) + (a >> 1) * 8) * 2,
                _ => b + a * 8,
            };
            float[] c = palette.Colours[Normalise(palette.NumColours, i)];
            for (int tries = 0; tries < 64; tries++)
            {
                if (!picked.Any(d => Close(c, d))) break;
                i = (i + 1) % 64;
                c = palette.Colours[Normalise(palette.NumColours, i)];
            }
            picked.Add(c);
        }
        return picked;
    }

    private static int Normalise(string mode, int i) => mode switch
    {
        "_1" => 0,
        "_4" => ((i % 8) / 4 + (i / 32) * 8) * 4,
        "_8" => i % 8,
        "_16" => ((i % 8) / 2 + (i / 16) * 8) * 2,
        _ => i,
    };

    /// <summary>The game's "same colour" test, in float32.</summary>
    private static bool Close(float[] c, float[] d)
    {
        float r = c[0] - d[0], g = c[1] - d[1], b = c[2] - d[2];
        float sum = r * r;
        sum += g * g;
        sum += b * b;
        return sum < 2.3283064365386963e-10f;
    }
}
