using Android.Gms.Extensions;
using Android.Graphics;
using Xamarin.Google.MLKit.Vision.Common;
using Xamarin.Google.MLKit.Vision.Text;
using Xamarin.Google.MLKit.Vision.Text.Latin;
using ArtifactX.Maui.Services;

namespace ArtifactX.Maui.Platforms.Android;

/// <summary>Bundled on-device Latin OCR; no network or cloud recognition.</summary>
public sealed class GalaxyTextReader : IGalaxyTextReader
{
    public async Task<IReadOnlyList<string>> ReadLinesAsync(byte[] image, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var bitmap = BitmapFactory.DecodeByteArray(image, 0, image.Length)
            ?? throw new InvalidDataException("Preview text could not be decoded.");
        using var input = InputImage.FromBitmap(bitmap, 0);
        using var recognizer = TextRecognition.GetClient(TextRecognizerOptions.DefaultOptions);
        try
        {
            using var result = await recognizer.Process(input).AsAsync<Text>();
            cancellationToken.ThrowIfCancellationRequested();
            return result.TextBlocks.SelectMany(block => block.Lines).Select(line => line.Text).ToArray();
        }
        finally { recognizer.Close(); }
    }
}
