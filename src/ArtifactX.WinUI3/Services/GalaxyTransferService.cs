using ArtifactX.Core.NmsModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Service for transferring player save location to any galaxy (1..256), safely docking
/// their starship inside an active space station in a Gateway system near the galactic core (~5,571 LY).
/// All edits are staged through SaveSessionManager and can be reverted at any time before saving.
/// </summary>
public static class GalaxyTransferService
{
    private sealed class PresenceAccumulator
    {
        public int GalaxyNumber { get; }
        public bool IsCurrent { get; set; }
        public bool IsVisited { get; set; }
        public int BaseCount { get; set; }
        public int StationCount { get; set; }
        public int SettlementCount { get; set; }
        public List<string> Bases { get; } = new();
        public List<string> Stations { get; } = new();

        public PresenceAccumulator(int number) => GalaxyNumber = number;

        public GalaxyPresence ToPresence() => new(
            GalaxyNumber,
            IsCurrent,
            IsVisited || IsCurrent || BaseCount > 0 || StationCount > 0 || SettlementCount > 0,
            BaseCount,
            StationCount,
            SettlementCount,
            Bases,
            Stations);
    }

    /// <summary>Reads the player's active galaxy from a live snapshot, loaded save, or staged edits.</summary>
    public static GalaxyInfo GetCurrentGalaxy(PlanetSnapshot? snapshot = null)
    {
        var universe = snapshot?.Universe
            ?? LiveSaveWatcher.LatestSnapshot?.Universe
            ?? SaveSessionManager.GetValue(NmsSpaceStationPaths.CurrentUniverseAddressPath);

        if (universe is JToken yhj && yhj["Iis"]?.Value<int>() is int realityIndex)
        {
            int galaxyNumber = realityIndex + 1;
            return NmsGalaxyInfo.Get(galaxyNumber)
                ?? new GalaxyInfo(galaxyNumber, realityIndex, NmsGalaxyNames.ForNumber(galaxyNumber) ?? $"Galaxy {galaxyNumber}", GalaxyClassification.Normal);
        }

        return NmsGalaxyInfo.AllGalaxies[0]; // Euclid default
    }

    /// <summary>
    /// Analyzes the player's save snapshot, bases, teleport markers, discoveries, and logged finds
    /// to determine player presence, base counts, and space station network presence across all 256 galaxies.
    /// </summary>
    public static IReadOnlyDictionary<int, GalaxyPresence> GetGalaxyPresence(PlanetSnapshot? snapshot = null)
    {
        var accumulators = new Dictionary<int, PresenceAccumulator>(256);
        for (int i = 1; i <= 256; i++)
        {
            accumulators[i] = new PresenceAccumulator(i);
        }

        // 1. Current Galaxy
        var current = GetCurrentGalaxy(snapshot);
        if (accumulators.TryGetValue(current.Number, out var cur))
        {
            cur.IsCurrent = true;
            cur.IsVisited = true;
        }

        // 2. Bases (F?0 / snapshot.Bases)
        var basesArray = snapshot?.Bases ?? SaveSessionManager.GetValue(NmsSpaceStationPaths.BasesPath) as JArray;
        if (basesArray is not null)
        {
            foreach (var b in basesArray.OfType<JObject>())
            {
                if (NmsSpaceStationPaths.ParsePackedAddress(b["oZw"]) is not ulong addr) continue;
                int g = NmsSpaceStationPaths.GalaxyNumber(addr);
                if (g < 1 || g > 256) continue;

                string kind = b["peI"]?["DPp"]?.Value<string>() ?? "";
                string name = b["NKm"]?.Value<string>() ?? "";
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = kind switch
                    {
                        "HomePlanetBase" => "Planet Base",
                        "PlayerSpaceStationBase" => "Claimed Station",
                        "Settlement" => "Settlement",
                        _ => "Base"
                    };
                }

                var acc = accumulators[g];
                acc.IsVisited = true;
                if (kind == "Settlement")
                {
                    acc.SettlementCount++;
                    acc.Bases.Add($"{name} (Settlement)");
                }
                else if (kind == "PlayerSpaceStationBase")
                {
                    acc.StationCount++;
                    acc.Stations.Add($"{name} (Claimed Station)");
                }
                else
                {
                    acc.BaseCount++;
                    acc.Bases.Add(name);
                }
            }
        }

