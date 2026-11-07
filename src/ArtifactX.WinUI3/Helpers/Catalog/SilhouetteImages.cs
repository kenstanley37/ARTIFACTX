using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace ArtifactX.WinUI3.Helpers.Catalog;

/// <summary>
/// The dark outline the game's Catalog shows for an item not known yet: the icon's
/// shape (its alpha) filled with one translucent grey, so it reads on both themes'
/// dark backgrounds without giving the picture away. Built on the UI thread.
/// </summary>
public static class SilhouetteImages
{
    private const byte Grey = 0x7A;
    private const double Strength = 0.55;

    public static async Task<WriteableBitmap?> FromPngAsync(byte[] png)
    {
        try
        {
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(png.AsBuffer());
            stream.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var pixels = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                new BitmapTransform(), ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
            byte[] data = pixels.DetachPixelData();
            for (int i = 0; i < data.Length; i += 4)
            {
                byte alpha = (byte)(data[i + 3] * Strength);
                byte colour = (byte)(Grey * alpha / 255); // premultiplied
                data[i] = data[i + 1] = data[i + 2] = colour;
                data[i + 3] = alpha;
            }
            var bitmap = new WriteableBitmap((int)decoder.PixelWidth, (int)decoder.PixelHeight);
            using (var output = bitmap.PixelBuffer.AsStream()) await output.WriteAsync(data);
            bitmap.Invalidate();
            return bitmap;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or System.Runtime.InteropServices.COMException)
        {
            return null; // the tile falls back to the dimmed picture
        }
    }
}
