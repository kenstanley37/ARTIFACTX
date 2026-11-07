using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// A "still to catch" tile on Fishing: a group of planet types that shares its own fish
/// (or every fish, or the fish of any planet with water), how many are caught, and a
/// click to list the rest. The one the list is showing is outlined.
/// </summary>
public sealed class FishProgressTileViewModel(string title, int caught, int total, int biome, bool isSelected, bool isOverall = false)
{
    public string Title { get; } = title;
    public int Caught { get; } = caught;
    public int Total { get; } = total;

    /// <summary>The planet type to filter by when clicked (FishingPage's AllTypes for "All fish").</summary>
    public int Biome { get; } = biome;

    public bool AllCaught => Caught == Total;
    public string CountText => AllCaught ? "✓ all" : $"{Caught} / {Total}";
    public double Maximum => System.Math.Max(1, Total);
    public double Value => Caught;

    public string Tip => AllCaught ? $"You've caught every {Title} fish" : $"{Total - Caught} {Title} fish still to catch - click to show them";

    public Brush TitleBrush => ThemeResources.Brush(isOverall ? "ThemeAccentBrush" : "ThemeTextBrush");
    public Windows.UI.Text.FontWeight TitleWeight => isOverall ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
    public Brush CountBrush => ThemeResources.Brush(AllCaught ? "ThemeDoneBrush" : "ThemeTextDimBrush");
    public Brush Border => ThemeResources.Brush(isSelected ? "ThemeAccentBrush" : "ThemeLineBrush");
    public Thickness BorderThickness => new(isSelected ? 2 : 1);
}
