using ArtifactX.Core.Universe;

/// <summary>Exotic worlds' descriptor word lists (PlanetProfileGenerator.DescriptorWords) against words captured in
/// game (user.db planet_details, 2026-11-14): each GcBiomeSubType draws from its own list.</summary>
internal static class PlanetDescriptorChecks
{
    public static void Check()
    {
        const int weird = 7;
        // "Calcified/Petrified/Ossified" (Structure), "Fissured/Breached" (Beam), "Hexagonal/Scaly" (Hexagon),
        // "Mechanical/Metallic" (FractCube), "Bubbling" (Bubble), "Columned/Pillared" (Shards), "Cabled/Contoured"
        // (Contour), "Finned/Bladed/Shell-Strewn" (Shell), "Rattling/Spined/Skeletal" (BoneSpire),
        // "Fragmented/Fractured" (WireCell), "Fungal/Sporal/Capped" (HydroGarden).
        (int Sub, string Prefix)[] expected =
        [
            (3, "MSTRUCTBIOME"), (4, "BEAMSBIOME"), (5, "HEXAGONBIOME"), (6, "FRACTCUBEBIOME"), (7, "BUBBLEBIOME"),
            (8, "SHARDSBIOME"), (9, "CONTOURBIOME"), (10, "IRRISHELLSBIOME"), (11, "BONESPIREBIOME"),
            (12, "WIRECELLSBIOME"), (13, "HYDROGARDENBIOME"),
        ];
        foreach (var (sub, prefix) in expected)
            if (PlanetProfileGenerator.DescriptorWords(weird, sub) != (prefix, 3))
                throw new InvalidOperationException($"Exotic subtype {sub}: expected {prefix}, got {PlanetProfileGenerator.DescriptorWords(weird, sub)}");
        if (PlanetProfileGenerator.DescriptorWords(weird, 1) != ("GLITCHBIOME", 12))
            throw new InvalidOperationException("Standard exotic worlds should use the 12 glitch words");
        // Weather words (captured, 2026-11-14): a lush world with corrupted sentinels (Weird type 13) shows humid
        // words; Bubble weather (12) shows green-anomaly words; a lush swamp world's Toxic weather (4) toxic words.
        (int Biome, int Sub, int Weather, string Family)[] weather =
        [
            (0, 1, 13, "LUSH"), (0, 14, 2, "LUSH"), (0, 2, 12, "GREEN"), (0, 25, 4, "TOXIC"), (0, 25, 10, "SWAMP"),
            (5, 1, 14, "HEAT"), (5, 1, 1, "BARREN"), (2, 26, 14, "LAVA"), (14, 21, 6, "WATERWORLD"), (10, 18, 13, "BLUE"),
            (7, 8, 0, "GLITCH"),
        ];
        foreach (var (biome, sub, type, family) in weather)
            if (PlanetProfileGenerator.WeatherWordFamily(biome, sub, type) != family)
                throw new InvalidOperationException($"Weather words for biome {biome} sub {sub} type {type}: expected {family}, got {PlanetProfileGenerator.WeatherWordFamily(biome, sub, type)}");
        Console.WriteLine("Planet descriptors: exotic word lists by subtype and weather word families OK");
    }
}
