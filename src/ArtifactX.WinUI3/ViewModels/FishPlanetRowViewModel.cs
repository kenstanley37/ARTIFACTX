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
    public required List<ImageSource> Glyphs { get; init; }
    public required string BaseText { get; init; }
    public required Visibility BaseVisibility { get; init; }
}
