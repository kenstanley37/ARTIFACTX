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
    public static async Task<WriteableBitmap?> LoadAsync(string? platformFolder, ulong address, string name)
    {
        byte[]? pixels = await Task.Run(() => ReadPixels(platformFolder, address, name));
        if (pixels is null) return null;

        var bitmap = new WriteableBitmap(Width, Height);
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
