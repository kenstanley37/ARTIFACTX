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
/// </summary>
public sealed class GlyphStrip : Button
{
    public static readonly DependencyProperty CodeProperty =
        DependencyProperty.Register(nameof(Code), typeof(string), typeof(GlyphStrip), new PropertyMetadata("", OnCodeChanged));

    /// <summary>The 12-digit portal code the glyphs spell.</summary>
    public string Code
    {
        get => (string)GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
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
    }
}
