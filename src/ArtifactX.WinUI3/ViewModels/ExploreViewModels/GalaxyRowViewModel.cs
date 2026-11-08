using ArtifactX.Core.NmsModels;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

public sealed partial class GalaxyRowViewModel : ObservableObject
{
    public GalaxyInfo Info { get; }

    public int Number => Info.Number;
    public int RealityIndex => Info.RealityIndex;
    public string Name => Info.Name;
    public GalaxyClassification Classification => Info.Classification;
    public string ClassificationName => Info.ClassificationName;
    public string Description => Info.Description;

    [ObservableProperty]
    public partial GalaxyPresence Presence { get; set; }

    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    public bool IsVisited => Presence.IsVisited;
    public bool IsUnvisited => !Presence.IsVisited;
    public int BaseCount => Presence.BaseCount;
    public int StationCount => Presence.StationCount;
    public int SettlementCount => Presence.SettlementCount;
    public bool HasBases => Presence.HasBases;
    public bool HasStations => Presence.HasStations;
    public bool HasSettlements => Presence.HasSettlements;
    public IReadOnlyList<string> Bases => Presence.Bases;
    public IReadOnlyList<string> Stations => Presence.Stations;
    public string PresenceSummary => Presence.PresenceSummary;

    public string BasesBadgeText => $"{BaseCount} ⌂";
    public string StationsBadgeText => $"{StationCount} ✦";

    public string FormattedTitle => $"{Number}. {Name}";

    public string BadgeBrushKey => Classification switch
    {
        GalaxyClassification.Lush => "ThemeDoneBrush",      // Green (#4ADE80)
        GalaxyClassification.Harsh => "ThemeAlertBrush",    // Red/Orange (#F87171)
        GalaxyClassification.Empty => "ThemeAccentBrush",   // Blue/Violet
        _ => "ThemeLineBrush"                               // Slate/Cyan
    };

    public GalaxyRowViewModel(GalaxyInfo info, GalaxyPresence presence)
    {
        Info = info;
        Presence = presence;
        IsCurrent = presence.IsCurrent;
    }

    partial void OnIsCurrentChanged(bool value)
    {
        if (Presence.IsCurrent != value)
        {
            Presence = Presence with { IsCurrent = value, IsVisited = value || Presence.IsVisited };
            OnPropertyChanged(nameof(IsVisited));
            OnPropertyChanged(nameof(IsUnvisited));
            OnPropertyChanged(nameof(PresenceSummary));
        }
    }
}
