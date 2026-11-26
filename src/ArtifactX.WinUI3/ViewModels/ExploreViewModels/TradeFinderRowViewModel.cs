using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml.Media;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>One place to buy the picked item in the Trade Finder: the price, the system, its economy and how far.</summary>
public sealed class TradeFinderRowViewModel
{
    public TradeFinderRowViewModel(TradeFinderStation station, TradeStockItem item, string itemName, ImageSource? icon,
        ulong? here, double? distance)
    {
        ulong system = station.SystemAddress;
        Icon = icon;
        ItemName = itemName;
        Price = item.BuyPrice ?? int.MaxValue;
        PriceLabel = item.BuyPrice is int price ? $"{price:N0} units" : "Price not known";
        KindLabel = item.Kind == TradeStockKind.TradeGood ? "Trade good of this economy"
            : item.Always ? "Every station sells it" : "This station's pick";
        SystemLabel = $"{UniverseInfo.SystemName(system)} · {UniverseInfo.Region(system)}";
        EconomyLabel = $"{UniverseInfo.Economy(system)} · {UniverseInfo.Wealth(system)}";
        DistanceLabel = here is ulong h && NmsSpaceStationPaths.ToSystemAddress(h) == system ? "In the system you're in"
            : distance switch
            {
                null => "In a different galaxy from you",
                < 1 => JumpPlanner.WithJumps("Under 400 ly from you", 1),
                double ly => JumpPlanner.WithJumps($"About {ly:N0} ly from you", ly),
            };
        Distance = distance;
        PortalCode = NmsSpaceStationPaths.PortalAddress(system, 1);
        AutomationName = $"{ItemName}, {PriceLabel} at the space station of {SystemLabel}, {EconomyLabel}, {DistanceLabel}.";
    }

    public ImageSource? Icon { get; }
    public string ItemName { get; }
    public int Price { get; }
    public string PriceLabel { get; }
    public string KindLabel { get; }
    public string SystemLabel { get; }
    public string EconomyLabel { get; }
    public string DistanceLabel { get; }
    public double? Distance { get; }
    public string PortalCode { get; }
    public string AutomationName { get; }

    /// <summary>What a screen reader announces for the result.</summary>
    public override string ToString() => AutomationName;
}
