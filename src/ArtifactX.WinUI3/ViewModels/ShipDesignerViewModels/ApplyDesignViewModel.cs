using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels.ShipDesignerViewModels;

/// <summary>One of the player's ships the design can go on.</summary>
public sealed record ApplyShipChoice(int Index, string Label, bool Named)
{
    public override string ToString() => Label;
}

/// <summary>
/// The "Apply the design to which ship?" choice. A ship with a name of its own keeps it unless asked; one without
/// takes the design's name anyway (the game names it from the new seed). The rename box is always shown, so the
/// choice can be found: greyed out and ticked when there is nothing to choose.
/// </summary>
public sealed partial class ApplyDesignViewModel : ObservableObject
{
    private bool _renameChoice = true;

    public ApplyDesignViewModel(string designName, IEnumerable<ApplyShipChoice> ships)
    {
        Ships = ships.ToList();
        Intro = $"The ship gets this design's type, parts, paint and name (\"{designName}\"). Its technology, cargo, class and stats stay as they are.";
        RenameText = $"Rename it to \"{designName}\"";
    }

    public List<ApplyShipChoice> Ships { get; }
    public string Intro { get; }
    public string RenameText { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial ApplyShipChoice? SelectedShip { get; set; }

    [ObservableProperty]
    public partial bool RenameEnabled { get; set; }

    [ObservableProperty]
    public partial bool? Rename { get; set; } = true;

    [ObservableProperty]
    public partial string RenameNote { get; set; } = "Pick a ship. One you named yourself can keep its name.";

    public bool HasSelection => SelectedShip is not null;

    partial void OnSelectedShipChanged(ApplyShipChoice? value)
    {
        bool named = value?.Named == true;
        RenameEnabled = named;
        Rename = !named || _renameChoice;
        RenameNote = named ? "You named this ship. Untick to keep its name." : "The game named this ship, so it takes the design's name.";
    }

    /// <summary>The player ticked or unticked the box: remembered for the next named ship picked.</summary>
    public void RenameClicked()
    {
        if (RenameEnabled) _renameChoice = Rename == true;
    }
}
