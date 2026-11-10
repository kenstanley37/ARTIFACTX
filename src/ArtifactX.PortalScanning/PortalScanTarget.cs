namespace ArtifactX.PortalScanning;

/// <summary>Normalized upright preview bounds, independent of screen size and camera resolution.</summary>
public sealed record PortalScanTarget(double X, double Y, double Width, double Height)
{
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
