namespace ArtifactX.PortalScanning;

public sealed record LivePortalObservation(PortalScanResult Glyphs, int? GalaxyNumber)
{
    public PortalScanTarget? Target { get; init; }
}
public sealed record LivePortalAddress(string PortalCode, int GalaxyNumber);

/// <summary>
/// Requires three consecutive, independently processed frames with the same confident address and galaxy.
/// This reduces transient reads; repeated errors remain possible and require real-device validation.
/// </summary>
public sealed class LivePortalScanState
{
    private LivePortalAddress? _candidate;
    private TimeSpan _lastFrame;
    private TimeSpan _lastCompleted;
    public int MatchingFrames { get; private set; }

    public LivePortalAddress? Observe(LivePortalObservation observation, TimeSpan capturedAt, TimeSpan? completedAt = null)
    {
        TimeSpan completed = completedAt ?? capturedAt;
        if (completed < capturedAt) { Reset(); return null; }
        var glyphs = observation.Glyphs;
        if (!glyphs.HasCandidate || glyphs.Candidate!.Any(c => !"0123456789ABCDEF".Contains(c)) || glyphs.Glyphs.Count != 12 || glyphs.NeedsCorrection ||
            !PortalScanQuality.CanReview(glyphs.Glyphs) ||
            glyphs.Candidate != new string(glyphs.Glyphs.Select(g => g.Digit).ToArray()) ||
            observation.GalaxyNumber is not (>= 1 and <= 256))
        { Reset(); return null; }

        var next = new LivePortalAddress(glyphs.Candidate!, observation.GalaxyNumber.Value);
        if (_candidate != next || capturedAt <= _lastFrame || capturedAt - _lastCompleted > TimeSpan.FromSeconds(10))
            MatchingFrames = 0;
        _candidate = next;
        _lastFrame = capturedAt;
        _lastCompleted = completed;
        MatchingFrames++;
        return MatchingFrames >= 3 ? next : null;
    }

    public void Reset() { _candidate = null; MatchingFrames = 0; _lastFrame = default; _lastCompleted = default; }
}
