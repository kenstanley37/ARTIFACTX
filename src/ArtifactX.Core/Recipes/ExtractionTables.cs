using ArtifactX.Core.Universe;

namespace ArtifactX.Core.Recipes;

public sealed record HotspotDefinition(int Flag, string Type, HotspotCategory Parameters);
public sealed record HotspotBiomeGases(int Biome, string FirstGas, string SecondGas);

/// <summary>Portable extraction inputs supplied entirely by the game-data catalog.
/// The biome index and generated resource fields match native resource selection
/// inspected 2026-11-01. No saved network resource identity is assumed here.</summary>
public sealed record ExtractionTables(HotspotPoleRules Rules, HotspotDefinition[] Hotspots,
    HotspotBiomeGases[] BiomeGases, ExtractionSubstance[] Substances, ExtractionNetworkRule[] Networks)
{
    public HotspotResources ResourcesFor(PlanetProfile profile, int biome)
    {
        var gases = BiomeGases.SingleOrDefault(g => g.Biome == biome)
            ?? throw new ArgumentOutOfRangeException(nameof(biome));
        return new(profile.Common, profile.Uncommon, profile.Rare, gases.FirstGas, gases.SecondGas);
    }

    public string ResourceForFlag(HotspotResources resources, int flag) =>
        Hotspots.SingleOrDefault(h => h.Flag == flag) is { } hotspot ? resources.ForType(hotspot.Type) : "";
}
