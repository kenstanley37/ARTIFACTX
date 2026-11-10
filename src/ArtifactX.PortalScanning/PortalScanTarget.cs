namespace ArtifactX.PortalScanning;

/// <summary>Normalized upright preview bounds, independent of screen size and camera resolution.</summary>
public sealed record PortalScanTarget(double X, double Y, double Width, double Height)
{
    /// <summary>
    /// The on-screen guide box players line the strip and galaxy name up in. The overlay draws it and the
    /// live analyzer searches this band before the full frame (Pixel 7 Pro Release, 2026-11-10: a full-frame
    /// search cost ~1.1-1.3 s per frame).
    /// </summary>
    public static PortalScanTarget Guide { get; } = new(0.06, 0.36, 0.88, 0.24);

    /// <summary>The guide box in image pixels, widened by <paramref name="margin"/> (a fraction of the image) for hand movement.</summary>
    public ScanRegion ToRegion(int imageWidth, int imageHeight, double margin = 0) => new(
        Math.Clamp((int)((X - margin) * imageWidth), 0, imageWidth),
        Math.Clamp((int)((Y - margin) * imageHeight), 0, imageHeight),
        Math.Clamp((int)Math.Ceiling((X + Width + margin) * imageWidth), 0, imageWidth),
        Math.Clamp((int)Math.Ceiling((Y + Height + margin) * imageHeight), 0, imageHeight));

    public static PortalScanTarget? FromRegion(ScanRegion region, int imageWidth, int imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0) return null;
        int left = Math.Clamp(region.Left, 0, imageWidth), right = Math.Clamp(region.Right, 0, imageWidth);
        int top = Math.Clamp(region.Top, 0, imageHeight), bottom = Math.Clamp(region.Bottom, 0, imageHeight);
        if (right <= left || bottom <= top) return null;
        return new(left / (double)imageWidth, top / (double)imageHeight,
            (right - left) / (double)imageWidth, (bottom - top) / (double)imageHeight);
    }
}
