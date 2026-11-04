using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One item tile on the Catalog page, shown like the game's Catalog: coloured
/// when known, dimmed when not. CanEdit is false for items the game counts as known
/// without a save list to change (not craftable, or a known special) and for sections
/// whose list isn't mapped yet (Materials &amp; Items).</summary>
public sealed partial class GameCatalogItemViewModel : ObservableObject, IUnlockRow
{
    public GameCatalogItemViewModel(string gameId, string name, bool known, bool canEdit, bool showState, string? readOnlyReason,
        IReadOnlyList<string>? listIds = null, string? detail = null, string? iconId = null)
    {
        GameId = gameId;
        Detail = detail;
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
    public string? Detail { get; }
    public bool HasDetail => !string.IsNullOrEmpty(Detail);

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

    public double TileOpacity => !ShowState || IsUnlocked ? 1 : 0.45;
    public bool ShowTick => ShowState && CanEdit;

    /// <summary>A Wonders record slot ("WONDER:PlanetWonders:3"): no item picture.</summary>
    public bool IsRecord => GameId.StartsWith("WONDER:", System.StringComparison.Ordinal) && IconId == GameId;
    public bool ShowLock => ShowState && !CanEdit;
    public string ToolTip => ReadOnlyReason is null ? Name : $"{Name} - {ReadOnlyReason}";

    partial void OnIsUnlockedChanged(bool value)
    {
        OnPropertyChanged(nameof(TileOpacity));
        OnPropertyChanged(nameof(ToolTip));
    }

    /// <summary>What a screen reader says for the tile in the grid.</summary>
    public override string ToString() => !ShowState ? Name
        : $"{Name}, {(IsUnlocked ? "known" : "not known")}{(ReadOnlyReason is null ? "" : $", {ReadOnlyReason}")}";
}
