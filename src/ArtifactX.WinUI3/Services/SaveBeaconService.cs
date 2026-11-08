using ArtifactX.Core.NmsModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Places and removes physical in-game Save Beacons (^BUILDBEACON in vLc.6f=.c5s) at planetary coordinates.
/// Unlike scan events (A1f), which only show for registered procedural buildings, a Save Beacon is a real
/// physical waypoint object that produces a permanent in-game HUD waypoint visible from space and terrain.
/// </summary>
public static class SaveBeaconService
{
    private static readonly HashSet<string> AddedKeys = new(StringComparer.OrdinalIgnoreCase);

    public static bool CanAdd => SaveSessionManager.IsSaveLoaded && !GameProcessMonitorService.IsGameRunning;

    public static bool CanRemove => SaveSessionManager.IsSaveLoaded && !GameProcessMonitorService.IsGameRunning;

    public static string PosKey(ulong planetAddress, Vector3 pos)
    {
        float l = pos.Length();
        Vector3 dir = l > 1f ? pos / l : pos;
        return $"{planetAddress:X}_{Math.Round(dir.X * 100.0)}_{Math.Round(dir.Y * 100.0)}_{Math.Round(dir.Z * 100.0)}";
    }

    /// <summary>
    /// Checks if two planet-relative coordinates refer to the same geographical site on the planet.
    /// Uses angular surface distance on the planet rather than raw Euclidean distance, so that elevation
    /// offsets (e.g. subterranean vs floating beacons) still correctly match the site.
    /// </summary>
    public static bool MatchesSite(Vector3 p1, Vector3 p2, float maxSurfaceDistMeters = 50.0f)
    {
        float l1 = p1.Length();
        float l2 = p2.Length();
        if (l1 < 100f || l2 < 100f) return Vector3.Distance(p1, p2) < maxSurfaceDistMeters;

        Vector3 dir1 = p1 / l1;
        Vector3 dir2 = p2 / l2;
        float dot = Math.Clamp(Vector3.Dot(dir1, dir2), -1f, 1f);
        float angle = MathF.Acos(dot);
        float avgRadius = (l1 + l2) * 0.5f;
        return (angle * avgRadius) < maxSurfaceDistMeters;
    }

    /// <summary>Checks if a Save Beacon exists in c5s at this planetary site (within 50m surface distance).</summary>
    public static bool IsPlaced(ulong planetAddress, Vector3 localPos)
    {
        if (SaveSessionManager.GetValue(NmsBuildCounts.LooseObjectsPath) is not JArray objects) return false;

        foreach (var token in objects)
        {
            if (token is not JObject obj) continue;
            if (obj["r<7"]?.Value<string>() != "^BUILDBEACON") continue;
            if (NmsSpaceStationPaths.ParsePackedAddress(obj["oZw"]) != planetAddress) continue;
            if (obj["wMC"] is not JArray posArr || posArr.Count != 3) continue;

            float x = posArr[0]?.Value<float>() ?? 0;
            float y = posArr[1]?.Value<float>() ?? 0;
            float z = posArr[2]?.Value<float>() ?? 0;

            if (MatchesSite(new Vector3(x, y, z), localPos, 50.0f))
                return true;
        }

        return false;
    }

    /// <summary>Checks if a beacon at this position was added in the current session and is waiting to be saved.</summary>
    public static bool IsUnsaved(ulong planetAddress, Vector3 localPos) =>
        AddedKeys.Contains(PosKey(planetAddress, localPos))
        && SaveSessionManager.HasStagedEdit(NmsBuildCounts.LooseObjectsPath)
        && IsPlaced(planetAddress, localPos);

    /// <summary>Places a Save Beacon in c5s at the given planet-local position.</summary>
    public static void Add(ulong planetAddress, Vector3 localPos)
    {
        if (!CanAdd) return;

        var objects = SaveSessionManager.GetValue(NmsBuildCounts.LooseObjectsPath) is JArray current
            ? (JArray)current.DeepClone()
            : new JArray();

        // Remove any existing beacon at this site first
        for (int i = objects.Count - 1; i >= 0; i--)
        {
            if (objects[i] is JObject obj
                && obj["r<7"]?.Value<string>() == "^BUILDBEACON"
                && NmsSpaceStationPaths.ParsePackedAddress(obj["oZw"]) == planetAddress
                && obj["wMC"] is JArray arr && arr.Count == 3)
            {
                float ox = arr[0]?.Value<float>() ?? 0;
                float oy = arr[1]?.Value<float>() ?? 0;
                float oz = arr[2]?.Value<float>() ?? 0;
                if (MatchesSite(new Vector3(ox, oy, oz), localPos, 50.0f))
                    objects.RemoveAt(i);
            }
        }

        Vector3 up = Vector3.Normalize(localPos);
        Vector3 temp = Math.Abs(up.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitZ;
        Vector3 right = Vector3.Normalize(Vector3.Cross(up, temp));
        Vector3 at = Vector3.Normalize(Vector3.Cross(right, up));

        // Offset slightly (+2m) along outward normal
        Vector3 beaconPos = localPos + up * 2.0f;

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        ulong beaconSeed = ((ulong)(uint)Random.Shared.Next() << 32) | (ulong)(uint)Random.Shared.Next();
        var beacon = new JObject
        {
            ["b1:"] = now,
            ["r<7"] = "^BUILDBEACON",
            ["oZw"] = $"0x{planetAddress:X}",
            ["ofi"] = $"0x{beaconSeed:X16}",
            ["CVX"] = 0,
            ["wMC"] = new JArray(Math.Round(beaconPos.X, 3), Math.Round(beaconPos.Y, 3), Math.Round(beaconPos.Z, 3)),
            ["wJ0"] = new JArray(Math.Round(up.X, 4), Math.Round(up.Y, 4), Math.Round(up.Z, 4)),
            ["aNu"] = new JArray(Math.Round(at.X, 4), Math.Round(at.Y, 4), Math.Round(at.Z, 4)),
        };

        objects.Add(beacon);
        SaveSessionManager.StageEdit(objects, NmsBuildCounts.LooseObjectsPath);
        AddedKeys.Add(PosKey(planetAddress, localPos));
    }

    /// <summary>Removes a placed Save Beacon near this local position.</summary>
    public static void Remove(ulong planetAddress, Vector3 localPos)
    {
        if (SaveSessionManager.GetValue(NmsBuildCounts.LooseObjectsPath) is not JArray current) return;

        var objects = (JArray)current.DeepClone();
        bool changed = false;

        for (int i = objects.Count - 1; i >= 0; i--)
        {
            if (objects[i] is JObject obj
                && obj["r<7"]?.Value<string>() == "^BUILDBEACON"
                && NmsSpaceStationPaths.ParsePackedAddress(obj["oZw"]) == planetAddress
                && obj["wMC"] is JArray arr && arr.Count == 3)
            {
                float ox = arr[0]?.Value<float>() ?? 0;
                float oy = arr[1]?.Value<float>() ?? 0;
                float oz = arr[2]?.Value<float>() ?? 0;
                if (MatchesSite(new Vector3(ox, oy, oz), localPos, 50.0f))
                {
                    objects.RemoveAt(i);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            SaveSessionManager.StageEdit(objects, NmsBuildCounts.LooseObjectsPath);
            AddedKeys.Remove(PosKey(planetAddress, localPos));
        }
    }
}

