using ArtifactX.Core.Theming;

namespace ArtifactX.Maui.Controls;

/// <summary>A portal address drawn as glyphs; the screen reader hears the glyph names in order. Tapping it opens the
/// address large (LargeGlyphsPage) for players who can't read the small row.</summary>
public partial class GlyphRow : ContentView
{
    public static readonly BindableProperty CodeProperty = BindableProperty.Create(nameof(Code), typeof(string), typeof(GlyphRow), "",
        propertyChanged: (b, _, value) => ((GlyphRow)b).Show(value as string));

    public static readonly BindableProperty GlyphSizeProperty = BindableProperty.Create(nameof(GlyphSize), typeof(double), typeof(GlyphRow), 24.0);

    /// <summary>What the address is for, shown on the large view ("Uwaiimor XIII").</summary>
    public static readonly BindableProperty CaptionProperty = BindableProperty.Create(nameof(Caption), typeof(string), typeof(GlyphRow), "");

    /// <summary>The address's galaxy for the large view; empty means the galaxy being viewed (GalaxyService).</summary>
    public static readonly BindableProperty GalaxyProperty = BindableProperty.Create(nameof(Galaxy), typeof(string), typeof(GlyphRow), "");

    public GlyphRow()
    {
        InitializeComponent();
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await ShowLargeAsync();
        GestureRecognizers.Add(tap);
    }

    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public string Galaxy
    {
        get => (string)GetValue(GalaxyProperty);
        set => SetValue(GalaxyProperty, value);
    }

    private async Task ShowLargeAsync()
    {
        if (string.IsNullOrEmpty(Code) || Application.Current?.Windows.FirstOrDefault()?.Page is not { } root) return;
        string galaxy = Galaxy.Length > 0 ? Galaxy : Services.GalaxyService.CurrentOption.DisplayName;
        await root.Navigation.PushModalAsync(new Views.ExplorePages.LargeGlyphsPage(new ViewModels.LargeGlyphsViewModel(Code, Caption, galaxy)));
    }

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
        SemanticProperties.SetHint(this, glyphs.Count == 0 ? "" : "Tap to show the glyphs larger");
    }
}
