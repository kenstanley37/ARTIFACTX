namespace ArtifactX.Core.Universe;

/// <summary>Inactive animation duration used by NMS build 180836 at 0x142D555D0.</summary>
public static class PreviewAnimationTiming
{
    /// <summary>Uses the initialized runtime frame end, not the configured end or resource frame count.</summary>
    public static float? InactiveDuration(int frameStart, int runtimeFrameEnd, float speed,
        float secondsPerFrame, float playbackMultiplier)
    {
        if (frameStart < 0 || runtimeFrameEnd < frameStart || !float.IsFinite(speed)
            || !float.IsFinite(secondsPerFrame) || secondsPerFrame <= 0
            || !float.IsFinite(playbackMultiplier)) return null;
        float divisor = MathF.Abs(playbackMultiplier * speed);
        if (!float.IsFinite(divisor) || divisor == 0) return null;
        float duration = ((runtimeFrameEnd - frameStart) * secondsPerFrame) / divisor;
        return float.IsFinite(duration) && duration >= 0 ? duration : null;
    }
}
