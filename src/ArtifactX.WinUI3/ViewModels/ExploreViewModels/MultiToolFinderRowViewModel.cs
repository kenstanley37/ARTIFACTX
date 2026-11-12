using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml.Media;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>One cabinet offer in the Multi-Tool Finder: the tool, its kind and class, the system, how far, and where
/// the game must be loaded for the cabinet to sell it.</summary>
public sealed class MultiToolFinderRowViewModel
{
    private const string Letters = "CBAS";

    public MultiToolFinderRowViewModel(CabinetMultiToolFind find, ulong? here, double? distance)
    {
        Find = find;
        ulong system = find.SystemAddress;
        Name = find.Tool.DefaultName;
        Kind = find.Kind;
        ClassLetter = find.ClassIndex is >= 0 and < 4 ? Letters[find.ClassIndex].ToString() : "?";
        ClassBrush = ThemeResources.ClassBrush(ClassLetter);
        SystemLabel = $"{UniverseInfo.SystemName(system)} · {UniverseInfo.Region(system)}";
        IsOtherGalaxy = here is ulong h && NmsSpaceStationPaths.GalaxyNumber(h) != NmsSpaceStationPaths.GalaxyNumber(system);
        int galaxy = NmsSpaceStationPaths.GalaxyNumber(system);
        GalaxyLabel = IsOtherGalaxy ? $"{PlanetText.GalaxyLabel(galaxy)} · not your galaxy" : PlanetText.GalaxyLabel(galaxy);
        DistanceLabel = here is ulong h2 && NmsSpaceStationPaths.ToSystemAddress(h2) == system ? "In the system you're in"
            : distance switch
            {
                null => "In a different galaxy from you",
                < 1 => JumpPlanner.WithJumps("Under 400 ly from you", 1),
                double ly => JumpPlanner.WithJumps($"About {ly:N0} ly from you", ly),
            };
        // Where to buy it: the station cabinet, and the place to load the game at first (arriving counts as a load in
        // space, so body 0 is what the cabinet sells on arrival).
        CabinetLabel = $"Space station, cabinet {find.Cabinet}";
        LoadLabel = find.ReloadBody == 0 ? "Sold when you arrive"
            : $"Save and reload on {UniverseInfo.PlanetName(system, find.ReloadBody)} first";
        IsOnArrival = find.ReloadBody == 0;
        PortalCode = NmsSpaceStationPaths.PortalAddress(system, 1);
        AutomationName = $"{ClassLetter}-class {Kind} {Name}, {SystemLabel}, {DistanceLabel}, {GalaxyLabel}. {CabinetLabel}. {LoadLabel}.";
    }

    public CabinetMultiToolFind Find { get; }
    public string Name { get; }
    public string Kind { get; }
    public string ClassLetter { get; }
    public Brush ClassBrush { get; }
    public string SystemLabel { get; }
    public string GalaxyLabel { get; }
    public bool IsOtherGalaxy { get; }
    public bool IsThisGalaxy => !IsOtherGalaxy;
    public string DistanceLabel { get; }
    public string CabinetLabel { get; }
    public string LoadLabel { get; }
    public bool IsOnArrival { get; }
    public string PortalCode { get; }
    public string AutomationName { get; }

    /// <summary>What a screen reader announces for the result.</summary>
    public override string ToString() => AutomationName;
}
