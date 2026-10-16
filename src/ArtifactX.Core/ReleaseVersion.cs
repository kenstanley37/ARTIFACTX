using System.Globalization;
using System.Text.RegularExpressions;

namespace ArtifactX.Core;

/// <summary>
/// An ArtifactX version like "1.0.0-alpha1" or a release tag like
/// "v1.0.0-beta2" (2026-10-15, for the first alpha release): the numbers,
/// plus an optional pre-release label. Ordered the semver way - a release
/// is newer than any pre-release of the same numbers, and labels compare by
/// word then number, so alpha2 &gt; alpha1, alpha10 &gt; alpha9, beta1 &gt; alpha9.
/// </summary>
public sealed record ReleaseVersion(int Major, int Minor, int Patch, string Label) : IComparable<ReleaseVersion>
{
    private static readonly Regex Pattern = new(@"^[vV]?(\d+)\.(\d+)(?:\.(\d+))?(?:\.\d+)?(?:-([0-9A-Za-z.-]+))?(?:\+.*)?$");
    private static readonly Regex LabelPart = new(@"^([A-Za-z.-]*)(\d*)$");

    public bool IsPreRelease => Label.Length > 0;

    public static ReleaseVersion? TryParse(string? text)
    {
        var m = Pattern.Match(text?.Trim() ?? "");
        if (!m.Success) return null;
        int Num(int g) => m.Groups[g].Success ? int.Parse(m.Groups[g].Value, CultureInfo.InvariantCulture) : 0;
        return new ReleaseVersion(Num(1), Num(2), Num(3), m.Groups[4].Success ? m.Groups[4].Value : "");
    }

    /// <summary>"1.0.0" or "1.0.0-alpha1".</summary>
    public override string ToString() => $"{Major}.{Minor}.{Patch}{(IsPreRelease ? "-" + Label : "")}";

    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null) return 1;
        int c = Major.CompareTo(other.Major);
        if (c == 0) c = Minor.CompareTo(other.Minor);
        if (c == 0) c = Patch.CompareTo(other.Patch);
        if (c != 0) return c;
        if (IsPreRelease != other.IsPreRelease) return IsPreRelease ? -1 : 1;
        return CompareLabels(Label, other.Label);
    }

    private static int CompareLabels(string a, string b)
    {
        var ma = LabelPart.Match(a);
        var mb = LabelPart.Match(b);
        if (!ma.Success || !mb.Success) return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        int c = string.Compare(ma.Groups[1].Value, mb.Groups[1].Value, StringComparison.OrdinalIgnoreCase);
        if (c != 0) return c;
        int na = ma.Groups[2].Value.Length > 0 ? int.Parse(ma.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
        int nb = mb.Groups[2].Value.Length > 0 ? int.Parse(mb.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
        return na.CompareTo(nb);
    }

    public static bool operator >(ReleaseVersion a, ReleaseVersion b) => a.CompareTo(b) > 0;
    public static bool operator <(ReleaseVersion a, ReleaseVersion b) => a.CompareTo(b) < 0;
}
