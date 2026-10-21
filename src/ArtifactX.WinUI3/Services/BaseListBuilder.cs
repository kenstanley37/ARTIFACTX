using ArtifactX.Core.NmsModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.Services;

/// <summary>The sections of the Bases page, in the order they're shown.</summary>
public enum BaseKind { Freighter, Planet, Station, Settlement, Placed }

/// <summary>One base, settlement or loose placed object as the Bases page lists it -
/// plain data, built off the UI thread.</summary>
public sealed record BaseListEntry(
    BaseKind Kind,
    string Name,
    string Detail,
    string Facts,
    string SwatchHex,
    string PortalCode,
    string DistanceLabel,
    string AreaLabel,
    bool AreaRecorded,
    int Parts,
    string PartsLabel,
    int PartsLevel,
    JObject? Base,
    JObject? LooseObject,
    double SortDistance,
    int Galaxy,
    ulong Address,
    string ScreenshotName,
    string? PlacedItemId);

public sealed record BaseList(int PartsTotal, int PartsLevel, string PartsBreakdown, IReadOnlyList<BaseListEntry> Entries);

/// <summary>
/// Reads every base, settlement and loose placed object from a save snapshot for the
/// Bases page. Bases are the save's base array (peI.DPp says which kind), loose
/// objects are placed things outside any base (save beacons, refiners...), and
/// settlements are the settlements the player owns.
/// </summary>
public static class BaseListBuilder
{
    public static string KindLabel(BaseKind kind) => kind switch
    {
        BaseKind.Freighter => "Freighter base",
        BaseKind.Planet => "Planet bases",
        BaseKind.Station => "Claimed space stations",
        BaseKind.Settlement => "Settlements",
        _ => "Placed objects",
    };

