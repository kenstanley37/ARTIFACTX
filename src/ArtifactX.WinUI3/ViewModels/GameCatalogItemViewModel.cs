using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One item tile on the Catalog page, shown like the game's Catalog: coloured
/// when known, dimmed when not. CanEdit is false for items the game counts as known
/// without a save list to change (not craftable, or a known special) and for sections
/// whose list isn't mapped yet (Materials &amp; Items).</summary>
public sealed partial class GameCatalogItemViewModel : ObservableObject, IUnlockRow
{
    public GameCatalogItemViewModel(string gameId, string name, bool known, bool canEdit, bool showState, string? readOnlyReason,
        IReadOnlyList<string>? listIds = null, string? detail = null, string? iconId = null, string? species = null)
    {
        GameId = gameId;
        Detail = detail;
        Species = species;
        IconId = iconId ?? gameId;
        ListIds = listIds ?? [gameId];
        Name = name;
        IsUnlocked = known;
        CanEdit = canEdit;
        ShowState = showState;
        ReadOnlyReason = readOnlyReason;
    }

    public string GameId { get; }

    /// <summary>A second line under the name: where a Wonders record was set.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetail))]
    public partial string? Detail { get; set; }
    public bool HasDetail => !string.IsNullOrEmpty(Detail);

    /// <summary>The species name a Fauna or Flora record shows in game ("J. Mustlimii").</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSpecies))]
    public partial string? Species { get; set; }
    public bool HasSpecies => !string.IsNullOrEmpty(Species);

    /// <summary>The catalog item whose picture the tile shows (a record shows the item it recorded).</summary>
    public string IconId { get; }

    /// <summary>The list entries this tile stands for: its id, or a Recipes result's recipes.</summary>
    public IReadOnlyList<string> ListIds { get; }
    public string Name { get; }
    public bool CanEdit { get; }

    /// <summary>False where the known state isn't read yet: tiles show plainly, no tick.</summary>
    public bool ShowState { get; }
    public string? ReadOnlyReason { get; }

    [ObservableProperty] public partial bool IsUnlocked { get; set; }
    [ObservableProperty] public partial BitmapImage? Icon { get; set; }

    /// <summary>A dark outline of Icon, shown until the item is known, as the game's Catalog does.</summary>
    [ObservableProperty] public partial ImageSource? Silhouette { get; set; }

    public ImageSource? DisplayIcon => ShowState && !IsUnlocked && Silhouette is not null ? Silhouette : Icon;
    public double IconOpacity => ShowState && !IsUnlocked && Silhouette is null ? 0.45 : 1;

    public double TileOpacity => !ShowState || IsUnlocked ? 1 : 0.45;
    public bool ShowTick => ShowState && CanEdit;

    /// <summary>A Wonders record slot ("WONDER:PlanetWonders:3"): no item picture.</summary>
    public bool IsRecord => GameId.StartsWith("WONDER:", System.StringComparison.Ordinal) && IconId == GameId;
    public bool ShowLock => ShowState && !CanEdit;
    public string ToolTip => ReadOnlyReason is null ? Name : $"{Name} - {ReadOnlyReason}";

    partial void OnIconChanged(BitmapImage? value) => OnPropertyChanged(nameof(DisplayIcon));

    partial void OnSilhouetteChanged(ImageSource? value)
    {
        OnPropertyChanged(nameof(DisplayIcon));
        OnPropertyChanged(nameof(IconOpacity));
    }

    partial void OnIsUnlockedChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayIcon));
        OnPropertyChanged(nameof(IconOpacity));
        OnPropertyChanged(nameof(TileOpacity));
        OnPropertyChanged(nameof(ToolTip));
    }

    /// <summary>What a screen reader says for the tile in the grid.</summary>
    public override string ToString() => !ShowState ? Name
        : $"{Name}{(HasSpecies ? $", {Species}" : "")}, {(IsUnlocked ? "known" : "not known")}{(ReadOnlyReason is null ? "" : $", {ReadOnlyReason}")}";
}
