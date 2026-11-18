using ArtifactX.Core.NmsModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.Services;

/// <summary>Where the player is and what a save snapshot says about it, for Fishing: the system and planet (0 in space),
/// that planet's type when the save knows it, the save's known planets, the bases by planet address, and the catches by
/// fish id (times caught, largest).</summary>
public sealed record FishingPlace(
    ulong? System,
    int Planet,
    int? Biome,
    List<KnownPlanet> Planets,
    Dictionary<ulong, string> Bases,
    Dictionary<string, (uint Count, float Largest)> Caught)
{
    public static readonly FishingPlace None = new(null, 0, null, [], [], new(StringComparer.OrdinalIgnoreCase));
}

/// <summary>
/// The planets side of Fishing: reading where the player is from a save snapshot, and the "Fishing spot" finds (Finds
/// page) that record which fish were caught on which planet - marking a fish caught there, and the player's "no water"
/// marks (PlanetWaterService, user.db).
/// </summary>
public static class FishingPlacesService
{
    public static FishingPlace ReadPlace(PlanetSnapshot snapshot)
    {
        ulong? system = S.ReadSystemAddress(snapshot.Universe);
        int planet = snapshot.Universe?["oZw"]?["jsv"]?.Value<int>() ?? 0;
        var planets = NmsPlanets.ReadAll(snapshot.Discoveries);
        int? biome = system is ulong here && planet > 0
            ? planets.FirstOrDefault(p => p.SystemAddress == here && p.PlanetIndex == planet)?.BiomeIndex
            : null;
        // The bases by planet address (same rule as the Bases page).
        var bases = new Dictionary<ulong, string>();
        foreach (var b in (snapshot.Bases ?? new JArray()).OfType<JObject>())
        {
            if (b["peI"]?["DPp"]?.Value<string>() != "HomePlanetBase") continue;
            if (S.ParsePackedAddress(b["oZw"]) is not ulong address) continue;
            string name = b["NKm"]?.Value<string>() is { Length: > 0 } n ? n : "Unnamed base";
            bases[address] = bases.TryGetValue(address, out var other) ? $"{other}, {name}" : name;
        }
        // The save's three parallel fishing-record arrays (NmsFishingPaths).
        var caught = FishCatchService.ReadCaught(snapshot.Fishing)
            .ToDictionary(c => c.Key, c => (c.Value.Count, c.Value.Largest), StringComparer.OrdinalIgnoreCase);
        return new FishingPlace(system, planet, biome, planets, bases, caught);
    }

    /// <summary>A planet's key for the spot finds: its portal code and galaxy.</summary>
    public static string SpotKey(string portalCode, int galaxy) => $"{portalCode}|{galaxy}";

    /// <summary>Fishing-spot finds by planet. One find per planet; its ItemIds are the fish marked as caught there.</summary>
    public static Dictionary<string, Find> SpotsByPlanet() => FindsService.All
        .Where(f => f.Type == "Fishing spot" && f.PortalCode.Length == 12)
        .GroupBy(f => SpotKey(f.PortalCode, f.Galaxy))
        .ToDictionary(g => g.Key, g => g.First());

    /// <summary>Fishing spots' names by fish id, for the fish cards.</summary>
    public static Dictionary<string, List<string>> SpotNamesByFish()
    {
        var spots = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var find in FindsService.All.Where(f => f.Type == "Fishing spot"))
            foreach (var id in find.ItemIds)
            {
                if (!spots.TryGetValue(id, out var list)) spots[id] = list = new();
                list.Add(find.Title.Length > 0 ? find.Title : find.PortalCode);
            }
        return spots;
    }

    /// <summary>A spot marked for a fish on a planet these saves don't know, as a planet to list.</summary>
    public static KnownPlanet SpotPlanet(Find spot) =>
        new(NmsPortalCode.ToAddress(spot.PortalCode, spot.Galaxy), spot.Galaxy,
            int.Parse(spot.PortalCode[..1], NumberStyles.HexNumber, CultureInfo.InvariantCulture), 11, false, "", false, 0)
        { Source = "Recorded spot" };

    /// <summary>"Found it here" on a planet (key: SpotKey): marks the fish caught there, or takes the mark off. A spot
    /// that only existed for this mark goes away with it; one with notes or other fish stays. A catch proves the planet
    /// has water, whatever was marked before.</summary>
    public static void ToggleCaughtHere(string fishId, string key, string title)
    {
        string[] parts = key.Split('|');
        if (parts.Length != 2 || !int.TryParse(parts[1], out int galaxy)) return;
        string code = parts[0];

        var spot = SpotsByPlanet().GetValueOrDefault(key);
        if (spot is null)
            FindsService.Save(new Find { Type = "Fishing spot", Title = title, PortalCode = code, Galaxy = galaxy, ItemIds = new() { fishId } });
        else if (spot.ItemIds.Contains(fishId, StringComparer.OrdinalIgnoreCase))
        {
            var remaining = spot.ItemIds.Where(i => !string.Equals(i, fishId, StringComparison.OrdinalIgnoreCase)).ToList();
            if (remaining.Count == 0 && spot.Notes.Length == 0 && spot.Location.Length == 0) FindsService.Delete(spot.Id);
            else FindsService.Save(spot with { ItemIds = remaining });
        }
        else FindsService.Save(spot with { ItemIds = spot.ItemIds.Append(fishId).ToList() });

        if (SpotsByPlanet().TryGetValue(key, out var marked) && marked.ItemIds.Contains(fishId, StringComparer.OrdinalIgnoreCase))
        {
            ulong address = NmsPortalCode.ToAddress(code, galaxy);
            int planet = int.Parse(code[..1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            PlanetWaterService.Set(S.ToSystemAddress(address), planet, true);
        }
    }

    /// <summary>A planet's key for the water marks: its system address (hex) and planet index.</summary>
    public static string WaterKey(ulong system, int planet) => $"{system.ToString("X", CultureInfo.InvariantCulture)}|{planet}";

    /// <summary>Toggles a planet's "no water" mark (key: WaterKey).</summary>
    public static void ToggleNoWater(string key)
    {
        string[] parts = key.Split('|');
        if (parts.Length != 2 || !ulong.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong system)
            || !int.TryParse(parts[1], out int planet)) return;
        PlanetWaterService.Set(system, planet, PlanetWaterService.Get(system, planet) == false ? null : false);
    }
}