        // 3. Space Stations in Teleport Network (nlG / snapshot.Markers)
        var markersArray = snapshot?.Markers ?? SaveSessionManager.GetValue(NmsSpaceStationPaths.MarkersPath) as JArray;
        if (markersArray is not null)
        {
            foreach (var m in markersArray.OfType<JObject>())
            {
                if (m["iAF"]?.Value<string>() != NmsSpaceStationPaths.StationMarkerType) continue;

                int g = 0;
                if (m["yhJ"]?["Iis"]?.Value<int>() is int realityIndex)
                {
                    g = realityIndex + 1;
                }
                else if (NmsSpaceStationPaths.ReadSystemAddress(m["yhJ"]) is ulong addr)
                {
                    g = NmsSpaceStationPaths.GalaxyNumber(addr);
                }

                if (g < 1 || g > 256) continue;

                string name = m["NKm"]?.Value<string>() ?? "Space Station";
                var acc = accumulators[g];
                acc.IsVisited = true;
                if (!acc.Stations.Contains(name))
                {
                    acc.StationCount++;
                    acc.Stations.Add(name);
                }
            }
        }

        // 4. Discoveries (fDu / snapshot.Discoveries)
        var discoveries = snapshot?.Discoveries
            ?? SaveSessionManager.GetValue(new[] { "fDu", "ETO", "OsQ", "?fB" }) as JArray;
        if (discoveries is not null)
        {
            foreach (var record in discoveries.OfType<JObject>())
            {
                if (record["8P3"]?["5L6"] is JToken addrTok && NmsSpaceStationPaths.ParsePackedAddress(addrTok) is ulong addr)
                {
                    int g = NmsSpaceStationPaths.GalaxyNumber(addr);
                    if (g >= 1 && g <= 256)
                    {
                        accumulators[g].IsVisited = true;
                    }
                }
            }
        }

        // 5. Finds logged in user.db
        try
        {
            foreach (var f in FindsService.All)
            {
                if (f.Galaxy >= 1 && f.Galaxy <= 256)
                {
                    accumulators[f.Galaxy].IsVisited = true;
                }
            }
        }
        catch
        {
            // Ignore if database is busy
        }

