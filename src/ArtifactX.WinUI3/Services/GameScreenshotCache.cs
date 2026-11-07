using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// The pictures the game uploads of a player's space stations and bases, from its cache
/// folder (&lt;save folder&gt;/cache): one raw 640x360 RGBA file per upload, named
/// "&lt;address, 16 hex&gt;.Normal.&lt;name&gt;.&lt;owner&gt;.DDS" - a station's system address, a base's
/// planet address ("0033fa06f8ff7007.Normal.Water World - Toxic", 2026-10-18). Moved out of
/// SpaceStationsPage so the Bases page uses the same reader.
/// </summary>
public static class GameScreenshotCache
{
    public const int Width = 640;
    public const int Height = 360;

    /// <summary>The newest picture for this address (a renamed station or base keeps its
    /// old files too, so an exact-name match wins), or null when the game has none.</summary>
    /// <param name="faded">For a page backdrop: the edges fade to transparent (Fade).</param>
    /// <param name="aspect">Width / height of the area it will fill, so the fade lands on the part shown.</param>
    public static async Task<WriteableBitmap?> LoadAsync(string? platformFolder, ulong address, string name, bool faded = false, double aspect = 0)
    {
        byte[]? pixels = await Task.Run(() => ReadPixels(platformFolder, address, name));
        if (pixels is null) return null;
        if (faded) Fade(pixels, Width, Height, aspect);
        return await ToBitmapAsync(pixels, Width, Height);
    }

    /// <summary>A picture file (the system pictures screen recording keeps), faded for a backdrop.</summary>
    public static async Task<WriteableBitmap?> LoadFadedFileAsync(string path, double aspect = 0)
    {
        try
        {
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
            using var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
            var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
            using var soft = await decoder.GetSoftwareBitmapAsync(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);
            var pixels = new byte[soft.PixelWidth * soft.PixelHeight * 4];
            soft.CopyToBuffer(pixels.AsBuffer());
            Fade(pixels, soft.PixelWidth, soft.PixelHeight, aspect);
            return await ToBitmapAsync(pixels, soft.PixelWidth, soft.PixelHeight);
        }
        catch (Exception) { return null; }
    }

    /// <summary>Fades a picture to transparent at its edges - most at the bottom, a little at the
    /// sides - so a backdrop melts into the page instead of ending in a hard line against the
    /// theme's gradient (2026-10-18). Pixels are BGRA, premultiplied, as WriteableBitmap wants.</summary>
    /// The fade is measured on the part a UniformToFill brush shows in an area of the given
    /// aspect (a wide header crops the top and bottom off), so it ends exactly at the area's edge.
    public static void Fade(byte[] bgra, int w, int h, double aspect = 0)
    {
        double shownH = h, shownW = w;
        if (aspect > 0 && aspect > (double)w / h) shownH = w / aspect; else if (aspect > 0) shownW = h * aspect;
        double y0 = (h - shownH) / 2, x0 = (w - shownW) / 2;
        for (int y = 0; y < h; y++)
        {
            double fy = Math.Clamp((y - y0) / shownH, 0, 1);
            double vertical = fy < 0.35 ? 1 : Math.Max(0, 1 - (fy - 0.35) / 0.65);
            for (int x = 0; x < w; x++)
            {
                double fx = Math.Clamp((x - x0) / shownW, 0, 1);
                double side = Math.Min(1, Math.Min(fx, 1 - fx) / 0.12);
                double a = vertical * vertical * side;
                int i = (y * w + x) * 4;
                bgra[i] = (byte)(bgra[i] * a);
                bgra[i + 1] = (byte)(bgra[i + 1] * a);
                bgra[i + 2] = (byte)(bgra[i + 2] * a);
                bgra[i + 3] = (byte)(255 * a);
            }
        }
    }

    private static async Task<WriteableBitmap> ToBitmapAsync(byte[] pixels, int w, int h)
    {
        var bitmap = new WriteableBitmap(w, h);
        using (var stream = bitmap.PixelBuffer.AsStream())
            await stream.WriteAsync(pixels);
        bitmap.Invalidate();
        return bitmap;
    }

    /// <summary>The game's RGBA pixels, swapped to the BGRA order WriteableBitmap wants.</summary>
    private static byte[]? ReadPixels(string? platformFolder, ulong address, string name)
    {
        try
        {
            if (platformFolder is null) return null;
            string cacheFolder = Path.Combine(platformFolder, "cache");
            if (!Directory.Exists(cacheFolder)) return null;

            var newest = new DirectoryInfo(cacheFolder)
                .GetFiles($"{address:x16}.*.DDS")
                .Where(f => f.Length == Width * Height * 4)
                .OrderByDescending(f => name.Length > 0 && f.Name.Contains($".{name}.", StringComparison.Ordinal))
                .ThenByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();
            if (newest is null) return null;

            byte[] pixels = File.ReadAllBytes(newest.FullName);
            for (int i = 0; i < pixels.Length; i += 4)
                (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
            return pixels;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
