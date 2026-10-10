using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Reads the active theme's Theme* resources (Themes/*.xaml) from code, for
/// controls a page builds at runtime. Theme* keys are plain per-theme values
/// (not Light/Dark theme-dictionary entries), so an indexer lookup is safe;
/// pages are re-created on a theme change (ThemeService), which re-reads them.
/// </summary>
public static class ThemeResources
{
    public static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

    public static FontFamily Font(string key) => (FontFamily)Application.Current.Resources[key];

    public static CornerRadius CardCornerRadius => (CornerRadius)Application.Current.Resources["ThemeCardCornerRadius"];

    /// <summary>Border + fill for a toggle-style choice button (ship picker,
    /// Class selector): accent outline and tinted fill when selected.</summary>
    public static void StyleChoiceButton(Microsoft.UI.Xaml.Controls.Button button, bool isSelected)
    {
        button.BorderThickness = new Thickness(isSelected ? 2 : 1);
        button.BorderBrush = Brush(isSelected ? "ThemeAccentBrush" : "ThemeLineBrush");
        button.Background = Brush(isSelected ? "ThemeSelectedFillBrush" : "ThemePanelAltBrush");
        button.CornerRadius = CardCornerRadius;
    }
}
