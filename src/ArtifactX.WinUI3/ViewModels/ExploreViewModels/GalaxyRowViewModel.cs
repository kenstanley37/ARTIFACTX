using ArtifactX.Core.NmsModels;
using CommunityToolkit.Mvvm.ComponentModel;

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
    public partial bool IsCurrent { get; set; }

    public string FormattedTitle => $"{Number}. {Name}";

    public string BadgeBrushKey => Classification switch
    {
        GalaxyClassification.Lush => "ThemeDoneBrush",      // Green (#4ADE80)
        GalaxyClassification.Harsh => "ThemeAlertBrush",    // Red/Orange (#F87171)
        GalaxyClassification.Empty => "ThemeAccentBrush",   // Blue/Violet
        _ => "ThemeLineBrush"                               // Slate/Cyan
    };

    public GalaxyRowViewModel(GalaxyInfo info, bool isCurrent)
    {
        Info = info;
        IsCurrent = isCurrent;
    }
}
