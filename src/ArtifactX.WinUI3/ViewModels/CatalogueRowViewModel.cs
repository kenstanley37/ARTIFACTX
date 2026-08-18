using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One row in CataloguePage's technology list - pairs a catalog
/// item (GameId/DisplayName/Icon) with whether the currently loaded SAVE SLOT
/// has it in its known-technology list (NmsCataloguePaths.KnownTechnologyArrayPath).
/// No CategoryLabel, unlike AccountItemRowViewModel - this list is scoped to
/// GcTechnologyTable only, so a per-row category badge would be redundant.
/// Icon is populated once at load time via CatalogService.WarmCacheAsync/TryGet
/// (only ~370 rows here, unlike AccountDataPage's ~4,700 - small enough that
/// eagerly decoding every icon up front is cheap, not the same concern that
/// made AccountDataPage skip icons entirely).</summary>
public partial class CatalogueRowViewModel : ObservableObject
{
    public required string GameId { get; init; }
    public required string DisplayName { get; init; }

    /// <summary>Suit/Ship/Weapon/Freighter/"All" (shared tech), or null -
    /// drives CataloguePage's category filter, same UsageCategory values the
    /// Exosuit/Ships/MultiTool/Freighter pages already filter their own tech
    /// grids by.</summary>
    public string? UsageCategory { get; init; }

    [ObservableProperty]
    private bool isKnown;

    [ObservableProperty]
    private BitmapImage? icon;
}
