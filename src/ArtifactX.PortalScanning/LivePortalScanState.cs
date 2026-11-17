namespace ArtifactX.PortalScanning;

public sealed record LivePortalObservation(PortalScanResult Glyphs, int? GalaxyNumber)
{
    public PortalScanTarget? Target { get; init; }
}
public sealed record LivePortalAddress(string PortalCode, int GalaxyNumber);

/// <summary>
/// Requires at least three consecutive, independently processed frames with the same address and galaxy,
/// and every one of the twelve positions read confidently in at least two of them. A frame may carry one
/// uncertain symbol (the <see cref="PortalScanQuality"/> limit): NMS draws the strip on a transparent
/// background, so look-alike symbols (e.g. F vs. its nearest class) can lose margin on one frame and not the
/// next. Pixel 7 Pro, 2026-11-10: position 8 'F' alternated d=0.187/m=0.15 and confident while the game's
/// scenery behind the strip changed; the all-confident rule kept resetting. Any differing digit or galaxy
/// still resets agreement. This reduces transient reads; repeated identical errors remain possible.
/// </summary>
public sealed class LivePortalScanState
{
    public const int RequiredFrames = 3;
    private const int RequiredConfidentReads = 2;
    private LivePortalAddress? _candidate;
    private TimeSpan _lastFrame;
    private TimeSpan _lastCompleted;
    private const int AllowedEmptyFrames = 2;
    private int _emptyFrames;
    private readonly int[] _confident = new int[12];
    public int MatchingFrames { get; private set; }

    public LivePortalAddress? Observe(LivePortalObservation observation, TimeSpan capturedAt, TimeSpan? completedAt = null)
    {
        TimeSpan completed = completedAt ?? capturedAt;
        if (completed < capturedAt) { Reset(); return null; }
        var glyphs = observation.Glyphs;
        bool validStrip = glyphs.HasCandidate && glyphs.Candidate!.All(c => "0123456789ABCDEF".Contains(c)) && glyphs.Glyphs.Count == 12 &&
            PortalScanQuality.CanReview(glyphs.Glyphs) &&
            glyphs.Candidate == new string(glyphs.Glyphs.Select(g => g.Digit).ToArray());
        if (!validStrip || observation.GalaxyNumber is not (>= 1 and <= 256))
        {
            // A strip that disagrees with the current candidate is a conflict. A frame with nothing usable
            // (hand moved, motion blur, galaxy text not read) carries no evidence either way: a short run of
            // them keeps agreement, so one blurred frame does not restart a hand-held scan.
            if (validStrip && _candidate is not null && glyphs.Candidate != _candidate.PortalCode) { Reset(); return null; }
            if (++_emptyFrames > AllowedEmptyFrames) Reset();
            return null;
        }
        _emptyFrames = 0;

        var next = new LivePortalAddress(glyphs.Candidate!, observation.GalaxyNumber.Value);
        if (_candidate != next || capturedAt <= _lastFrame || capturedAt - _lastCompleted > TimeSpan.FromSeconds(10))
        {
            MatchingFrames = 0;
            Array.Clear(_confident);
        }
        _candidate = next;
        _lastFrame = capturedAt;
        _lastCompleted = completed;
        MatchingFrames++;
        for (int i = 0; i < 12; i++)
            if (glyphs.Glyphs[i].IsCertain) _confident[i]++;
        return MatchingFrames >= RequiredFrames && _confident.All(n => n >= RequiredConfidentReads) ? next : null;
    }

    public void Reset()
    {
        _candidate = null; MatchingFrames = 0; _lastFrame = default; _lastCompleted = default; _emptyFrames = 0;
        Array.Clear(_confident);
    }
}