        return accumulators.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToPresence());
    }

    /// <summary>Checks whether a galaxy transfer is currently staged in this edit session.</summary>
    public static bool HasStagedTransfer()
    {
        return SaveSessionManager.HasStagedEditsUnder(NmsSpaceStationPaths.CurrentUniverseAddressPath)
            || SaveSessionManager.HasStagedEditsUnder(NmsPlanetCoordinates.SpawnStatePath);
    }

    /// <summary>The teleporter marker this session's transfer added, so undoing it removes only that marker.</summary>
    private static ulong? _addedMarkerAddress;

    /// <summary>
    /// The system a transfer to <paramref name="destination"/> lands in: one that exists and has a working space
    /// station (<see cref="NmsGalaxyInfo.FindGatewaySystemAddress"/>). Null when the catalog has no galaxy-generation
    /// settings yet (rebuild the game data) or, in theory, when no system in the region qualifies.
    /// </summary>
    public static async Task<ulong?> FindDestinationAsync(GalaxyInfo destination)
    {
        if (await RegionSystemsService.GetAsync(NmsGalaxyInfo.GatewayRegionAddress(destination.RealityIndex)) is not { } count)
            return null;
        return await Task.Run(() => NmsGalaxyInfo.FindGatewaySystemAddress(destination.RealityIndex, count.Ordinary));
    }

    /// <summary>
    /// Stages a complete galaxy transfer to the space station of <paramref name="gatewayAddress"/> (from
    /// <see cref="FindDestinationAsync"/>). The player spawns "InStation"; the game places them inside that system's
    /// station (owner-tested transfers, 2026-11-10). The station is added to the teleport network as an ordinary,
    /// unowned station; a transfer never claims it or stamps it with the player's alliance.
    /// </summary>
    public static void StageTransfer(GalaxyInfo destination, ulong gatewayAddress)
    {
        var yhj = NmsSpaceStationPaths.CreateUniverseAddress(gatewayAddress);

        // 1. Stage universe address
        SaveSessionManager.StageEdit(yhj, NmsSpaceStationPaths.CurrentUniverseAddressPath);

        // 2. Stage location description
        SaveSessionManager.StageEdit($"Aboard {destination.Name} Core Station", NmsPlayerStateData.LocationDescriptionPath);

        // 3. Stage spawn state (safe inside station docking bay)
        var origSpawn = SaveSessionManager.GetValue(NmsPlanetCoordinates.SpawnStatePath) as JObject ?? new JObject();
        var spawn = (JObject)origSpawn.DeepClone();

        spawn["jk4"] = "InStation";
        spawn["mEH"] = new JArray { 497.955322265625, -10050.97265625, -368.651611328125, 1.0 };
        spawn["tnP"] = new JArray { 479.33544921875, -10033.919921875, -356.0213623046875, 1.0 };
        spawn["tmM"] = new JArray { 497.955322265625, -10050.97265625, -368.651611328125, 1.0 };
        spawn["PK0"] = new JArray { -0.12115523964166641, 0.5187167525291443, 0.8463181853294373, 1.0 };
        spawn["l2U"] = new JArray { -0.12115523964166641, 0.5187167525291443, 0.8463181853294373, 1.0 };
        spawn["l4H"] = new JArray { -0.6081892251968384, 0.6528269052505493, 0.4515783488750458, 1.0 };
        spawn["4xJ"] = new JArray { 0.5240810513496399, 0.7575072646141052, -0.38925841450691223, 1.0 };
        spawn["lKr"] = false;

        SaveSessionManager.StageEdit(spawn, NmsPlanetCoordinates.SpawnStatePath);

        // 4. Add the station to the teleport network (nlG) as an ordinary visited station, if it isn't there yet.
        RemoveAddedMarker();
        var markers = SaveSessionManager.GetValue(NmsSpaceStationPaths.MarkersPath) as JArray ?? new JArray();
        bool known = markers.OfType<JObject>().Any(m =>
            m["iAF"]?.Value<string>() == NmsSpaceStationPaths.StationMarkerType
            && NmsSpaceStationPaths.ReadSystemAddress(m["yhJ"]) == gatewayAddress);
        if (!known)
        {
            var updated = (JArray)markers.DeepClone();
            updated.Add(NmsSpaceStationPaths.CreateStationMarker(gatewayAddress, $"{destination.Name} Gateway Station",
                new JArray { 0, 0, 0, 0 }));
            SaveSessionManager.StageEdit(updated, NmsSpaceStationPaths.MarkersPath);
            _addedMarkerAddress = gatewayAddress;
        }
    }

    /// <summary>
    /// Reverts a staged galaxy transfer: location, spawn state, and only the teleporter marker the transfer added,
    /// so station claims staged on the Space Stations page (same marker array) are kept.
    /// </summary>
    public static void RevertTransfer()
    {
        SaveSessionManager.RevertEditsUnder(NmsSpaceStationPaths.CurrentUniverseAddressPath);
        SaveSessionManager.RevertEditsUnder(NmsPlanetCoordinates.SpawnStatePath);
        SaveSessionManager.RevertEditsUnder(NmsPlayerStateData.LocationDescriptionPath);
        RemoveAddedMarker();
    }

    private static void RemoveAddedMarker()
    {
        if (_addedMarkerAddress is not ulong added) return;
        _addedMarkerAddress = null;
        if (SaveSessionManager.GetValue(NmsSpaceStationPaths.MarkersPath) is not JArray markers) return;
        var updated = new JArray(markers.Where(m => !(m is JObject o
            && o["iAF"]?.Value<string>() == NmsSpaceStationPaths.StationMarkerType
            && NmsSpaceStationPaths.ReadSystemAddress(o["yhJ"]) == added)).Select(m => m.DeepClone()));
        if (updated.Count != markers.Count) SaveSessionManager.StageEdit(updated, NmsSpaceStationPaths.MarkersPath);
    }
}
