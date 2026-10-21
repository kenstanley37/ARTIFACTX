using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml.Media;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One result in the Planet Finder - a KnownPlanet (NmsPlanets) shaped for display.</summary>
public sealed class PlanetRowViewModel
{
    public PlanetRowViewModel(KnownPlanet planet, ulong? here, double? distance)
    {
        Planet = planet;
        var details = PlanetDetailsService.Get(planet.SystemAddress, planet.PlanetIndex);
        NameLabel = UniverseInfo.PlanetName(planet.SystemAddress, planet.PlanetIndex);
        FactsLabel = UniverseInfo.Compact(planet.SystemAddress);
        LocationLabel = string.Join(" · ", new[]
        {
            planet.Classification(details?.Descriptor), PlanetText.Body(planet), PlanetText.GalaxyLabel(planet.Galaxy), planet.Source,
        }.Where(s => s.Length > 0));
        NotesLabel = string.Join(" · ", new[]
        {
            PlanetText.DetailsSummary(details, PlanetWaterService.Get(planet.SystemAddress, planet.PlanetIndex)),
            PlanetText.NoteSummary(SystemNotesService.Get(planet.SystemAddress)),
        }.Where(s => s.Length > 0));
        DistanceLabel = PlanetText.Distance(planet, here, distance);
        DiscoveredLabel = $"{(planet.DiscoveredHere ? "You" : planet.Discoverer)} · {PlanetText.Date(planet.DiscoveredUnixSeconds)}";
        Swatch = new SolidColorBrush(PlanetDrawingHelper.ParseColour(NmsPlanets.TypeColour(planet.BiomeIndex)));
    }

    /// <summary>The planet itself, for opening its page.</summary>
    public KnownPlanet Planet { get; }

    public string NameLabel { get; }
    public string FactsLabel { get; }
    public string LocationLabel { get; }
    public string NotesLabel { get; }
    public bool HasNotes => NotesLabel.Length > 0;
    public string DistanceLabel { get; }
    public string DiscoveredLabel { get; }
    public string PortalCode => Planet.PortalCode;
    public Brush Swatch { get; }
    public bool HasRings => Planet.HasRings;
}
