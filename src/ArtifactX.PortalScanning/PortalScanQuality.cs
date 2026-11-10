namespace ArtifactX.PortalScanning;

/// <summary>
/// Conservative review gate for a complete strip. Multiple ambiguous symbols or an isolated
/// poor fit in an otherwise clean row indicate an obscured strip, not a trustworthy address.
/// Tuned against the owner's glare/distance photographs on 2026-11-09; not a probability model.
/// </summary>
public static class PortalScanQuality
{
    public static bool CanReview(IReadOnlyList<GlyphMatch> matches)
    {
        if (matches.Count != 12 || matches.Any(m => !double.IsFinite(m.Distance) || !double.IsFinite(m.Margin) ||
            m.Distance < 0 || m.Margin is < 0 or > 1))
            return false;
        double[] distances = matches.Select(m => m.Distance).Order().ToArray();
        double worst = distances[^1];
        double score = distances.Average() + worst * 0.25;
        double median = (distances[5] + distances[6]) / 2;
        return score <= 0.20 && worst <= 0.38 && matches.Count(m => m.IsCertain) >= 11 &&
            worst <= Math.Max(0.16, median * 2.5);
    }
}
