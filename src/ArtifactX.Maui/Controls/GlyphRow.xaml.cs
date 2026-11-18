using ArtifactX.Core.Theming;

namespace ArtifactX.Maui.Controls;

/// <summary>A portal address drawn as glyphs; the screen reader hears the glyph names in order.</summary>
public partial class GlyphRow : ContentView
{
    public static readonly BindableProperty CodeProperty = BindableProperty.Create(nameof(Code), typeof(string), typeof(GlyphRow), "",
        propertyChanged: (b, _, value) => ((GlyphRow)b).Show(value as string));

    public static readonly BindableProperty GlyphSizeProperty = BindableProperty.Create(nameof(GlyphSize), typeof(double), typeof(GlyphRow), 24.0);

    public GlyphRow() => InitializeComponent();

    /// <summary>The address (hex digits); anything else is skipped.</summary>
    public string Code
    {
        get => (string)GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    public double GlyphSize
    {
        get => (double)GetValue(GlyphSizeProperty);
        set => SetValue(GlyphSizeProperty, value);
    }

    private void Show(string? code)
    {
        var glyphs = (code ?? "").Where(Uri.IsHexDigit).Take(12).Select(ArtifactXPortalGlyphs.ForDigit).ToList();
        BindableLayout.SetItemsSource(Glyphs, glyphs);
        // Fewer than 12 (still typing): left-aligned, not spread across the row.
        Glyphs.JustifyContent = glyphs.Count == 12 ? Microsoft.Maui.Layouts.FlexJustify.SpaceBetween : Microsoft.Maui.Layouts.FlexJustify.Start;
        SemanticProperties.SetDescription(this, glyphs.Count == 0 ? "" : "Portal glyphs: " + string.Join(", ", glyphs.Select(g => g.Name)));
    }
}
