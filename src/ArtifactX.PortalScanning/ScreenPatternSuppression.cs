namespace ArtifactX.PortalScanning;

/// <summary>Removes isolated monitor-grid specks while retaining disconnected parts of glyphs.</summary>
public static class ScreenPatternSuppression
{
    public static void RemoveSpeckles(byte[] mask, int width, int height)
    {
        if (width <= 0 || height <= 0 || mask.Length != (long)width * height)
            throw new ArgumentException("Mask dimensions do not match.");
        bool[] seen = new bool[mask.Length];
        var pixels = new List<int>();
        for (int i = 0; i < mask.Length; i++)
        {
            if (mask[i] == 0 || seen[i]) continue;
            pixels.Clear(); pixels.Add(i); seen[i] = true;
            for (int next = 0; next < pixels.Count; next++)
            {
                int x = pixels[next] % width, y = pixels[next] / width;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                        int n = ny * width + nx;
                        if (mask[n] == 0 || seen[n]) continue;
                        seen[n] = true; pixels.Add(n);
                    }
            }
            if (pixels.Count < 6)
                foreach (int pixel in pixels) mask[pixel] = 0;
        }
    }
}
