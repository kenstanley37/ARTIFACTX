using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.ViewModels.ExploreViewModels;

/// <summary>A list of fish under a heading in the planet page's fish panel.</summary>
public sealed record FishGroup(string Heading, string Names, string Tooltip);

/// <summary>
/// The fish panel on a planet's page: how many of the fish that live on planets of
/// this type you've caught, the ones found on any planet with water, what's still to
/// catch, and what you've caught on this planet (with where, when ArtifactX saw it).
/// Shown only where there's water.
/// </summary>
public sealed class PlanetFishViewModel
{
    private const string Recorded = "Recorded by ArtifactX while it followed your game. Catches from before, or while it was closed, aren't known - the save doesn't record where a fish was caught.";

    private PlanetFishViewModel() { }

    public bool HasSamePresentation(PlanetFishViewModel other) =>
        (HasProgress, ProgressLabel, ProgressDone, ProgressTotal, CaughtHereLabel, Badge, BadgeTooltip)
        == (other.HasProgress, other.ProgressLabel, other.ProgressDone, other.ProgressTotal,
            other.CaughtHereLabel, other.Badge, other.BadgeTooltip)
        && Notes.SequenceEqual(other.Notes) && Groups.SequenceEqual(other.Groups);

    public bool HasProgress { get; private init; }
    public string ProgressLabel { get; private init; } = "";
    public int ProgressDone { get; private init; }
    public int ProgressTotal { get; private init; }
    public string ProgressCount => $"{ProgressDone} / {ProgressTotal} caught";
    public double ProgressValue => ProgressDone;
    public double ProgressMaximum => ProgressTotal;

    /// <summary>Lines above the lists ("Plus 12 fish found on any planet with water...").</summary>
    public IReadOnlyList<string> Notes { get; private init; } = [];
    public string CaughtHereLabel { get; private init; } = "";
    public IReadOnlyList<FishGroup> Groups { get; private init; } = [];

    /// <summary>The collapsed rail's badge and tooltip.</summary>
    public string Badge { get; private init; } = "";
    public string BadgeTooltip { get; private init; } = "";

    /// <summary>Null where the planet has no water (or the catalog has no fish).</summary>
    public static PlanetFishViewModel? For(KnownPlanet planet, IReadOnlyList<FishInfo> fish,
        IReadOnlyDictionary<string, (uint Count, float Largest)> caught, Func<string, string> fishName)
    {
        if (fish.Count == 0) return null;
        var caughtHere = FishCatchService.CaughtOn(planet.SystemAddress, planet.PlanetIndex);
        bool? water = PlanetWaterService.Get(planet.SystemAddress, planet.PlanetIndex);
        if (!(water == true || caughtHere.Count > 0 || (water is null && NmsPlanets.AlwaysHasWater(planet.BiomeIndex)))) return null;

        var own = fish.Where(f => f.LivesOn(planet.BiomeIndex) && !f.Biomes.Contains(FishInfo.AnyPlanet)).ToList();
        var anywhere = fish.Where(f => f.Biomes.Contains(FishInfo.AnyPlanet)).ToList();
        int Caught(List<FishInfo> list) => list.Count(f => caught.ContainsKey(f.Id));
        int totalHere = caughtHere.Values.Sum();

        var notes = new List<string>();
        if (own.Count > 0 && Caught(own) >= own.Count)
            notes.Add($"All {own.Count} {planet.TypeName} fish caught (on any {planet.TypeName} planet)");
        notes.Add(Caught(anywhere) >= anywhere.Count
            ? $"All {anywhere.Count} fish found on any planet with water caught"
            : $"Plus {anywhere.Count} fish found on any planet with water - you've caught {Caught(anywhere)}.");

        var groups = new List<FishGroup>();
        var missingOwn = own.Where(f => !caught.ContainsKey(f.Id)).Select(f => fishName(f.Id)).Order().ToList();
        var missingAny = anywhere.Where(f => !caught.ContainsKey(f.Id)).Select(f => fishName(f.Id)).Order().ToList();
        if (missingOwn.Count > 0)
            groups.Add(new($"STILL TO CATCH - ONLY ON {planet.TypeName.ToUpperInvariant()} PLANETS ({missingOwn.Count})", string.Join(", ", missingOwn), ""));
        if (missingAny.Count > 0)
            groups.Add(new($"STILL TO CATCH - ANY PLANET WITH WATER ({missingAny.Count})", string.Join(", ", missingAny), ""));
        if (caughtHere.Count > 0)
        {
            var spots = FishCatchService.SpotsOn(planet.SystemAddress, planet.PlanetIndex);
            string Name(KeyValuePair<string, int> c) => c.Value > 1 ? $"{fishName(c.Key)} ×{c.Value}" : fishName(c.Key);
            var ordered = caughtHere.OrderByDescending(c => c.Value).ThenBy(c => fishName(c.Key)).ToList();
            var located = ordered.Where(c => spots.ContainsKey(c.Key)).Select(c =>
            {
                var shown = spots[c.Key].Select(a => NmsPlanetCoordinates.Format(a.Latitude, a.Longitude)).Distinct().ToList();
                string where = string.Join("  ·  ", shown.Take(3)) + (shown.Count > 3 ? $"  (+{shown.Count - 3} more)" : "");
                return $"{Name(c)}  -  at {where}";
            }).ToList();
            var unlocated = ordered.Where(c => !spots.ContainsKey(c.Key)).Select(Name).ToList();
            string heading = $"CAUGHT ON THIS PLANET ({caughtHere.Count} KIND{(caughtHere.Count == 1 ? "" : "S")})";
            if (located.Count > 0)
                groups.Add(new(heading, string.Join(Environment.NewLine, located),
                    Recorded + " Coordinates are where you were when the game next saved - the numbers your ship's dashboard and scanner show."));
            if (unlocated.Count > 0)
                groups.Add(new(located.Count > 0 ? "CAUGHT HERE BEFORE LOCATIONS WERE KEPT" : heading, string.Join(", ", unlocated), Recorded));
        }
        if (missingOwn.Count + missingAny.Count == 0 && caughtHere.Count == 0)
            notes.Add("You've caught every fish you can find here.");

        (string badge, string tip) = totalHere > 0 ? ($"{totalHere}", $"{totalHere} fish caught on this planet")
            : own.Count > 0 ? ($"{Caught(own)}/{own.Count}", $"{Caught(own)} of {own.Count} {planet.TypeName} fish caught")
            : ($"{fish.Count}", "Fish");

        return new PlanetFishViewModel
        {
            HasProgress = own.Count > 0 && Caught(own) < own.Count,
            ProgressLabel = $"{planet.TypeName} fish - on any {planet.TypeName} planet",
            ProgressDone = Caught(own),
            ProgressTotal = Math.Max(1, own.Count),
            Notes = notes,
            CaughtHereLabel = totalHere == 0 ? "Caught here: none yet"
                : $"Caught here: {totalHere:N0} fish · {caughtHere.Count} kind{(caughtHere.Count == 1 ? "" : "s")}",
            Groups = groups,
            Badge = badge,
            BadgeTooltip = tip,
        };
    }
}
