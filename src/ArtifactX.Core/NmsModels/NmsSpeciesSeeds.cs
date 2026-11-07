using Newtonsoft.Json.Linq;

namespace ArtifactX.Core.NmsModels;

/// <summary>
/// Where the seeds of a species name (ArtifactX.Core.Procedural.SpeciesNames)
/// are in the save. Checked on Slot 8 on 2026-11-04 against the game's own
/// screens, 26 of 26 names:
///  - a Fauna record (MZ1, GcWonderRecord UqY) holds [planet address, bEr[0]]
///    of the creature's discovery (fDu/ETO/OsQ/?fB, 8P3 {5L6 address, &lt;Dn
///    "Animal", bEr seeds}); the name comes from that discovery's bEr[2] and
///    bEr[3]. All 14 recorded Slot 8 creatures had their discovery in the save;
///  - a Flora record's (fp9) UqY[1] is the plant's own seed, and a Mineral
///    record's (bYs) the mineral's (8/8 on Slot 8);
///  - a companion's are m9o and JrL (NmsCompanionPaths.RollSeedPrimaryPath and
///    RollSeedSecondaryPath).
/// </summary>
public static class NmsSpeciesSeeds
{
    /// <summary>Every creature discovery's name seeds (bEr[2], bEr[3]), keyed by
    /// (address, bEr[0]) as a Fauna record stores them.</summary>
    public static Dictionary<(ulong Address, ulong Seed), (ulong A, ulong B)> CreatureNameSeeds(JArray? discoveries)
    {
        var seeds = new Dictionary<(ulong, ulong), (ulong, ulong)>();
        foreach (var record in discoveries?.OfType<JObject>() ?? [])
        {
            if (record["8P3"] is not JObject key || key["<Dn"]?.Value<string>() != "Animal") continue;
            if (NmsSpaceStationPaths.ParsePackedAddress(key["5L6"]) is not ulong address) continue;
            if (key["bEr"] is not JArray values || values.Count < 4) continue;
            if (NmsSpaceStationPaths.ParsePackedAddress(values[0]) is not ulong first
                || NmsSpaceStationPaths.ParsePackedAddress(values[2]) is not ulong a
                || NmsSpaceStationPaths.ParsePackedAddress(values[3]) is not ulong b) continue;
            seeds[(address, first)] = (a, b);
        }
        return seeds;
    }
}
