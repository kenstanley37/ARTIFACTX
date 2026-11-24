using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// A picture the player picks to sit behind a page's top section (owner, 2026-11-24: their own screenshot of the
/// system view, as Current System used to show). One picture per page, not per system: the chosen file is copied
/// to %AppData%\ArtifactX\backgrounds\&lt;page&gt;.jpg, scaled to at most <see cref="MaxWidth"/> pixels wide, so it
/// stays when the screenshot is deleted and picking again replaces it.
/// </summary>
public static class PageBackgroundService
{
    private const uint MaxWidth = 1920;

    /// <summary>Raised (any thread) with the page key when its picture was set or removed.</summary>
    public static event Action<string>? Changed;

    private static string PathFor(string page) => Path.Combine(LocalAppDataPaths.GetSubfolder("backgrounds"), page + ".jpg");

    public static bool Has(string page) => File.Exists(PathFor(page));

    /// <summary>Copies a picture in as the page's background. Returns an error to show, or null.</summary>
    public static async Task<string?> SetAsync(string page, string sourceFile)
    {
        string target = PathFor(page), temp = target + ".tmp";
        try
        {
            using (var input = File.OpenRead(sourceFile))
            using (var output = new FileStream(temp, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                var decoder = await BitmapDecoder.CreateAsync(input.AsRandomAccessStream());
                var transform = new BitmapTransform { InterpolationMode = BitmapInterpolationMode.Fant };
                if (decoder.OrientedPixelWidth > MaxWidth)
                {
                    transform.ScaledWidth = MaxWidth;
                    transform.ScaledHeight = (uint)Math.Round(decoder.OrientedPixelHeight * (double)MaxWidth / decoder.OrientedPixelWidth);
                }
                // Any picture type in, always a JPEG out (a PNG screenshot would be several times the size).
                using var pixels = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, transform,
                    ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);
                var options = new BitmapPropertySet { ["ImageQuality"] = new BitmapTypedValue(0.88, Windows.Foundation.PropertyType.Single) };
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, output.AsRandomAccessStream(), options);
                encoder.SetSoftwareBitmap(pixels);
                await encoder.FlushAsync();
            }
            File.Move(temp, target, overwrite: true);
        }
        catch (Exception ex)
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { }
            return $"That picture couldn't be used: {ex.Message}";
        }
        Changed?.Invoke(page);
        return null;
    }

    public static void Remove(string page)
    {
        try { File.Delete(PathFor(page)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return; }
        Changed?.Invoke(page);
    }

    /// <summary>The page's picture with its edges faded into the card (GameScreenshotCache.Fade), or null when none
    /// is set. Call on the UI thread: decoding runs off it.</summary>
    public static async Task<WriteableBitmap?> LoadAsync(string page)
    {
        string path = PathFor(page);
        var decoded = await Task.Run<(byte[] Pixels, int Width, int Height)?>(async () =>
        {
            try
            {
                if (!File.Exists(path)) return null;
                using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var decoder = await BitmapDecoder.CreateAsync(file.AsRandomAccessStream());
                using var soft = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                var pixels = new byte[soft.PixelWidth * soft.PixelHeight * 4];
                soft.CopyToBuffer(pixels.AsBuffer());
                GameScreenshotCache.Fade(pixels, soft.PixelWidth, soft.PixelHeight);
                return (pixels, soft.PixelWidth, soft.PixelHeight);
            }
            catch (Exception) { return null; }
        });
        if (decoded is not { } d) return null;
        var bitmap = new WriteableBitmap(d.Width, d.Height);
        using (var stream = bitmap.PixelBuffer.AsStream())
            await stream.WriteAsync(d.Pixels);
        bitmap.Invalidate();
        return bitmap;
    }
}
