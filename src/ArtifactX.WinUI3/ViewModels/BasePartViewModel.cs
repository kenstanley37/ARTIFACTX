using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using System.Globalization;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One kind of part in a base ("Timber Floor Panel · 237") on BaseDetailsPage.</summary>
public sealed partial class BasePartViewModel : ObservableObject
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int Count { get; init; }
    public ImageSource? Icon { get; init; }

    /// <summary>A glyph shown in place of a missing catalog icon.</summary>
    public required string Glyph { get; init; }

    /// <summary>False for the pieces the game places itself (a freighter's bridge
    /// connector...), which the player can't remove in game either.</summary>
    public required bool CanSelect { get; init; }

    /// <summary>Why it can't be selected ("Built into the station - can't be removed"), or empty.</summary>
    public string Note { get; init; } = "";
    public bool HasNote => Note.Length > 0;

    public string CountLabel => Count.ToString("N0", CultureInfo.CurrentCulture);
    public bool HasIcon => Icon is not null;
    public bool ShowGlyph => Icon is null;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
