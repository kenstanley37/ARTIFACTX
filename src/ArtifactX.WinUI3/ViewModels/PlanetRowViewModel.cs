using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml.Media;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One result in the Planet Finder - a KnownPlanet (NmsPlanets) shaped for display.</summary>
public sealed class PlanetRowViewModel
{
    /// <param name="facts">The finder's facts for the planet: their summary shows when nothing about it is recorded.</param>
    /// <param name="pinned">Pinned in the Planet Finder (PlanetPinService): shown first with a filled pin.</param>
    public PlanetRowViewModel(KnownPlanet planet, ulong? here, double? distance, PlanetFinderFacts? facts = null, bool pinned = false)
    {
        Planet = planet;
        IsPinned = pinned;
        var details = PlanetDetailsService.Get(planet.SystemAddress, planet.PlanetIndex);
        var desc = PlanetTypesService.Descriptor(planet.SystemAddress, planet.PlanetIndex);
        NameLabel = UniverseInfo.PlanetName(planet.SystemAddress, planet.PlanetIndex);
        FactsLabel = UniverseInfo.Compact(planet.SystemAddress);
        bool isMoon = BodyKindsService.IsMoon(planet.SystemAddress, planet.PlanetIndex);
        // The system gets its own line: it's what the player looks for on the galaxy map (owner, 2026-11-14).
        string star = PlanetText.StarOf(planet.SystemAddress);
        SystemLabel = $"{UniverseInfo.SystemName(planet.SystemAddress)} · {star} star";
        StarSwatch = new SolidColorBrush(PlanetDrawingHelper.ParseColour(Helpers.Location.StarColours.Hex(star)));
        LocationLabel = string.Join(" · ", new[]
        {
            planet.Classification(desc, isMoon: isMoon), isMoon ? "moon" : $"planet {planet.PlanetIndex}",
            PlanetText.GalaxyLabel(planet.Galaxy), planet.Source,
        }.Where(s => s.Length > 0));
        NotesLabel = string.Join(" · ", new[]
        {
            PlanetText.DetailsSummary(details, PlanetWaterService.Get(planet.SystemAddress, planet.PlanetIndex)),
            PlanetText.NoteSummary(SystemNotesService.Get(planet.SystemAddress)),
        }.Where(s => s.Length > 0));
        if (NotesLabel.Length == 0 && facts is not null) NotesLabel = facts.Summary;
        DistanceLabel = PlanetText.Distance(planet, here, distance);
        DiscoveredLabel = planet.Predicted ? "Not in your saves: worked out the way the game does"
            : $"{(planet.DiscoveredHere ? "You" : planet.Discoverer)} · {PlanetText.Date(planet.DiscoveredUnixSeconds)}";
        Swatch = new SolidColorBrush(PlanetDrawingHelper.ParseColour(NmsPlanets.TypeColour(planet.BiomeIndex)));
    }

    /// <summary>The planet itself, for opening its page.</summary>
    public KnownPlanet Planet { get; }

    public string NameLabel { get; }
    /// <summary>"Inzadga-Rigpr · Yellow star": the system to find on the galaxy map.</summary>
    public string SystemLabel { get; }
    public Brush StarSwatch { get; }
    public string FactsLabel { get; }
    public string LocationLabel { get; }
    public string NotesLabel { get; }
    public bool HasNotes => NotesLabel.Length > 0;
    public string DistanceLabel { get; }
    public string DiscoveredLabel { get; }
    public string PortalCode => Planet.PortalCode;
    public Brush Swatch { get; }
    public bool HasRings => Planet.HasRings;

    public bool IsPinned { get; }
    public string PinGlyph => IsPinned ? "\uE77A" : "\uE718";
    public string PinName => IsPinned ? $"Unpin {NameLabel}" : $"Pin {NameLabel} to the top of the list";
}
