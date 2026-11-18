using ArtifactX.Core.Theming;

namespace ArtifactX.Maui.ViewModels;

/// <summary>One glyph in the large view: its place in the dialling order and the glyph.</summary>
public sealed record LargeGlyphItem(int Position, PortalGlyphVector Glyph)
{
    public string PositionLabel => Position.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public string Description => $"Glyph {Position}: {Glyph.Name}";
}

/// <summary>
/// A portal address shown large, for players who can't read the small glyph rows (owner, 2026-11-18): the 12 glyphs
/// numbered in dialling order, with what the address is for and its galaxy.
/// </summary>
public sealed class LargeGlyphsViewModel(string code, string caption, string galaxy)
{
    public IReadOnlyList<LargeGlyphItem> Glyphs { get; } = code.Where(Uri.IsHexDigit).Take(12)
        .Select((digit, i) => new LargeGlyphItem(i + 1, ArtifactXPortalGlyphs.ForDigit(digit))).ToList();

    public string Caption { get; } = caption.Length > 0 ? caption : "Portal address";

    public string GalaxyLine { get; } = galaxy.Length > 0 ? $"In {galaxy}" : "";

    public bool HasGalaxy => GalaxyLine.Length > 0;

    public string Hint { get; } = "Dial them in order, left to right, top to bottom.";
}
