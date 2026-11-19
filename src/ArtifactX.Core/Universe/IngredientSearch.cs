using ArtifactX.Core.NmsModels;

namespace ArtifactX.Core.Universe;

/// <summary>A body that gives an item, as two lines: the body ("Frozen planet Okiru II") and what to do there
/// ("hunt the Kordiussia · fish in storms"; empty when there's nothing to add).</summary>
public sealed record IngredientBody(string Planet, string Detail)
{
    public bool HasDetail => Detail.Length > 0;
    public override string ToString() => HasDetail ? $"{Planet}, {Detail}" : Planet;
}

/// <summary>A star system that has an item, for the "Closest to you" list: its name, how far it is in words, and its
/// bodies that give the item.</summary>
public sealed record IngredientPlace(ulong SystemAddress, string SystemName, string Distance, IReadOnlyList<IngredientBody> Bodies,
    bool IsHere, double SortKey)
{
    public string AccessibleName => $"{SystemName}, {Distance}: {string.Join("; ", Bodies)}";
}

/// <summary>Where a fish lives: its planet types (GcBiomeType; 16 = any planet) and whether it bites only in storms.</summary>
public sealed record FishHabitat(int[] Biomes, bool NeedsStorm);

/// <summary>
/// Where to go for a wild ingredient, creature produce or fish (the Ingredient Finder, catalog format 93): the systems
/// nearest a starting system whose planets give it, from the game's own generators (NearbySystems, HarvestSearch,
/// CreatureProduceSearch, FishSearch). The start's region is searched first, then the regions around it only when it
/// holds too few. Distances inside the start's region are the galaxy map's light-years when <c>stars</c> is given
/// (RegionStarPositions, checked on the map 2026-10-27), else "This region"; farther systems read "1 region away".
/// Moved from the desktop's IngredientLocatorService (2026-11-18) so the phone shares it.
/// </summary>
public static class IngredientSearch
{
    /// <summary>What the search looks for before it stops widening.</summary>
    public const int Wanted = 5;

    /// <summary>The systems nearest <paramref name="start"/> that give <paramref name="itemId"/>, nearest first (the
    /// start itself first when it does). In parallel; <paramref name="cancel"/> stops it. <paramref name="fish"/> is the
    /// item's habitat when it is a fish caught outside missions; <paramref name="huntIds"/> the creatures whose kill
    /// drops it; <paramref name="speciesName"/> names a predicted creature (null: "feed its creatures");
    /// <paramref name="stars"/> are the start region's stars for light-year distances.</summary>
    public static IReadOnlyList<IngredientPlace> Find(ulong start, string itemId, GalaxyGenerationSettings settings,
        BiomeTables biomes, PlanetProfileTables tables, FishHabitat? fish, IReadOnlySet<string> huntIds,
        Func<PredictedCreature, ulong, string?>? speciesName, IReadOnlyList<RegionStar>? stars, CancellationToken cancel)
    {
        bool fed = CreatureProduceSearch.IsProduce(itemId, tables.Creatures);
        var creatures = fed || huntIds.Count > 0 ? tables.Creatures : null;
        bool plants = tables.Objects?.Harvest is not null;
        if (creatures is null && !plants && fish is null) return [];
        var hunt = huntIds as HashSet<string> ?? new HashSet<string>(huntIds, StringComparer.OrdinalIgnoreCase);
        ulong startSystem = start & ~(0xFUL << 52);
        int startIndex = (int)((start >> 40) & 0xFFF);
        var here = stars?.FirstOrDefault(s => s.System == startIndex);

        var found = new System.Collections.Concurrent.ConcurrentBag<IngredientPlace>();
        foreach (int radius in new[] { 0, 1 })
        {
            var systems = NearbySystems.Around(start, radius, settings)
                .Where(s => radius == 0 || s.RegionsAway > 0).ToList();
            Parallel.ForEach(systems, new ParallelOptions { CancellationToken = cancel }, system =>
            {
                var (portal, galaxy) = SystemProfiles.PortalOf(system.SystemAddress);
                PlanetType[] types;
                try { types = PlanetGenerator.Generate(portal, galaxy, biomes); }
                catch { return; }
                var profiles = SystemProfiles.Generate(system.SystemAddress, biomes, tables);
                var bodies = new SortedDictionary<int, (string Planet, List<string> Details)>();
                List<string> At(int index, PlanetType body)
                {
                    if (!bodies.TryGetValue(index, out var entry)) bodies[index] = entry = (Body(portal, galaxy, index, body), []);
                    return entry.Details;
                }
                if (plants)
                    foreach (var f in HarvestSearch.On(system.SystemAddress, types, profiles, itemId))
                    {
                        var details = At(f.PlanetIndex, f.Body);
                        string where = f.Harvest.TileType switch { 4 => "underwater", 5 => "in caves", _ => "" };
                        string mine = MinedDetail(itemId);
                        if (f.Harvest.Mined) details.Add(where.Length > 0 ? $"{mine} {where}" : mine);
                        else if (where.Length > 0) details.Add("found " + where);
                    }
                if (creatures is not null)
                    foreach (var f in CreatureProduceSearch.On(system.SystemAddress, types, profiles, itemId, creatures, fed, hunt))
                        At(f.PlanetIndex, f.Body).AddRange(new[] { Species("feed", f.Fed, f.PlanetAddress, speciesName),
                            Species("hunt", f.Hunted, f.PlanetAddress, speciesName) }.Where(t => t.Length > 0));
                if (fish is not null)
                    foreach (var f in FishSearch.On(system.SystemAddress, types, profiles, fish.Biomes, fish.NeedsStorm))
                        At(f.PlanetIndex, f.Body).Add(fish.NeedsStorm ? "fish in storms" : "fish here");
                if (bodies.Count > 0)
                    found.Add(Place(system, [.. bodies.Values.Select(b => new IngredientBody(b.Planet, string.Join(" · ", b.Details.Distinct())))],
                        startSystem, startIndex, stars, here));
            });
            if (found.Count(p => !p.IsHere) >= Wanted) break;
        }
        return found.OrderBy(p => p.SortKey).Take(Wanted + 1).ToList();
    }

