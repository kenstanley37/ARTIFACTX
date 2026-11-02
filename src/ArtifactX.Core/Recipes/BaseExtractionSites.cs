using System.Globalization;
using System.Numerics;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using Newtonsoft.Json.Linq;

namespace ArtifactX.Core.Recipes;

public sealed record ExtractionWorldTables(BiomeTables Biomes, PlanetProfileTables Profiles, ExtractionTables Extraction);
public sealed record BaseExtractionSite(string BaseName, string ItemId, string Kind, int Extractors);

/// <summary>Read-only extraction-site discovery, not an available-stock estimate.
/// The saved HomePlanetBase anchor/forward and local object positions were
/// inspected 2026-11-02. Radial up, orthogonalized forward and their right axis
/// place all 20 reference Paraffinium extractors within the native-selected
/// Mineral2 hotspot. A second read-only save check identifies all 25 gas
/// extractors at the owner's Oxygen Farm as Oxygen, matching the depot image.
/// Unsupported worlds, coordinates and distant hotspots are
/// excluded; power, output rates and accumulated stock are not inferred.</summary>
public static class BaseExtractionSites
{
    public static IReadOnlyList<BaseExtractionSite> Read(JArray? bases, ExtractionWorldTables tables)
    {
        var result = new List<BaseExtractionSite>();
        if (bases is null) return result;
        var definitions = tables.Extraction.Networks.Where(n => n.Rate > 0 && n.HotspotKind is "Mineral" or "Gas")
            .ToDictionary(n => n.PartId, StringComparer.OrdinalIgnoreCase);
        var categories = tables.Extraction.Hotspots.Select(h => h.Parameters).ToArray();
        var worlds = new Dictionary<ulong, (float Radius, HotspotResources Resources)?>();
        foreach (var (entry, index) in bases.OfType<JObject>().Select((b, i) => (b, i)))
        {
            if (entry["peI"]?["DPp"]?.Value<string>() != "HomePlanetBase" || entry["@ZJ"] is not JArray objects) continue;
            var producers = objects.OfType<JObject>().Where(o => definitions.ContainsKey(o["r<7"]?.Value<string>()?.TrimStart('^') ?? "")).ToArray();
            if (producers.Length == 0 || NmsSpaceStationPaths.ParsePackedAddress(entry["oZw"]) is not ulong address
                || !Vector(entry["wMC"], out var anchor) || !Vector(entry["oHw"], out var forward) || anchor.LengthSquared() <= 0) continue;
            if (!worlds.TryGetValue(address, out var world)) worlds[address] = world = World(address, tables);
            if (world is not { } generated) continue;
            var up = Vector3.Normalize(anchor);
            var right = Vector3.Cross(up, forward);
            if (right.LengthSquared() < 0.000001f) continue;
            right = Vector3.Normalize(right);
            forward = Vector3.Cross(right, up);
            var sources = new Dictionary<(string Id, string Kind), int>();
            foreach (var producer in producers)
            {
                if (!Vector(producer["wMC"], out var local)) continue;
                var definition = definitions[producer["r<7"]!.Value<string>()!.TrimStart('^')];
                var position = anchor + right * local.X + up * local.Y + forward * local.Z;
                var selected = HotspotNeighborhood.Closest(address, position, generated.Radius,
                    definition.HotspotKind == "Mineral" ? 14 : 48, tables.Extraction.Rules, categories);
                if (selected is not { } match || match.Distance > match.Candidate.Attributes.Range) continue;
                string id = tables.Extraction.ResourceForFlag(generated.Resources, match.Candidate.Attributes.Flag);
                if (id.Length == 0) continue;
                var key = (id, definition.HotspotKind);
                sources[key] = sources.GetValueOrDefault(key) + 1;
            }
            string name = entry["NKm"]?.Value<string>()?.Trim() is { Length: > 0 } savedName ? savedName : $"Base {index + 1}";
            result.AddRange(sources.Select(s => new BaseExtractionSite(name, s.Key.Id, s.Key.Kind, s.Value)));
        }
        return result;
    }

    private static (float Radius, HotspotResources Resources)? World(ulong address, ExtractionWorldTables tables)
    {
        int planet = (int)((address >> 52) & 15), galaxy = NmsSpaceStationPaths.GalaxyNumber(address) - 1;
        if (planet == 0) return null;
        ulong code = ulong.Parse(NmsSpaceStationPaths.PortalAddress(address, planet), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var types = PlanetGenerator.Generate(code, galaxy, tables.Biomes);
        var bodies = SystemGenerator.Bodies(code, galaxy);
        int index = planet - 1;
        if (index >= types.Length || index >= bodies.PlanetSeeds.Length || PlanetBuildingSites.Radius(types[index].BodyClass) is not float radius) return null;
        var system = SystemGenerator.Attributes(code, galaxy);
        var profile = PlanetProfileGenerator.Generate(bodies.PlanetSeeds[index], index, types[index], (int)system.Star,
            PlanetGenerator.GalaxyType(galaxy), system.GasGiantPlanet, tables.Profiles with { Objects = null }, system.Uncharted, system.Abandoned);
        return (radius, tables.Extraction.ResourcesFor(profile, types[index].Biome));
    }

    private static bool Vector(JToken? token, out Vector3 vector)
    {
        vector = default;
        if (token is not JArray { Count: >= 3 } values) return false;
        float[] numbers = values.Take(3).Select(v => v.Value<float>()).ToArray();
        if (numbers.Any(n => !float.IsFinite(n))) return false;
        vector = new(numbers[0], numbers[1], numbers[2]);
        return true;
    }
}
