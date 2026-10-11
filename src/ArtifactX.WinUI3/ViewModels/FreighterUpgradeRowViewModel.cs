using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One row in FreighterUpgradesPage's blueprint list - pairs a
/// curated ArtifactX.Almanac.Freighter.FreighterUpgradeInfo entry with
/// whether the currently loaded save has it in the blueprint-unlock array
/// (NmsFreighterUpgradePaths.BlueprintArrayPath). Icon comes from the
/// shipped catalog DB when available. The 10 "Recolouring" paints have no
/// icon; they carry their real paint colour as Swatch instead (catalog
/// "FreighterPaints").</summary>
public partial class FreighterUpgradeRowViewModel : ObservableObject
{
    public required string GameId { get; init; }
    public required string DisplayName { get; init; }
    public required string Group { get; init; }

    [ObservableProperty]
    private bool isUnlocked;

    [ObservableProperty]
    private BitmapImage? icon;

    /// <summary>Paint colour drawn in place of an icon (Recolouring only).</summary>
    public Brush? Swatch { get; init; }
}
