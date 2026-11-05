using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Procedural;
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
    private readonly Dictionary<(ulong Address, ulong Seed), (ulong A, ulong B)> _creatureSeeds;

    private CreatureNameService(IReadOnlyList<string> creatureRoots, IReadOnlyList<string> floraRoots,
        Dictionary<(ulong, ulong), (ulong, ulong)> creatureSeeds)
    {
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
        return new CreatureNameService(words.GetValueOrDefault("CRE") ?? [], words.GetValueOrDefault("FLORA_ROOT") ?? [], seeds);
    }

    /// <summary>The species on a Fauna record (its UqY pair), or null when its
    /// discovery isn't in the save.</summary>
    public string? FaunaRecord(ulong address, ulong seed) =>
        _creatureSeeds.TryGetValue((address, seed), out var s) ? SpeciesNames.Fauna(s.A, s.B, _creatureRoots) : null;

    /// <summary>The species on a Flora record (its UqY[1]).</summary>
    public string? FloraRecord(ulong seed) => seed == 0 ? null : SpeciesNames.Flora(seed, _floraRoots);

    /// <summary>A companion's species from its m9o and JrL.</summary>
    public string? Companion(ulong seedA, ulong seedB) =>
        seedA == 0 && seedB == 0 ? null : SpeciesNames.Fauna(seedA, seedB, _creatureRoots);

    /// <summary>The name the game shows for a companion never renamed, from its
    /// CreatureID (XID) and BirthTime (XwC).</summary>
    public string? CompanionName(string creatureId, ulong birthTime) =>
        CreatureNames.Generate(CreatureNames.CompanionSeed(creatureId, birthTime), creatureId, _creatureRoots);
}
