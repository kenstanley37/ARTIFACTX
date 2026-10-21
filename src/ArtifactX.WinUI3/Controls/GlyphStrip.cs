using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

/// <summary>
/// Wraps a row of portal glyphs so it can be clicked to open the large glyph
/// view (GlyphViewer) - for anyone who finds the small glyphs hard to read.
/// Looks like the row it wraps; on hover/focus it gets an outline and a
/// magnifier badge (GlyphStripButtonStyle), and it works with the keyboard and
/// screen readers like any button.
///
/// With GlyphSize set it draws the row itself from Code (the usual case: a
/// card's address); without it, it shows whatever content it's given.
/// </summary>
public sealed class GlyphStrip : Button
{
    public static readonly DependencyProperty CodeProperty =
        DependencyProperty.Register(nameof(Code), typeof(string), typeof(GlyphStrip), new PropertyMetadata("", OnCodeChanged));

    public static readonly DependencyProperty GlyphSizeProperty =
        DependencyProperty.Register(nameof(GlyphSize), typeof(double), typeof(GlyphStrip), new PropertyMetadata(0.0, (d, _) => ((GlyphStrip)d).DrawGlyphs()));

    public static readonly DependencyProperty HighlightFirstProperty =
        DependencyProperty.Register(nameof(HighlightFirst), typeof(bool), typeof(GlyphStrip), new PropertyMetadata(false, (d, _) => ((GlyphStrip)d).DrawGlyphs()));

    /// <summary>The 12-digit portal code the glyphs spell.</summary>
    public string Code
    {
        get => (string)GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    /// <summary>The size of each glyph's square when the strip draws the row itself:
    /// 24 on cards, 20 in compact lists. 0 (the default) leaves the content alone.</summary>
    public double GlyphSize
    {
        get => (double)GetValue(GlyphSizeProperty);
        set => SetValue(GlyphSizeProperty, value);
    }

    /// <summary>Outlines the first glyph, the one that picks the planet.</summary>
    public bool HighlightFirst
    {
        get => (bool)GetValue(HighlightFirstProperty);
        set => SetValue(HighlightFirstProperty, value);
    }

    /// <summary>Optional heading for the large view, e.g. the system's name.</summary>
    public string Title { get; set; } = "";

    public GlyphStrip()
    {
        Style = (Style)Application.Current.Resources["GlyphStripButtonStyle"];
        ToolTipService.SetToolTip(this, "Click to enlarge");
        Click += async (_, _) => await GlyphViewer.ShowAsync(XamlRoot, Code, Title);
    }

    private static void OnCodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var strip = (GlyphStrip)d;
        string code = e.NewValue as string ?? "";
        strip.IsEnabled = code.Length == 12;
        AutomationProperties.SetName(strip, code.Length == 12 ? $"Portal address {code}. Click to enlarge the glyphs." : "Portal address");
        strip.DrawGlyphs();
    }

    private async void DrawGlyphs()
    {
        if (GlyphSize <= 0) return;
        string code = Code;
        await PortalGlyphs.WarmAsync();
        if (code != Code) return; // changed while the pictures loaded

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = GlyphSize >= 24 ? 2 : 1 };
        foreach (var image in PortalGlyphs.Images(code))
        {
            bool outlined = HighlightFirst && row.Children.Count == 0;
            row.Children.Add(new Border
            {
                Width = GlyphSize,
                Height = GlyphSize,
                Background = ThemeResources.Brush("ThemeVoidBrush"),
                BorderBrush = outlined ? ThemeResources.Brush("ThemeAccentBrush") : null,
                BorderThickness = new Thickness(outlined ? 1 : 0),
                Child = new Image { Source = image, Width = GlyphSize * 0.84, Height = GlyphSize * 0.84 },
            });
        }
        Content = row;
    }
}
