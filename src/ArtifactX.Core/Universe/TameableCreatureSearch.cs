using ArtifactX.Core.Companions;
using ArtifactX.Core.Procedural;

namespace ArtifactX.Core.Universe;

/// <summary>A creature a player can adopt, predicted on a body: where (packed planet address, planet number from 1),
/// the predicted creature, its species/genus name seeds (<see cref="SpeciesNames.FaunaSeeds"/>) and its pet-battle
/// classes (<see cref="PetBattlerClasses.Roll"/>: GcPetBattlerCoreStat order, 0 = C ... 3 = S) and overall potential.</summary>
public sealed record TameableCreature(ulong PlanetAddress, int PlanetIndex, PredictedCreature Creature, ulong SpeciesSeed,
    ulong GenusSeed, int[] Classes, int Potential);

/// <summary>
/// The creatures players can adopt (the game's CanBeUsedInPetBattler, catalog format 83) in a star system, with their
/// battle classes: the Creature Finder's search for systems nobody in the saves has visited, and the future mobile
/// app's search from a typed or scanned starting point. About 0.7 ms per system on the owner's PC (2026-11-12).
/// </summary>
public static class TameableCreatureSearch
{
    /// <summary>The adoptable creatures on every body of <paramref name="systemAddress"/>; empty before catalog
    /// format 83 (no pet flag) or without the pet-battle tables.</summary>
    public static IEnumerable<TameableCreature> InSystem(ulong systemAddress, BiomeTables biomes, PlanetProfileTables tables)
    {
        if (tables.Creatures is not { PetBattle: { } battle } creatures) yield break;
        var profiles = SystemProfiles.Generate(systemAddress, biomes, tables);
        ulong system = systemAddress & ~(0xFUL << 52);
        for (int i = 0; i < profiles.Length; i++)
        {
            if (profiles[i]?.PredictedCreatures is not { } predicted) continue;
            ulong planetAddress = system | ((ulong)(i + 1) << 52);
            foreach (var creature in predicted)
            {
                if (creatures.CanBattle(creature) != true) continue;
                var (species, genus) = SpeciesNames.FaunaSeeds(creature.Seed,
                    string.IsNullOrEmpty(creature.SpeciesId) ? creature.Id : creature.SpeciesId,
                    planetAddress, creature.CreatureType, creature.SizeClass);
                var (classes, potential) = PetBattlerClasses.Roll(species, genus, battle.ClassRolls);
                yield return new TameableCreature(planetAddress, i + 1, creature, species, genus, classes, potential);
            }
        }
    }
}
