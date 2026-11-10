using System.Text.Json.Serialization;

namespace ArtifactX.PortalScanning;

public sealed record GlyphShapeExample(char Digit, double[] Features);

/// <summary>
/// Versioned numerical classifier exported by GameData from a local catalog. Contains no images,
/// textures, vector paths or reference crops. Generated model files are release artifacts, not source.
/// </summary>
public sealed record PortalGlyphModel(int Version, string GameBuild, GlyphShapeExample[] Examples)
{
    public void Validate()
    {
        if (Version != 1 || string.IsNullOrWhiteSpace(GameBuild) || Examples is null ||
            Examples.Length is < 16 or > 4096 ||
            Examples.Any(e => e is null || !"0123456789ABCDEF".Contains(e.Digit) ||
                e.Features is null || e.Features.Length != GlyphShape.FeatureCount ||
                e.Features.Any(v => !double.IsFinite(v))) ||
            Examples.Select(e => e.Digit).Distinct().Count() != 16)
            throw new InvalidDataException("The portal recognition model is missing or incompatible.");
    }

    public GlyphMatch Match(double[] features)
    {
        Span<double> distances = stackalloc double[16];
        distances.Fill(double.PositiveInfinity);
        foreach (var example in Examples)
        {
            int digit = "0123456789ABCDEF".IndexOf(example.Digit);
            double squared = 0;
            for (int i = 0; i < features.Length; i++)
            {
                double difference = example.Features[i] - features[i];
                squared += difference * difference;
            }
            distances[digit] = Math.Min(distances[digit], squared);
        }
        int first = 0, second = 1;
        if (distances[second] < distances[first]) (first, second) = (second, first);
        for (int i = 2; i < 16; i++)
            if (distances[i] < distances[first]) { second = first; first = i; }
            else if (distances[i] < distances[second]) second = i;
        double best = Math.Sqrt(distances[first]), next = Math.Sqrt(distances[second]);
        return new("0123456789ABCDEF"[first], best, (next - best) / Math.Max(0.001, next));
    }
}

public sealed record GlyphMatch(char Digit, double Distance, double Margin)
{
    // An acceptance heuristic, not a calibrated probability. Every result still requires review.
    public bool IsCertain => Distance < 0.30 && Margin > 0.20;
}

public sealed record PortalScanResult(string? Candidate, IReadOnlyList<GlyphMatch> Glyphs, string Message)
{
    public bool HasCandidate => Candidate is { Length: 12 };
    public bool NeedsCorrection => Glyphs.Any(g => !g.IsCertain);
}

[JsonSerializable(typeof(PortalGlyphModel))]
public partial class PortalGlyphModelJsonContext : JsonSerializerContext;
