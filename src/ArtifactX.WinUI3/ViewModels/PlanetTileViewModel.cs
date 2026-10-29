using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels.ExploreViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One planet's card on Current System: what the save knows (a label / value table -
/// 2026-10-13: a stack of small dim lines was hard to read), its fish where there's water,
/// a line of what you recorded, and its glyphs. Weather, sentinels, fauna and resources come from
/// the game's own generator (PlanetTypesService), for found and unfound bodies alike. "You found" and "Visited" update in place
/// as you play, so the card doesn't redraw (no blink).</summary>
public sealed partial class PlanetTileViewModel : ObservableObject
{
    /// <summary>Static card content only; live counts and coordinates update on the retained row.</summary>
    public bool HasSamePresentation(PlanetTileViewModel other) =>
        Planet == other.Planet && IsHere == other.IsHere
        && new[] { Eyebrow, Name, OriginalName, TypeText, BasesLabel, Bases, FoundBy, Weather,
            Sentinels, Flora, Fauna, Resources, DetailsSummary }.SequenceEqual(
            new[] { other.Eyebrow, other.Name, other.OriginalName, other.TypeText, other.BasesLabel,
                other.Bases, other.FoundBy, other.Weather, other.Sentinels, other.Flora, other.Fauna,
                other.Resources, other.DetailsSummary })
        && (Fish is null ? other.Fish is null : other.Fish is not null && Fish.HasSamePresentation(other.Fish));

    public PlanetTileViewModel(KnownPlanet planet, bool here, IReadOnlyList<string> baseNames, PlanetFishViewModel? fish,
        IReadOnlyDictionary<string, string> itemNames)
    {
        Planet = planet;
        IsHere = here;
        bool isMoon = BodyKindsService.IsMoon(planet.SystemAddress, planet.PlanetIndex);
        Eyebrow = isMoon ? "MOON" : $"PLANET {planet.PlanetIndex}";
        Name = UniverseInfo.PlanetName(planet.SystemAddress, planet.PlanetIndex);
        string generated = UniverseInfo.GeneratedPlanetName(planet.SystemAddress, planet.PlanetIndex);
        // "Originally Irskya XIX" (2026-10-18): guides and other players may still use the game's original name.
        OriginalName = string.Equals(Name, generated, StringComparison.OrdinalIgnoreCase) ? "" : $"Originally {generated}";
        var details = PlanetDetailsService.Get(planet.SystemAddress, planet.PlanetIndex);
        TypeText = planet.Classification(details?.Descriptor, ringSuffix: " · rings");
        BasesLabel = baseNames.Count == 1 ? "Your base" : "Your bases";
        Bases = string.Join(", ", baseNames);
        FoundBy = planet.Predicted ? "Not discovered yet" : planet.DiscoveredHere ? "You" : planet.Discoverer;
        Fish = fish;
        if (PlanetTypesService.Profile(planet.SystemAddress, planet.PlanetIndex) is { } profile)
        {
            Weather = PlanetText.Weather(profile);
            Sentinels = PlanetText.Sentinels(profile);
            Flora = details?.Flora is { Length: > 0 } fl ? fl : PlanetText.Flora(profile);
            Fauna = details?.Fauna is { Length: > 0 } fa ? fa : PlanetText.Fauna(profile);
            Resources = string.Join(", ", PlanetText.Resources(profile, itemNames));
        }
        string summary = PlanetText.CardSummary(details);
        DetailsSummary = summary.Length > 0 ? summary : "None yet - open the planet to add weather, sentinels, resources and notes.";
        HasDetails = summary.Length > 0;
    }

    public KnownPlanet Planet { get; }
    public bool IsHere { get; }
    public string Eyebrow { get; }
    public string Name { get; }
    public string OriginalName { get; }
    public bool HasOriginalName => OriginalName.Length > 0;
    public string TypeText { get; }
    public string BasesLabel { get; }
    public string Bases { get; }
    public bool HasBases => Bases.Length > 0;
    public string FoundBy { get; }
    public string Weather { get; } = "";
    public string Sentinels { get; } = "";
    public string Flora { get; } = "";
    public string Fauna { get; } = "";
    public string Resources { get; } = "";
    public bool HasProfile => Weather.Length > 0;
    public bool HasFlora => Flora.Length > 0;
    public bool HasFauna => Fauna.Length > 0;
    public PlanetFishViewModel? Fish { get; }
    public bool HasFish => Fish is not null;
    public string DetailsSummary { get; }
    public bool HasDetails { get; }
    public double DetailsOpacity => HasDetails ? 1 : 0.7;
    public string PortalCode => Planet.PortalCode;

