using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using Newtonsoft.Json.Linq;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// "Discover this system" (#43): stages discovery records for every planet, creature, plant and mineral of the loaded
/// save's current system that the save doesn't already hold, as discoveries made but not uploaded
/// (<see cref="NmsDiscoveryRecords"/>), from the generators (<see cref="SystemDiscoveries"/>). Plants and minerals need
/// game data of catalog format 108. Reset (<see cref="Revert"/>) unstages every path it touched.
/// </summary>
public static class SystemDiscoveryService
{
    /// <summary>What discovering the system would add, by kind; Records is null when nothing is missing.</summary>
    public sealed record Plan(ulong SystemAddress, int Planets, int Creatures, int Flora, int Minerals, List<DiscoveryRecordKey> Records)
    {
        public int Total => Records.Count;
    }

    /// <summary>The loaded save's current system, or null with no save or no location.</summary>
    public static ulong? CurrentSystem() =>
        S.ReadSystemAddress(SaveSessionManager.GetValue(S.CurrentUniverseAddressPath)) is ulong a ? S.ToSystemAddress(a) : null;

    /// <summary>Whether this session has the discoveries staged.</summary>
    public static bool IsStaged() => SaveSessionManager.HasStagedEditsUnder(NmsDiscoveryRecords.RecordsPath);

    /// <summary>The records the current system is missing (generators run off the UI thread); null without a save,
    /// a location or the planet tables.</summary>
    public static async Task<Plan?> PlanAsync()
    {
        if (CurrentSystem() is not ulong system) return null;
        await PlanetTypesService.EnsureLoadedAsync();
        if (PlanetTypesService.BiomeTables is not { } biomes || PlanetTypesService.ProfileTables is not { } tables) return null;
        var existing = SaveSessionManager.GetValue(NmsDiscoveryRecords.RecordsPath) as JArray;
        var missing = await Task.Run(() => NmsDiscoveryRecords.Missing(existing, SystemDiscoveries.ForSystem(system, biomes, tables)));
        return new Plan(system, missing.Count(r => r.Kind == "Planet"), missing.Count(r => r.Kind == "Animal"),
            missing.Count(r => r.Kind == "Flora"), missing.Count(r => r.Kind == "Mineral"), missing);
    }

    /// <summary>Stages <paramref name="plan"/>'s records, upload queue entries, the record count and the stats.</summary>
    public static void Stage(Plan plan)
    {
        if (plan.Records.Count == 0) return;
        var records = (SaveSessionManager.GetValue(NmsDiscoveryRecords.RecordsPath) as JArray)?.DeepClone() as JArray ?? new JArray();
        var queue = (SaveSessionManager.GetValue(NmsDiscoveryRecords.UploadQueuePath) as JArray)?.DeepClone() as JArray ?? new JArray();
        var (playerId, playerName) = Player(records);
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var key in plan.Records)
        {
            records.Add(NmsDiscoveryRecords.CreateRecord(key, now, playerId, playerName));
            queue.Add(NmsDiscoveryRecords.CreateUploadEntry(key, now));
        }

        var edits = new List<(JToken? Value, string[] Path)>
        {
            (records, NmsDiscoveryRecords.RecordsPath),
            (queue, NmsDiscoveryRecords.UploadQueuePath),
            (new JValue(records.Count), NmsDiscoveryRecords.CountPath),
        };
        // Each changed or new stat group on its own (gUR/[i], i == Count appends), as SpaceStationService stages its
        // system group: staging the whole gUR would take over other pages' group edits.
        if (SaveSessionManager.GetValue(S.StatGroupsPath) is JArray groups)
        {
            var counted = NmsDiscoveryRecords.CountInStats(groups, plan.Records);
            for (int i = 0; i < counted.Count; i++)
                if (i >= groups.Count || !JToken.DeepEquals(groups[i], counted[i]))
                {
                    edits.Add((counted[i], S.StatGroupPath(i)));
                    _stagedGroups.Add(i);
                }
        }
        SaveSessionManager.StageEdits(edits);
    }

    /// <summary>Unstages everything <see cref="Stage"/> staged.</summary>
    public static void Revert()
    {
        SaveSessionManager.RevertEdit(NmsDiscoveryRecords.RecordsPath);
        SaveSessionManager.RevertEdit(NmsDiscoveryRecords.UploadQueuePath);
        SaveSessionManager.RevertEdit(NmsDiscoveryRecords.CountPath);
        foreach (int i in _stagedGroups) SaveSessionManager.RevertEdit(S.StatGroupPath(i));
        _stagedGroups.Clear();
    }

    /// <summary>The stat groups <see cref="Stage"/> staged, for <see cref="Revert"/>.</summary>
    private static readonly List<int> _stagedGroups = new();

    /// <summary>The player's id (the save's own account record) and name (their newest own discovery that has one).</summary>
    private static (string Id, string Name) Player(JArray records)
    {
        string id = SaveSessionManager.GetValue(NmsDiscoveryRecords.PlayerIdPath)?.Value<string>() ?? "";
        var own = records.OfType<JObject>().Select(r => r["ksu"] as JObject)
            .Where(o => o is not null && (id.Length == 0 || o["K7E"]?.Value<string>() == id) && !string.IsNullOrEmpty(o["V?:"]?.Value<string>()))
            .OrderByDescending(o => o!["3I1"]?.Value<long>() ?? 0)
            .FirstOrDefault();
        if (id.Length == 0) id = own?["K7E"]?.Value<string>() ?? "";
        return (id, own?["V?:"]?.Value<string>() ?? "");
    }
}
