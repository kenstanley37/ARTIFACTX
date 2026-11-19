using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Recipes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArtifactX.Maui.ViewModels;

/// <summary>Coordinates a console player types into nothing but reads off the Analysis Visor: copied, and the button
/// says so for a moment.</summary>
public abstract partial class CopyCoordinatesViewModel(string coordinates) : ObservableObject
{
    public string Coordinates { get; } = coordinates;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CopyLabel))]
    public partial bool IsCopied { get; set; }

    public string CopyLabel => IsCopied ? "Copied" : "Copy";

    [RelayCommand]
    private async Task CopyAsync()
    {
        await Clipboard.Default.SetTextAsync(Coordinates);
        IsCopied = true;
        await Task.Delay(1500);
        IsCopied = false;
    }
}

/// <summary>A base computer spot near the portal: what it reaches, where to stand, and each hotspot's distance.</summary>
public sealed class BaseSpotItemViewModel : CopyCoordinatesViewModel
{
    public BaseSpotItemViewModel(PlanetHotspotCluster spot, float baseReach, int rank) : base(spot.BaseCoordinates)
    {
        Title = $"Option {rank}: {spot.Title}";
        (SpareText, IsTight) = PortalSurvey.Spare(spot, baseReach);
        Lines = PortalSurvey.Lines(spot)
            .Select(l => $"{l.Description} · {l.Amount} · {l.FromComputer} from the computer" + (l.FromPortal.Length > 0 ? $" · {l.FromPortal} from the portal" : ""))
            .ToList();
    }

    public string Title { get; }
    public string SpareText { get; }

    /// <summary>Little room to spare: placing the computer by eye may miss a hotspot.</summary>
    public bool IsTight { get; }

    public IReadOnlyList<string> Lines { get; }
    public string CopyDescription => $"Copy base computer coordinates for {Title}";
}

/// <summary>A hotspot near the portal.</summary>
public sealed class NearbyHotspotViewModel(PlanetHotspot hotspot) : CopyCoordinatesViewModel(hotspot.Coordinates)
{
    public string Title { get; } = $"{hotspot.ResourceName} · class {hotspot.Class}";

    public string Detail { get; } = (hotspot.Kind == "Power" ? $"{Math.Round(hotspot.Strength)} kP" : $"{hotspot.YieldPercent}% yield")
        + (hotspot.PortalDistanceLabel is { } fromPortal ? $" · {fromPortal}" : "");

    public string CopyDescription => $"Copy coordinates for {Title}";
}

/// <summary>A crashed ship or shop near the portal (PlanetFindsService), with its coordinates to copy.</summary>
public sealed class PlanetFindViewModel(ArtifactX.Maui.Services.PlanetFind find) : CopyCoordinatesViewModel(find.Coordinates)
{
    public string Title { get; } = find.Title;
    public string Detail { get; } = find.Detail;
    public bool IsBest { get; } = find.IsBest;
    public string CopyDescription => $"Copy coordinates for {find.Title}";
}