    /// <summary>Where the planet's portal stands ("+67.09, +81.40"), worked out from the game's own placement
    /// rules (PortalLocationService); empty until it's ready or when it can't be worked out.</summary>
    [ObservableProperty] public partial string Portal { get; private set; } = "";
    [ObservableProperty] public partial bool HasPortal { get; private set; }

    /// <summary>Fills <see cref="Portal"/> once the system's placement is done (off the UI thread).</summary>
    public async System.Threading.Tasks.Task LoadPortalAsync()
    {
        if (HasPortal) return;
        if (await PortalLocationService.GetAsync(Planet.Address) is { } portal)
        {
            Portal = portal.Coordinates;
            HasPortal = true;
        }
    }

    /// <summary>The nearest S-class crashed ship (Exotics included) on the planet (to you when you're here, else to the
    /// portal), with its optional save marker (CrashedShipService: a whole-planet pass, cached per planet). Other
    /// classes and types are searched on Planet Details.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasNotableShip))]
    public partial CrashedShipRowViewModel? NotableShip { get; private set; }
    public bool HasNotableShip => NotableShip is not null;
    [ObservableProperty] public partial string NotableSummary { get; private set; } = "";
    [ObservableProperty] public partial bool HasNotable { get; private set; }
    [ObservableProperty] public partial string NotableHeading { get; private set; } = "S-CLASS CRASHED SHIP";

    private (double Latitude, double Longitude)? _here;

    /// <summary>Fills <see cref="NotableShip"/> with the nearest crashed ship of class <paramref name="shipClass"/>
    /// (C=0 .. S=3; off the UI thread); leaves the row hidden when the planet can't be worked out yet.</summary>
    public async System.Threading.Tasks.Task LoadNotableAsync(int shipClass = 3)
    {
        bool fromYou = IsHere && _here is not null;
        (double Latitude, double Longitude)? from = fromYou ? _here
            : await PortalLocationService.GetAsync(Planet.Address) is { } portal ? (portal.Latitude, portal.Longitude) : null;
        if (from is not { } at || await CrashedShipService.OfClassAsync(Planet.Address, shipClass, at.Latitude, at.Longitude) is not { } found)
            return;
        char letter = "CBAS"[shipClass];
        NotableHeading = $"{letter}-CLASS CRASHED SHIP";
        NotableShip = found.Nearest.Count > 0
            ? new CrashedShipRowViewModel(found.Nearest[0], 1, IsHere, fromYou ? "away" : "from the portal")
            : null;
        string summary = found.Total switch
        {
            0 => $"No {letter}-class crashed ships on this planet.",
            1 => $"The only {letter}-class crashed ship on this planet.",
            _ => $"The nearest of {found.Total:N0} {letter}-class crashed ships on this planet.",
        };
        NotableSummary = found.Unverified ? summary + " Not yet confirmed in game for systems without a race." : summary;
        HasNotable = true;
    }

    [ObservableProperty] public partial string Discovered { get; private set; } = "";
    [ObservableProperty] public partial string Visit { get; private set; } = "-";
    [ObservableProperty] public partial string HereLabel { get; private set; } = "YOU ARE HERE";

    /// <summary>The save's latest numbers for the planet, and where you are on it.</summary>
    public void Update(PlanetDiscoveryCounts? tally, IReadOnlyDictionary<string, double> stats, PlanetSnapshot snapshot)
    {
        Discovered = PlanetText.Discovered(Planet.Address, tally);
        string visit = PlanetText.Visit(stats);
        Visit = visit.Length > 0 ? visit : "-";
        HereLabel = snapshot.Coordinates is { } at ? $"YOU ARE HERE · {NmsPlanetCoordinates.Format(at.Latitude, at.Longitude)}" : "YOU ARE HERE";
        _here = IsHere ? snapshot.Coordinates : null;
    }
}

