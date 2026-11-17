using ArtifactX.Core.NmsModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>A creature the save has a discovery record for: its species name (the player's own name when renamed),
/// its seed (bEr[0], what PlanetFaunaPairing pairs on) and its name seeds (bEr[2]/bEr[3]).</summary>
public sealed record FaunaDiscovery(string SpeciesName, ulong Seed, ulong SpeciesSeed, ulong GenusSeed);

/// <summary>
/// The save's creature discoveries per planet, for Planet Details and the Creature Finder. Only records the game has
/// uploaded or named (ksu f5Q/K7E) count, as the Discoveries page does; a name seen twice on a planet is one species.
/// </summary>
public static class FaunaDiscoveryService
{
    /// <summary>Every planet's creature discoveries, keyed by planet address (pure, any thread).</summary>
    public static Dictionary<ulong, List<FaunaDiscovery>> ReadAll(JArray? discoveries, CreatureNameService names)
    {
        var result = new Dictionary<ulong, List<FaunaDiscovery>>();
        var fallback = new Dictionary<ulong, int>();
        if (discoveries is null) return result;
        foreach (var record in discoveries.OfType<JObject>())
        {
            if (record["8P3"] is not JObject key || key["<Dn"]?.Value<string>() != "Animal") continue;
            if (NmsSpaceStationPaths.ParsePackedAddress(key["5L6"]) is not ulong address) continue;
            if (string.IsNullOrEmpty(record["ksu"]?["f5Q"]?.Value<string>()) && string.IsNullOrEmpty(record["ksu"]?["K7E"]?.Value<string>())) continue;
            if (key["bEr"] is not JArray bEr || bEr.Count < 2) continue;

            ulong seed = NmsSpaceStationPaths.ParsePackedAddress(bEr[0]) ?? 0;
            ulong seedA = bEr.Count > 2 ? (NmsSpaceStationPaths.ParsePackedAddress(bEr[2]) ?? 0) : 0;
            ulong seedB = bEr.Count > 3 ? (NmsSpaceStationPaths.ParsePackedAddress(bEr[3]) ?? 0) : 0;

            if (!result.TryGetValue(address, out var list)) result[address] = list = new();
            string? custom = record["q9a"]?["q5u"]?.Value<string>()?.Trim();
            string species;
            if (!string.IsNullOrWhiteSpace(custom)) species = custom;
            else if (names.Companion(seedA, seedB) is string generated) species = generated;
            else
            {
                int n = fallback.GetValueOrDefault(address) + 1;
                fallback[address] = n;
                species = $"Species #{n}";
            }

            if (list.Any(s => string.Equals(s.SpeciesName, species, StringComparison.OrdinalIgnoreCase))) continue;
            list.Add(new FaunaDiscovery(species, seed, seedA, seedB));
        }
        return result;
    }
}
