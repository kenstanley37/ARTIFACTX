using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One row in FishingRecordsPage's species list - pairs a catalog
/// fish item (GameId/DisplayName/Icon, from CatalogService.FilterFishSpecies)
/// with whether the currently loaded SAVE SLOT has caught it
/// (NmsFishingPaths.ProductListPath). Same shape as CatalogueRowViewModel -
/// list is small enough (~210 rows) that eagerly decoding every icon up
/// front is cheap.</summary>
public partial class FishingRecordRowViewModel : ObservableObject
{
    public required string GameId { get; init; }
    public required string DisplayName { get; init; }

    [ObservableProperty]
    private bool isCaught;

    [ObservableProperty]
    private BitmapImage? icon;
}
