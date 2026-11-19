namespace ArtifactX.Core.Universe;

/// <summary>A plant or mineral a planet's placed objects can be recorded as (a Flora or Mineral discovery record,
/// fDu/ETO/OsQ/?fB 8P3: bEr = [<see cref="Seed"/>, <see cref="TypeHash"/>]).</summary>
/// <param name="Kind">"Flora" or "Mineral" (the record's &lt;Dn).</param>
/// <param name="Seed">bEr[0]: the placed object's seed; also its name seed (SpeciesNames.Flora / .Mineral).</param>
/// <param name="TypeHash">bEr[1]: <see cref="DiscoveryKeys.TypeHash"/> of <see cref="Model"/>.</param>
/// <param name="Model">The object's scene, upper case.</param>
public sealed record PlanetDiscovery(string Kind, ulong Seed, ulong TypeHash, string Model);

public static partial class PlanetProfileGenerator
{
    /// <summary>
    /// Every plant and mineral discovery a planet's placed objects can give (catalog format 108; empty without the
    /// spawn tables). Ported from NMS.exe build 184271 (Ghidra, 2026-11-19) and checked against the owner's Slot 8
    /// save: 1043 of 1056 Flora and Mineral records on 103 planets, seed and type both right (the misses are planets
    /// whose placed lists the object step doesn't predict).
    ///
    /// How the game seeds them: after the object step picks the planet's lists (child seed 7, 0x1416d1fd0) it gathers
    /// each picked file's objects into four arrays - Landmarks, Objects, DetailObjects, DistantObjects - in list order
    /// (0x1416d3b70, 0x1416d3f60), stably sorts each by Order, then PlacementPriority high first (0x1416df530, the
    /// comparator at 0x1416d3b40), and seeds each array (0x1416d4a90) from a fresh child seed 8 stream: one seed (two
    /// draws through Mix64) shared by every object, then one per object and one per alt resource after it. An object
    /// with SwapPrimaryForRandomColour takes 66 more draws after its own seed and each alt's (0x1416da170, one per
    /// colour of its 66 palettes). A placed object's discovery uses its seed and its scene's name (0x14044e1e0).
    /// A ChooseUsingLifeLevel list's file is the flora level.
    ///
    /// Not handled: a scene that picks one of several referenced scenes per instance (HQLUSH HQTREEREF's 75 parts)
    /// is sometimes recorded under the part's name; this returns the placed scene's.
    /// </summary>
    public static PlanetDiscovery[] FloraAndMinerals(ulong planetSeed, int planetIndex, PlanetType body, int star,
        int galaxyType, bool inGasGiantSystem, PlanetProfileTables t)
    {
        if (t.Objects is not { Spawn: { } spawn, DiscoveryKinds: { } kinds } objects
            || BiomeFile(objects, body.Biome, body.SubType) is not { } file)
            return [];
        var seeds = ChildSeeds(planetSeed);
        var b = Basics(seeds, planetIndex, body, star, galaxyType, inGasGiantSystem, t);
        var (_, placed) = TerrainAndObjects(seeds, body, star, b, objects, file);

        var found = new List<PlanetDiscovery>();
        var seen = new HashSet<(string, ulong, string)>();
        void Add(string model, ulong seed)
        {
            if (!kinds.TryGetValue(model, out var modelKinds)) return;
            foreach (var kind in modelKinds)
                if (seen.Add((kind, seed, model)))
                    found.Add(new PlanetDiscovery(kind, seed, DiscoveryKeys.TypeHash(model), model));
        }

        for (int category = 0; category < 4; category++)
        {
            var entries = new List<SpawnObject>();
            for (int i = 0; i < placed.Placed.Count; i++)
            {
                var list = placed.Placed[i];
                if (list.Files is not { Length: > 0 } files) continue;
                int pick = list.LifeLevel ? b.Flora : placed.Picks[i];
                if (spawn.GetValueOrDefault(files[Math.Clamp(pick, 0, files.Length - 1)]) is { } categories && category < categories.Length)
                    entries.AddRange(categories[category]);
            }
            var rng = Rng.Start(seeds[8]);
            NextSeed(ref rng); // the array's shared seed
            foreach (var e in entries.Select((e, i) => (e, i)).OrderBy(x => x.e.Order).ThenByDescending(x => x.e.Priority)
                .ThenBy(x => x.i).Select(x => x.e))
            {
                Add(e.Model, NextSeed(ref rng));
                if (e.RandomColour) Skip(ref rng, 66);
                foreach (var alt in e.Alts)
                {
                    Add(alt, NextSeed(ref rng));
                    if (e.RandomColour) Skip(ref rng, 66);
                }
            }
        }
        return found.ToArray();
    }

    private static ulong NextSeed(ref Rng rng)
    {
        ulong lo = rng.Next(), hi = rng.Next();
        return Mix64((hi << 32) | lo);
    }

    private static void Skip(ref Rng rng, int draws)
    {
        for (int i = 0; i < draws; i++) rng.Next();
    }
}
