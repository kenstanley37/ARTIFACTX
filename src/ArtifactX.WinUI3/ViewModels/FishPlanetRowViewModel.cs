using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One known planet in FishingPage's "Where to catch it" panel.</summary>
public sealed class FishPlanetRowViewModel
{
    public required string Title { get; init; }
    public required string Detail { get; init; }
    public required string Distance { get; init; }
    public required Brush Swatch { get; init; }
    public required string PortalCode { get; init; }
    public required string BaseText { get; init; }
    public required Visibility BaseVisibility { get; init; }
    public required string SpotText { get; init; }
    public required Visibility SpotVisibility { get; init; }
    public required string MarkKey { get; init; }
    public required string MarkText { get; init; }
    public required Brush CardBorder { get; init; }
    public required string WaterKey { get; init; }
    public required string WaterButtonText { get; init; }
    public required Visibility NoWaterVisibility { get; init; }
    public required Visibility WaterButtonVisibility { get; init; }
    public required string WaterText { get; init; }
    public required Visibility WaterVisibility { get; init; }
}
