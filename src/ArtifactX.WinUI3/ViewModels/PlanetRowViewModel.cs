using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One result row in PlanetsPage's planet finder - a KnownPlanet
/// (ArtifactX.Core.NmsModels.NmsPlanets) shaped for display. Read-only; the
/// finder never edits anything.</summary>
public sealed class PlanetRowViewModel
{
    public required string TypeLabel { get; init; }
    public required string FactsLabel { get; init; }
    public required Brush Swatch { get; init; }
    public required Visibility RingVisibility { get; init; }
    public required string LocationLabel { get; init; }
    public required string PortalCode { get; init; }
    public required List<ImageSource> Glyphs { get; init; }
    public required string DiscoveredLabel { get; init; }
    public required string DistanceLabel { get; init; }
    public required string NotesLabel { get; init; }
    public required Visibility NotesVisibility { get; init; }
    /// <summary>The planet itself, for opening it in full.</summary>
    public required ArtifactX.Core.NmsModels.KnownPlanet Planet { get; init; }
}
