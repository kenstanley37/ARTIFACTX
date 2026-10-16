using System;
using System.Globalization;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Rough jump counts for the Tools pages' distances, from the hyperdrive range
/// the player types in (HyperdriveRangeBox). The save doesn't store the range
/// - the game works it out from the ship's installed technology - so it's an
/// app setting, not per save. Jumps = distance / range rounded up: real jumps
/// land on whatever star is near the target, so it's an estimate.
/// </summary>
public static class JumpPlanner
{
    /// <summary>Raised (on the caller's thread) when the player changes the range.</summary>
    public static event Action? RangeChanged;

    /// <summary>Hyperdrive range in light years, or null when not set.</summary>
    public static double? Range
    {
        get => AppSettingsService.HyperdriveRange;
        set
        {
            double? clean = value is double v && v >= 1 ? Math.Round(v) : null;
            if (clean == AppSettingsService.HyperdriveRange) return;
            AppSettingsService.SetHyperdriveRange(clean);
            RangeChanged?.Invoke();
        }
    }

    /// <summary>"About 3 jumps", or null when no range is set or the distance is unknown.</summary>
    public static string? Jumps(double? lightYears)
    {
        if (Range is not double range || lightYears is not double ly) return null;
        int jumps = Math.Max(1, (int)Math.Ceiling(ly / range));
        return jumps == 1 ? "1 jump" : $"about {jumps.ToString("N0", CultureInfo.CurrentCulture)} jumps";
    }

    /// <summary>Appends the jump estimate to a distance label, e.g.
    /// "About 1,131 ly from you · 1 jump".</summary>
    public static string WithJumps(string distanceText, double lightYears) =>
        Jumps(lightYears) is string jumps ? $"{distanceText} · {jumps}" : distanceText;
}
