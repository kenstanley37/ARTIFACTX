using CommunityToolkit.Mvvm.ComponentModel;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One row in CataloguePage's technology list - pairs a catalog
/// item (GameId/DisplayName) with whether the currently loaded SAVE SLOT has
/// it in its known-technology list (NmsCataloguePaths.KnownTechnologyArrayPath).
/// No CategoryLabel, unlike AccountItemRowViewModel - this list is scoped to
/// GcTechnologyTable only, so a per-row category badge would be redundant.</summary>
public partial class CatalogueRowViewModel : ObservableObject
{
    public required string GameId { get; init; }
    public required string DisplayName { get; init; }

    [ObservableProperty]
    private bool isKnown;
}
