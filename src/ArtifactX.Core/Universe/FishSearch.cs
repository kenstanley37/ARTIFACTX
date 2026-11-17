namespace ArtifactX.Core.Universe;

/// <summary>A body where a fish can be caught: where (packed planet address, planet number from 1) and the body.</summary>
public sealed record FishFind(ulong PlanetAddress, int PlanetIndex, PlanetType Body);

/// <summary>
/// Where a fish bites (the Ingredient Finder's fishing half beside <see cref="HarvestSearch"/>): a body of one of the
/// fish's GcFishTable biomes (16 = any), with a sea (<see cref="PlanetProfile.Water"/>; gas giants need none, the game
/// ignores depth there - FishingRules), and storms (<see cref="PlanetProfile.Storms"/> above 0) for a storm-only fish.
/// Time of day is the player's to wait for, so it doesn't narrow the search. Needs only a starting system, so the
/// mobile app can run it too.
/// </summary>
public static class FishSearch
{
    public const int AnyPlanet = 16, GasGiant = 15;

    /// <summary>The bodies of <paramref name="systemAddress"/> where a fish of <paramref name="biomes"/> bites, from the
    /// system's bodies and profiles already generated.</summary>
    public static IEnumerable<FishFind> On(ulong systemAddress, PlanetType[] types, PlanetProfile?[] profiles, IReadOnlyCollection<int> biomes, bool needsStorm)
    {
        ulong system = systemAddress & ~(0xFUL << 52);
        for (int i = 0; i < profiles.Length && i < types.Length; i++)
        {
            if (profiles[i] is not { } profile) continue;
            int biome = types[i].Biome;
            if (!biomes.Contains(AnyPlanet) && !biomes.Contains(biome)) continue;
            if (biome != GasGiant && !profile.Water) continue;
            if (needsStorm && profile.Storms == 0) continue;
            yield return new FishFind(system | ((ulong)(i + 1) << 52), i + 1, types[i]);
        }
    }
}
