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

    /// <summary>How close a count is to a limit (NmsBuildCounts' levels): green with room,
    /// yellow when close, red at or over it. The same in every theme - it's a status, not
    /// decoration (each theme's ThemeDoneBrush is its own colour, e.g. yellow).</summary>
    public static Brush LevelBrush(int level) => new SolidColorBrush(level switch
    {
        2 => Windows.UI.Color.FromArgb(255, 0xFF, 0x5A, 0x5A),
        1 => Windows.UI.Color.FromArgb(255, 0xF5, 0xC5, 0x42),
        _ => Windows.UI.Color.FromArgb(255, 0x3D, 0xDC, 0x84),
    });

    /// <summary>The game's class-tier colours (S gold, A purple, B blue, C green) for class
    /// badges - fixed, not themed, as they're the game's own. Transparent for anything else.</summary>
    public static Windows.UI.Color ClassColour(string? cls) => cls switch
    {
        "S" => Windows.UI.Color.FromArgb(255, 201, 151, 34),
        "A" => Windows.UI.Color.FromArgb(255, 130, 80, 200),
        "B" => Windows.UI.Color.FromArgb(255, 60, 110, 200),
        "C" => Windows.UI.Color.FromArgb(255, 60, 150, 90),
        "X" => Windows.UI.Color.FromArgb(255, 72, 72, 82), // the game's dark illegal-module badge
        _ => Microsoft.UI.Colors.Transparent,
    };

    public static Brush ClassBrush(string? cls) => new SolidColorBrush(ClassColour(cls));

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
