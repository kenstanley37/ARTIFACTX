using CommunityToolkit.Mvvm.ComponentModel;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One row in LanguageWordsControl's word list - pairs a catalog word
/// (GameId/DisplayName) with whether the currently loaded save slot knows it
/// (NmsLanguagePaths.KnownWordsArrayPath). No Icon, unlike CatalogueRowViewModel -
/// words have no in-game icon asset at all.</summary>
public partial class LanguageWordRowViewModel : ObservableObject
{
    public required string GameId { get; init; }
    public required string DisplayName { get; init; }

    [ObservableProperty]
    private bool isKnown;
}
