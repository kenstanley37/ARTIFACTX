using SkiaSharp;

namespace ArtifactX.PortalScanning;

/// <summary>Bounded decoding and EXIF orientation for both photo pickers and offline camera checks.</summary>
public static class PortalPhotoDecoder
{
    public const int MaxDimension = 1600;

    public static SKBitmap Decode(Stream stream)
    {
        using var codec = SKCodec.Create(stream) ?? throw new InvalidDataException("This image format could not be read.");
        if ((long)codec.Info.Width * codec.Info.Height > 100_000_000)
            throw new InvalidDataException("Choose a photo smaller than 100 megapixels.");
        float scale = Math.Min(1f, (float)MaxDimension / Math.Max(codec.Info.Width, codec.Info.Height));
        var dimensions = codec.GetScaledDimensions(scale);
        if ((long)dimensions.Width * dimensions.Height > 16_000_000)
            throw new InvalidDataException("This format cannot be downsampled safely. Choose a smaller photo or screenshot.");
        // JPEG decodes at a supported subsampling size before the final resize.
        using var decoded = new SKBitmap(new SKImageInfo(dimensions.Width, dimensions.Height, SKColorType.Rgba8888));
        var result = codec.GetPixels(decoded.Info, decoded.GetPixels());
        if (result != SKCodecResult.Success) throw new InvalidDataException("The photo is incomplete or unreadable.");
        bool swap = codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or
            SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        int width = swap ? decoded.Height : decoded.Width, height = swap ? decoded.Width : decoded.Height;
        float resize = Math.Min(1f, (float)MaxDimension / Math.Max(width, height));
        var oriented = new SKBitmap(Math.Max(1, (int)(width * resize)), Math.Max(1, (int)(height * resize)));
        using var canvas = new SKCanvas(oriented);
        canvas.Scale(resize);
        switch (codec.EncodedOrigin)
        {
            case SKEncodedOrigin.TopRight: canvas.Translate(width, 0); canvas.Scale(-1, 1); break;
            case SKEncodedOrigin.BottomRight: canvas.Translate(width, height); canvas.RotateDegrees(180); break;
            case SKEncodedOrigin.BottomLeft: canvas.Translate(0, height); canvas.Scale(1, -1); break;
            case SKEncodedOrigin.LeftTop: canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
            case SKEncodedOrigin.RightTop: canvas.Translate(width, 0); canvas.RotateDegrees(90); break;
            case SKEncodedOrigin.RightBottom: canvas.Translate(width, height); canvas.RotateDegrees(90); canvas.Scale(-1, 1); break;
            case SKEncodedOrigin.LeftBottom: canvas.Translate(0, height); canvas.RotateDegrees(-90); break;
        }
        using var source = SKImage.FromBitmap(decoded);
        canvas.DrawImage(source, new SKRect(0, 0, decoded.Width, decoded.Height),
            new SKSamplingOptions(SKFilterMode.Linear));
        return oriented;
    }
}