    /// <summary>What to do to get a mined item: break a Whispering Egg for Larval Core, kill the ground worms for a
    /// Hexite... core, else mine it.</summary>
    public static string MinedDetail(string itemId) => itemId.ToUpperInvariant() switch
    {
        "FIENDCORE" => "break a Whispering Egg",
        "WORMCORE" => "kill the ground worms",
        _ => "mine it",
    };

    /// <summary>"Frozen planet Okiru II", "Infested Lush planet ..." for the infested variant players look for.</summary>
    private static string Body(ulong portal, int galaxy, int planetIndex, PlanetType type) =>
        $"{(type.SubType == NmsPlanets.InfestedSubType ? "Infested " : "")}{NmsPlanets.BiomeGroup(type.Biome)} {(SystemGenerator.Bodies(portal, galaxy).IsMoon(planetIndex) ? "moon" : "planet")} "
        + SystemGenerator.PlanetName(portal | ((ulong)planetIndex << 44), galaxy);

    /// <summary>"feed the Kordiussia" (the species name the game gives once scanned), "hunt the Kordiussia or the
    /// Vrandeis", "... or 2 more"; "feed its creatures" when the names are unknown; "" for no creatures.</summary>
    private static string Species(string verb, PredictedCreature[] creatures, ulong planetAddress, Func<PredictedCreature, ulong, string?>? names)
    {
        if (creatures.Length == 0) return "";
        var species = creatures.Select(c => names?.Invoke(c, planetAddress)).OfType<string>().Distinct().ToList();
        if (species.Count == 0) return $"{verb} its creatures";
        string shown = string.Join(" or ", species.Take(2).Select(n => "the " + n));
        return species.Count > 2 ? $"{verb} {shown} or {species.Count - 2} more" : $"{verb} {shown}";
    }

    private static IngredientPlace Place(NearbySystem system, List<IngredientBody> bodies, ulong startSystem, int startIndex,
        IReadOnlyList<RegionStar>? stars, RegionStar? here)
    {
        var (portal, galaxy) = SystemProfiles.PortalOf(system.SystemAddress);
        int index = (int)((system.SystemAddress >> 40) & 0xFFF);
        bool isHere = (system.SystemAddress & ~(0xFUL << 52)) == startSystem;
        string distance;
        double sort;
        if (isHere) { distance = "You're here"; sort = -1; }
        else if (system.RegionsAway == 0 && here is { } h && stars?.FirstOrDefault(s => s.System == index) is { System: > 0 } star)
        {
            int ly = RegionStarPositions.LightYears(star, h);
            distance = $"{ly:N0} ly";
            sort = ly;
        }
        else
        {
            distance = system.RegionsAway == 0 ? "This region" : system.RegionsAway == 1 ? "1 region away" : $"{system.RegionsAway} regions away";
            sort = system.RegionsAway * 100_000 + index;
        }
        return new IngredientPlace(system.SystemAddress, SystemGenerator.SystemName(portal, galaxy), distance, bodies, isHere, sort);
    }
}
