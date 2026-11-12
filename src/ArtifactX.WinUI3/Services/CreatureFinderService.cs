using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Procedural;
using ArtifactX.Core.Universe;
using ArtifactX.WinUI3.Helpers.Fauna;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>One creature the Creature Finder lists: where it lives, its name, whether this save has discovered it,
/// its traits and its pet-battle classes.</summary>
public sealed record FoundCreature(KnownPlanet Planet, string Name, bool Discovered, PredictedCreature Creature,
    CreatureBattle Battle, string Rarity, string Activity, string Habitat);

/// <summary>
/// The Creature Finder's search (issue #6): every creature a player can adopt as a battle pet (the game's
/// CanBeUsedInPetBattler, catalog format 83) on every body of every system the saves know, with its Trait Potential
/// classes. Predictions come from the planet generator (PlanetTypesService.Profile, cached per system; about 0.7 ms
/// per system, 10,000 creatures in 0.2 s on the owner's PC, 2026-11-12). A creature this save discovered keeps its
/// recorded name and name seeds, as on Planet Details.
/// </summary>
public static class CreatureFinderService
{
    /// <summary>The tameable creatures on <paramref name="planets"/> and the other bodies of their systems, off the UI
    /// thread. Empty without the planet tables or before catalog format 83.</summary>
    public static async Task<List<FoundCreature>> FindAsync(IReadOnlyList<KnownPlanet> planets, JArray? discoveries)
    {
        await PlanetTypesService.EnsureLoadedAsync();
        var names = await CreatureNameService.LoadAsync();
        return await Task.Run(() => Find(planets, discoveries, names));
    }

    private static List<FoundCreature> Find(IReadOnlyList<KnownPlanet> planets, JArray? discoveries, CreatureNameService names)
    {
        var found = new List<FoundCreature>();
        if (PlanetTypesService.CreatureTables is not { } tables) return found;
        var saved = FaunaDiscoveryService.ReadAll(discoveries, names);

        // The known bodies, then the undiscovered ones in the same systems (Planet Details opens either).
        var bodies = planets.GroupBy(p => p.Address).Select(g => g.First()).ToList();
        foreach (var system in bodies.GroupBy(p => p.SystemAddress).ToList())
            bodies.AddRange(PlanetTypesService.Undiscovered(system.Key, system.Select(p => p.PlanetIndex).ToHashSet()));

        foreach (var planet in bodies)
        {
            var predicted = PlanetTypesService.Profile(planet.SystemAddress, planet.PlanetIndex)?.PredictedCreatures;
            if (predicted is null || predicted.Length == 0) continue;
            var mine = saved.GetValueOrDefault(planet.Address) ?? [];
            foreach (var slot in PlanetFaunaPairing.Pair(predicted, mine.Select(d => d.Seed).ToList()))
            {
                if (slot.Predicted is not { } creature || tables.CanBattle(creature) != true) continue;
                // A discovery counts only when it is this very creature (same seed), as on Planet Details.
                var disc = slot.Discovery >= 0 && mine[slot.Discovery].Seed == creature.Seed ? mine[slot.Discovery] : null;
                string? name = disc?.SpeciesName ?? names.PredictedSpecies(creature, planet.Address);
                if (name is null || CreatureListService.IsDeleted(planet.SystemAddress, planet.PlanetIndex, name)) continue;
                var (speciesSeed, genusSeed) = disc is not null ? (disc.SpeciesSeed, disc.GenusSeed)
                    : SpeciesNames.FaunaSeeds(creature.Seed, string.IsNullOrEmpty(creature.SpeciesId) ? creature.Id : creature.SpeciesId,
                        planet.Address, creature.CreatureType, creature.SizeClass);
                if (CreatureTraitText.Battle(creature, tables, speciesSeed, genusSeed) is not { } battle) continue;
                var (rarity, activity) = CreatureTraitText.RarityAndActivity(creature, tables);
                found.Add(new FoundCreature(planet, name, disc is not null, creature, battle, rarity, activity, tables.HabitatOf(creature)));
            }
        }
        return found;
    }
}
