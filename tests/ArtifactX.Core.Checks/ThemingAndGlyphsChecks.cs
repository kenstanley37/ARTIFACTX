using ArtifactX.Core.Theming;

internal static class ThemingAndGlyphsChecks
{
    public static void Check()
    {
        if (ArtifactXThemes.All.Count != 2)
            throw new InvalidOperationException("Expected GalaxyMap and StationCore shared themes.");

        foreach (var theme in ArtifactXThemes.All)
        {
            if (string.IsNullOrWhiteSpace(theme.Id) ||
                !theme.VoidHex.StartsWith('#') ||
                !theme.AccentHex.StartsWith('#') ||
                !theme.PanelHex.StartsWith('#'))
            {
                throw new InvalidOperationException($"Invalid theme token definition for {theme.Id}.");
            }
        }

        if (ArtifactXPortalGlyphs.All.Count != 16)
            throw new InvalidOperationException("Expected 16 hexadecimal portal glyph vector definitions.");

        for (int i = 0; i < 16; i++)
        {
            char hex = i.ToString("X")[0];
            var glyph = ArtifactXPortalGlyphs.ForDigit(hex);
            if (glyph.Index != i || glyph.HexDigit != hex || string.IsNullOrWhiteSpace(glyph.PathData))
                throw new InvalidOperationException($"Invalid vector glyph definition for {hex}.");
        }
    }
}
