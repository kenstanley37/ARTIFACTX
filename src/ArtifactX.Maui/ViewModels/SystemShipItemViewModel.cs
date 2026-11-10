using ArtifactX.Core.Universe;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArtifactX.Maui.ViewModels;

public sealed partial class SystemShipItemViewModel : ObservableObject
{
    public SystemShipItemViewModel(GeneratedSystemShip ship)
    {
        Ship = ship;
    }

    public GeneratedSystemShip Ship { get; }
    public string IndexLabel => Ship.IndexLabel;
    public string TypeLabel => Ship.TypeLabel.ToUpperInvariant();
    public string Name => Ship.Name;
    public string SeedHex => Ship.SeedHex;
    public string StatsLine => Ship.StatsLine;
    public string SlotsLine => Ship.SlotsLine;
    public string SpawnChanceSummary => Ship.SpawnChanceSummary;
    public bool IsHighlightClass => Ship.Type is SystemStarshipType.Exotic or SystemStarshipType.SentinelInterceptor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CopyLabel))]
    public partial bool IsCopied { get; set; }

    public string CopyLabel => IsCopied ? "Copied!" : "Copy seed";
}
