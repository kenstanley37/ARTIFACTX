namespace ArtifactX.Core.Recipes;

/// <summary>Generated planet resources plus both gas options extracted for its
/// biome. Names here describe the inputs; substance IDs come from game data.</summary>
public sealed record HotspotResources(string CommonMineral, string BiomeMineral, string RareMineral,
    string FirstGas, string SecondGas)
{
    /// <summary>Resource branch of native 0x1412F5EF0, inspected 2026-11-01.
    /// The caller must already have resolved the actual hotspot type at the
    /// extractor. This selector does not infer that type from a planet biome.</summary>
    public string ForType(string type) => type switch
    {
        "Mineral1" => CommonMineral,
        "Mineral2" => BiomeMineral,
        "Mineral3" => RareMineral,
        "Gas1" => FirstGas,
        "Gas2" => SecondGas,
        _ => ""
    };
}
