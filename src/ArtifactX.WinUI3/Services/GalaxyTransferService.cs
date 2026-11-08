using ArtifactX.Core.NmsModels;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Service for transferring player save location to any galaxy (1..256), safely docking
/// their starship inside an active space station in a Gateway system near the galactic core (~5,571 LY).
/// All edits are staged through SaveSessionManager and can be reverted at any time before saving.
/// </summary>
public static class GalaxyTransferService
{
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

    /// <summary>Checks whether a galaxy transfer is currently staged in this edit session.</summary>
    public static bool HasStagedTransfer()
    {
        return SaveSessionManager.HasStagedEditsUnder(NmsSpaceStationPaths.CurrentUniverseAddressPath)
            || SaveSessionManager.HasStagedEditsUnder(NmsPlanetCoordinates.SpawnStatePath);
    }

    /// <summary>
    /// Stages a complete galaxy transfer to the gateway space station of the specified galaxy.
    /// Safely positions the player on foot inside the station docking walkway, places their starship
    /// on the landing pad, and registers the station in their teleporter network.
    /// </summary>
    public static void StageTransfer(GalaxyInfo destination)
    {
        ulong gatewayAddress = NmsGalaxyInfo.GetGatewaySystemAddress(destination.RealityIndex);
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

        // 4. Register the station in the teleport network (nlG)
        SpaceStationService.EnsureStationMarkerStaged(gatewayAddress, $"{destination.Name} Gateway Station");
    }

    /// <summary>Reverts all staged galaxy transfer edits, restoring original location and spawn state.</summary>
    public static void RevertTransfer()
    {
        SaveSessionManager.RevertEditsUnder(NmsSpaceStationPaths.CurrentUniverseAddressPath);
        SaveSessionManager.RevertEditsUnder(NmsPlanetCoordinates.SpawnStatePath);
        SaveSessionManager.RevertEditsUnder(NmsPlayerStateData.LocationDescriptionPath);
        SaveSessionManager.RevertEditsUnder(NmsSpaceStationPaths.MarkersPath);
    }
}

