using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Procedural;
using ArtifactX.Core.Universe;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// The game's generated creature names for the loaded save: species names ("J.
/// Mustlimii") on Fauna and Flora records and companions (ArtifactX.Core
/// SpeciesNames; where the seeds are, NmsSpeciesSeeds), and the name a
/// companion goes by until renamed ("Ana-Ana", Core CreatureNames). The words are the catalog's NAMEGEN lists "CRE" and
/// "FLORA_ROOT" (format 69); with an older catalog every name is null.
/// </summary>
public sealed class CreatureNameService
{
    private readonly IReadOnlyList<string> _creatureRoots;
    private readonly IReadOnlyList<string> _floraRoots;
    private readonly string? _mineralSuffix;
    private readonly Dictionary<(ulong Address, ulong Seed), (ulong A, ulong B)> _creatureSeeds;

    private CreatureNameService(IReadOnlyList<string> creatureRoots, IReadOnlyList<string> floraRoots,
        Dictionary<(ulong, ulong), (ulong, ulong)> creatureSeeds, string? mineralSuffix)
    {
        _mineralSuffix = mineralSuffix;
        _creatureRoots = creatureRoots;
        _floraRoots = floraRoots;
        _creatureSeeds = creatureSeeds;
    }

    /// <summary>Reads the words and the save's creature discoveries off the UI thread.</summary>
    public static async Task<CreatureNameService> LoadAsync()
    {
        var words = await CatalogService.GetProceduralNameWordsAsync();
        var discoveries = SaveSessionManager.GetValue(NmsSpaceStationPaths.DiscoveriesPath) as JArray;
        var seeds = await Task.Run(() => NmsSpeciesSeeds.CreatureNameSeeds(discoveries));
        return new CreatureNameService(words.GetValueOrDefault("CRE") ?? [], words.GetValueOrDefault("FLORA_ROOT") ?? [], seeds,
            words.GetValueOrDefault("MINERAL_SUFFIX")?.FirstOrDefault());
    }

    /// <summary>The species on a Fauna record (its UqY pair), or null when its
    /// discovery isn't in the save.</summary>
    public string? FaunaRecord(ulong address, ulong seed) =>
        _creatureSeeds.TryGetValue((address, seed), out var s) ? SpeciesNames.Fauna(s.A, s.B, _creatureRoots) : null;

    /// <summary>The species on a Flora record (its UqY[1]).</summary>
    public string? FloraRecord(ulong seed) => seed == 0 ? null : SpeciesNames.Flora(seed, _floraRoots);

    /// <summary>The mineral on a Mineral record (its UqY[1]).</summary>
    public string? MineralRecord(ulong seed) => seed == 0 ? null : SpeciesNames.Mineral(seed, _mineralSuffix);

    /// <summary>The species name the game will give a predicted creature when it's discovered, or null when the game
    /// data predates catalog format 79 (no spawn scale, so no genus seed) or the words are missing. Right for 97% of
    /// the owner's 2026 discoveries (SpeciesNames.FaunaSeeds).</summary>
    public string? PredictedSpecies(PredictedCreature creature, ulong planetAddress)
    {
        if (creature.Seed == 0 || creature.Scale <= 0 || creature.CreatureType == 0) return null;
        var (species, genus) = SpeciesNames.FaunaSeeds(creature.Seed,
            string.IsNullOrEmpty(creature.SpeciesId) ? creature.Id : creature.SpeciesId,
            planetAddress, creature.CreatureType, creature.SizeClass);
        return SpeciesNames.Fauna(species, genus, _creatureRoots);
    }

    /// <summary>A companion's species from its m9o and JrL.</summary>
    public string? Companion(ulong seedA, ulong seedB) =>
        seedA == 0 && seedB == 0 ? null : SpeciesNames.Fauna(seedA, seedB, _creatureRoots);

    /// <summary>The name the game shows for a companion never renamed, from its
    /// CreatureID (XID) and BirthTime (XwC).</summary>
    public string? CompanionName(string creatureId, ulong birthTime) =>
        CreatureNames.Generate(CreatureNames.CompanionSeed(creatureId, birthTime), creatureId, _creatureRoots);
}
