using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One row in FreighterUpgradesPage's blueprint list - pairs a
/// curated ArtifactX.Almanac.Freighter.FreighterUpgradeInfo entry with
/// whether the currently loaded save has it in the blueprint-unlock array
/// (NmsFreighterUpgradePaths.BlueprintArrayPath). Icon comes from the
/// shipped catalog DB when available - the 10 "Recolouring" entries aren't
/// in it at all (see FreighterUpgrades' own doc comment), so Icon is null
/// for those and the tile just shows its DisplayName with no image.</summary>
public partial class FreighterUpgradeRowViewModel : ObservableObject
{
    public required string GameId { get; init; }
    public required string DisplayName { get; init; }
    public required string Group { get; init; }

    [ObservableProperty]
    private bool isUnlocked;

    [ObservableProperty]
    private BitmapImage? icon;
}
