namespace ArtifactX.Core.Universe;

/// <summary>
/// Every body's profile (resources, weather, sentinels, predicted creatures...) of one star system, straight from the
/// game's generators: <see cref="PlanetGenerator"/> for the bodies' types, <see cref="SystemGenerator"/> for the
/// system's attributes and planet seeds, then <see cref="PlanetProfileGenerator"/> per body. Pure and uncached, so the
/// desktop app's Planet Details and a search over thousands of systems (or the future mobile app) share it.
/// </summary>
public static class SystemProfiles
{
    /// <summary>The bodies' profiles in planet order (index 0 = planet 1); a body the generator can't predict is null.
    /// <paramref name="systemAddress"/> is a packed system address (galaxy, region, system index; planet bits ignored).</summary>
    public static PlanetProfile?[] Generate(ulong systemAddress, BiomeTables biomes, PlanetProfileTables tables)
    {
        var (portal, galaxy) = PortalOf(systemAddress);
        PlanetType[] types;
        try { types = PlanetGenerator.Generate(portal, galaxy, biomes); }
        catch { return []; }
        var profiles = new PlanetProfile?[types.Length];
        try
        {
            var attributes = SystemGenerator.Attributes(portal, galaxy);
            var seeds = SystemGenerator.Bodies(portal, galaxy).PlanetSeeds;
            for (int i = 0; i < types.Length && i < seeds.Length; i++)
                profiles[i] = PlanetProfileGenerator.Generate(seeds[i], i, types[i],
                    (int)attributes.Star, PlanetGenerator.GalaxyType(galaxy), attributes.GasGiantPlanet, tables,
                    attributes.Uncharted, attributes.Abandoned);
        }
        catch { /* leave the rest unknown */ }
        return profiles;
    }

    /// <summary>A packed system address as the generators take it: the portal code (system index in bits 32-43,
    /// region below) and the galaxy number.</summary>
    public static (ulong Portal, int Galaxy) PortalOf(ulong systemAddress) =>
        ((((systemAddress >> 40) & 0xFFF) << 32) | (systemAddress & 0xFFFFFFFF), (int)((systemAddress >> 32) & 0xFF));
}