    /// <summary>Reads catalog names (CatalogService.TryGet), so warm the cache for the
    /// loose objects' ids first.</summary>
    public static BaseList Build(PlanetSnapshot snapshot, ulong? here)
    {
        var entries = new List<BaseListEntry>();
        var counts = NmsBuildCounts.PerBase(snapshot.Bases).ToDictionary(c => c.Base, c => c.Parts);
        int total = NmsBuildCounts.Total(snapshot.Bases, snapshot.LooseObjects);
        var planets = NmsPlanets.ReadAll(snapshot.Discoveries)
            .GroupBy(p => p.Address).ToDictionary(g => g.Key, g => g.First());
        var bases = snapshot.Bases?.OfType<JObject>().ToList() ?? new();
        var loose = snapshot.LooseObjects?.OfType<JObject>().ToList() ?? new();

        string PlanetType(ulong address, string ringSuffix = "")
        {
            if (!planets.TryGetValue(address, out var planet)) return "Planet";
            return planet.Classification(PlanetDetailsService.Get(planet.SystemAddress, planet.PlanetIndex)?.Descriptor, ringSuffix: ringSuffix);
        }
        string Swatch(ulong address, string fallback) =>
            planets.TryGetValue(address, out var planet) ? NmsPlanets.TypeColour(planet.BiomeIndex) : fallback;
        double? Distance(ulong address) =>
            here is ulong h ? NmsPlanets.DistanceLightYears(h, S.ToSystemAddress(address)) : null;

        foreach (var b in bases.Where(b => BaseType(b) == "FreighterBase"))
        {
            ulong address = S.ParsePackedAddress(b["oZw"]) ?? here ?? 0;
            int planetIndex = PlanetIndex(address);
            int galaxy = address != 0 ? S.GalaxyNumber(address) : 1;
            string name = b["NKm"]?.Value<string>() is { Length: > 0 } n ? n : "Freighter";
            double? distance = address != 0 ? Distance(address) : null;
            int parts = counts.GetValueOrDefault(b);
            entries.Add(new BaseListEntry(BaseKind.Freighter, name,
                Detail: address != 0 ? $"Freighter · {UniverseInfo.LocationWithRegion(address, galaxy)}" : "Freighter",
                Facts: address != 0 ? UniverseInfo.Compact(address) : "",
                SwatchHex: "#7E57C2",
                PortalCode: address != 0 ? S.PortalAddress(address, planetIndex > 0 ? planetIndex : 1) : "",
                DistanceLabel: address != 0 ? DistanceText(here, address, distance) : "",
                AreaLabel: "Everything built inside your freighter.",
                AreaRecorded: true,
                Parts: parts, PartsLabel: NmsBuildCounts.UploadLabel(parts),
                PartsLevel: NmsBuildCounts.UploadLevel(parts),
                Base: b, LooseObject: null,
                SortDistance: distance ?? double.MaxValue, Galaxy: galaxy, Address: address,
                ScreenshotName: name, PlacedItemId: null));
        }

        foreach (var b in bases.Where(b => BaseType(b) == "HomePlanetBase"))
        {
            if (S.ParsePackedAddress(b["oZw"]) is not ulong address) continue;
            int planetIndex = PlanetIndex(address);
            int galaxy = S.GalaxyNumber(address);
            string name = b["NKm"]?.Value<string>() is { Length: > 0 } n ? n : "Unnamed base";
            string code = S.PortalAddress(address, planetIndex);
            double? distance = Distance(address);
            int parts = counts.GetValueOrDefault(b);
            string area = AreaText(address, planetIndex, galaxy, code);
            entries.Add(new BaseListEntry(BaseKind.Planet, name,
                Detail: $"{UniverseInfo.PlanetName(address, planetIndex)} ({PlanetType(address, " · rings")}){CoordsText(b)} · {UniverseInfo.LocationWithRegion(address, galaxy)}",
                Facts: UniverseInfo.Compact(address),
                SwatchHex: Swatch(address, "#777777"),
                PortalCode: code,
                DistanceLabel: DistanceText(here, address, distance),
                AreaLabel: area.Length > 0 ? area : "Nothing recorded about this planet yet - add its resources, sentinels and notes on the Planets page.",
                AreaRecorded: area.Length > 0,
                Parts: parts, PartsLabel: NmsBuildCounts.UploadLabel(parts),
                PartsLevel: NmsBuildCounts.UploadLevel(parts),
                Base: b, LooseObject: null,
                SortDistance: distance ?? double.MaxValue, Galaxy: galaxy, Address: address,
                ScreenshotName: name, PlacedItemId: null));
        }

        foreach (var b in bases)
        {
            string type = BaseType(b);
            if (type is "HomePlanetBase" or "FreighterBase" || type.StartsWith("External", StringComparison.Ordinal)) continue;
            if (S.ParsePackedAddress(b["oZw"]) is not ulong address) continue;
            int galaxy = S.GalaxyNumber(address);
            string name = b["NKm"]?.Value<string>() is { Length: > 0 } n ? n : "Space station";
            double? distance = Distance(address);
            int parts = counts.GetValueOrDefault(b);
            entries.Add(new BaseListEntry(BaseKind.Station, name,
                Detail: $"Space station · {UniverseInfo.LocationWithRegion(address, galaxy)}",
                Facts: UniverseInfo.Compact(address),
                SwatchHex: "#FF9800",
                PortalCode: S.PortalAddress(address, 1),
                DistanceLabel: DistanceText(here, address, distance),
                AreaLabel: $"Claimed station in {UniverseInfo.SystemName(address)} ({UniverseInfo.RaceLabel(address)}).",
                AreaRecorded: true,
                Parts: parts, PartsLabel: $"{parts:N0} parts placed",
                PartsLevel: NmsBuildCounts.UploadLevel(parts),
                Base: b, LooseObject: null,
                SortDistance: distance ?? double.MaxValue, Galaxy: galaxy, Address: address,
                ScreenshotName: name, PlacedItemId: null));
        }

        // A settlement isn't a base entry: its card gathers the parts on its planet -
        // loose objects there and the parts of bases there - so they can be listed.
        var settlementAddresses = new HashSet<ulong>();
        string localOnlineId = snapshot.LocalOnlineId
            ?? SaveSessionManager.GetValue(NmsSettlementPaths.LocalPlayerOnlineIdPath)?.Value<string>()
            ?? "";
        foreach (var s in snapshot.Settlements?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
        {
            string name = s["NKm"]?.Value<string>() ?? "";
            if (name.Length == 0) continue;
            string owner = s["3?K"]?["K7E"]?.Value<string>() ?? "";
            if (localOnlineId.Length > 0 && owner != localOnlineId) continue;
            if (S.ParsePackedAddress(s["yhJ"]) is not ulong address || address == 0) continue;
            settlementAddresses.Add(address);

            int planetIndex = PlanetIndex(address);
            int galaxy = S.GalaxyNumber(address);
            string code = S.PortalAddress(address, planetIndex > 0 ? planetIndex : 1);
            double? distance = Distance(address);
            int population = s["x3<"]?.Value<int>() ?? 0;

            var parts = new JArray();
            foreach (var o in loose.Where(o => S.ParsePackedAddress(o["oZw"]) == address))
                parts.Add(new JObject { ["r<7"] = o["r<7"] });
            foreach (var b in bases.Where(b => S.ParsePackedAddress(b["oZw"]) == address))
                foreach (var p in (b["@ZJ"] as JArray)?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
                    if (p["r<7"]?.Value<string>() != NmsBuildCounts.BaseComputerId) parts.Add(p);
            var gathered = new JObject { ["@ZJ"] = parts, ["oZw"] = s["yhJ"] };

            string area = AreaText(address, planetIndex, galaxy, code);
            entries.Add(new BaseListEntry(BaseKind.Settlement, name,
                Detail: $"Settlement · {UniverseInfo.PlanetName(address, planetIndex)} ({PlanetType(address, " · rings")}) · {UniverseInfo.LocationWithRegion(address, galaxy)}",
                Facts: UniverseInfo.Compact(address),
                SwatchHex: Swatch(address, "#00BCD4"),
                PortalCode: code,
                DistanceLabel: DistanceText(here, address, distance),
                AreaLabel: area.Length > 0 ? area : $"Settlement on {UniverseInfo.PlanetName(address, planetIndex)} in {UniverseInfo.SystemName(address)} ({UniverseInfo.RaceLabel(address)}).",
                AreaRecorded: true,
                Parts: parts.Count, PartsLabel: parts.Count > 0 ? $"{parts.Count:N0} parts placed · {population:N0} citizens" : $"{population:N0} citizens",
                PartsLevel: -1,
                Base: gathered, LooseObject: null,
                SortDistance: distance ?? double.MaxValue, Galaxy: galaxy, Address: address,
                ScreenshotName: name, PlacedItemId: null));
        }

        foreach (var o in loose)
        {
            if (S.ParsePackedAddress(o["oZw"]) is not ulong address) continue;
            if (settlementAddresses.Contains(address)) continue; // listed with the settlement
            string id = o["r<7"]?.Value<string>() ?? "";
            int planetIndex = PlanetIndex(address);
            int galaxy = S.GalaxyNumber(address);
            string code = S.PortalAddress(address, planetIndex > 0 ? planetIndex : 1);
            double? distance = Distance(address);
            long placedAt = o["b1:"]?.Value<long>() ?? 0;
            string area = AreaText(address, planetIndex, galaxy, code);
            entries.Add(new BaseListEntry(BaseKind.Placed, BasePartNames.Name(id),
                Detail: $"Placed on {UniverseInfo.PlanetName(address, planetIndex)} ({PlanetType(address)}){CoordsText(o)} · {UniverseInfo.LocationWithRegion(address, galaxy)}",
                Facts: placedAt > 0 ? $"Placed {DateTimeOffset.FromUnixTimeSeconds(placedAt).LocalDateTime:d} · outside a base" : "Outside a base",
                SwatchHex: Swatch(address, "#00BCD4"),
                PortalCode: code,
                DistanceLabel: DistanceText(here, address, distance),
                AreaLabel: area.Length > 0 ? area : $"Placed on {UniverseInfo.PlanetName(address, planetIndex)}.",
                AreaRecorded: true,
                Parts: 1, PartsLabel: "1 part",
                PartsLevel: -1,
                Base: null, LooseObject: o,
                SortDistance: distance ?? double.MaxValue, Galaxy: galaxy, Address: address,
                ScreenshotName: "", PlacedItemId: id));
        }

        return new BaseList(total, NmsBuildCounts.GlobalLevel(total),
            NmsBuildCounts.Breakdown(snapshot.Bases, snapshot.LooseObjects), entries);
    }

    private static string BaseType(JObject b) => b["peI"]?["DPp"]?.Value<string>() ?? "";

    private static int PlanetIndex(ulong address) => (int)((address >> 52) & 0xF);

    private static string CoordsText(JObject placed) =>
        NmsPlanetCoordinates.OfBase(placed) is { } at ? $" at {NmsPlanetCoordinates.Format(at.Latitude, at.Longitude)}" : "";

    /// <summary>What's recorded about the planet and area (resources, sentinels, water,
    /// fish caught, finds, ships seen) - you can teleport to a base, so this is often
    /// more useful than its glyphs. Empty when nothing is recorded.</summary>
    private static string AreaText(ulong address, int planet, int galaxy, string code)
    {
        ulong system = S.ToSystemAddress(address);
        var parts = new List<string>();
        var d = PlanetDetailsService.Get(system, planet);
        if (d is not null)
        {
            if (d.Resources.Count > 0) parts.Add($"Resources: {string.Join(", ", d.Resources)}");
            if (d.Sentinels != "") parts.Add($"{d.Sentinels} sentinels");
            if (d.Weather != "") parts.Add(d.Weather);
            if (d.Flora != "") parts.Add($"Flora: {d.Flora}");
            if (d.Fauna != "") parts.Add($"Fauna: {d.Fauna}");
        }
        if (PlanetWaterService.Get(system, planet) == true) parts.Add("Water");
        int fish = FishCatchService.CaughtOn(system, planet).Count;
        if (fish > 0) parts.Add(fish == 1 ? "1 kind of fish caught here" : $"{fish} kinds of fish caught here");
        int finds = FindsService.All.Count(f => f.PortalCode == code && f.Galaxy == galaxy);
        if (finds > 0) parts.Add(finds == 1 ? "1 find" : $"{finds} finds");
        if (SystemNotesService.Get(system)?.Ships is { Count: > 0 } ships) parts.Add($"Ships seen: {string.Join(", ", ships)}");
        if (d is not null) parts.AddRange(d.Notes);
        return string.Join(" · ", parts);
    }

    private static string DistanceText(ulong? here, ulong address, double? distance)
    {
        string core = $"{NmsPlanets.DistanceToCoreLightYears(address):N0} ly from the galaxy core";
        string fromYou = (here is ulong h && S.ToSystemAddress(address) == h) ? "In the system you're in"
            : distance switch
            {
                null => "In a different galaxy from you",
                < 1 => JumpPlanner.WithJumps("Under 400 ly from you", 1),
                double ly => JumpPlanner.WithJumps($"About {ly:N0} ly from you", ly),
            };
        return $"{fromYou} · {core}";
    }
}
