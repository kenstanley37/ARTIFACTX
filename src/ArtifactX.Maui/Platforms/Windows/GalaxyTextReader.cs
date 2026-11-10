using ArtifactX.Maui.Services;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Globalization;

namespace ArtifactX.Maui.Platforms.Windows;

public sealed class GalaxyTextReader : IGalaxyTextReader
{
    public async Task<IReadOnlyList<string>> ReadLinesAsync(byte[] image, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(image);
        using var random = stream.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(random);
        using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var engine = OcrEngine.TryCreateFromLanguage(new Language("en-US"))
            ?? throw new InvalidOperationException("English text recognition is not installed on this device.");
        var result = await engine.RecognizeAsync(bitmap);
        cancellationToken.ThrowIfCancellationRequested();
        return result.Lines.Select(line => line.Text).ToArray();
    }
}
