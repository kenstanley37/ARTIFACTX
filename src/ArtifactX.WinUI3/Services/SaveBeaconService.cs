using ArtifactX.Core.NmsModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Places and removes physical in-game Save Beacons (^BUILDBEACON in vLc.6f=.c5s) at planetary coordinates.
/// Unlike scan events (A1f), which only show for registered procedural buildings, a Save Beacon is a real
/// physical object with a permanent in-game HUD waypoint.
///
/// Save format (2026-11-10, read-only inspection of Slots 5, 7 and 8): every beacon the game placed away from a
/// base is one c5s entry {b1: unix seconds, r&lt;7: "^BUILDBEACON", oZw: planet address, ofi: 64-bit seed, CVX: 0,
/// wMC: position from the planet centre, wJ0: up (= normalised wMC), aNu: facing}; its seed appears nowhere else
/// in the save. ArtifactX writes the same shape.
///
/// Why only the waypoint shows (owner tests on Slot 8, 2026-11-10; docs/research/2026-11-10-SAVE-BEACON-PLACEMENT.md):
/// the c5s entry above is all a real beacon is, and the game accepts ArtifactX's. Two things stop it being built:
/// the game's limits (BUILDBEACON RegionLimit 1, PlanetLimit 5 in GcBaseBuildingTable; a beacon over a limit keeps
/// only its waypoint) and height. Beacons are stored where they stand; <c>PlanetTerrain.Height</c> + 0.2 m is the
/// ground on some planets but 13-21 m too low on others (a real beacon on open ground stood 13.5 m above it), so
/// the beacon is buried. Placement stays as it is until the full ground height is researched (owner decision).
///
/// Ownership: ArtifactX's beacons carry a seed derived from their site (<see cref="SeedFor"/>), so Add and Remove
/// only ever replace or delete beacons ArtifactX placed, never ones the player built in game nearby.
/// </summary>
public static class SaveBeaconService
{
    /// <summary>Height above Core's terrain estimate, matching game-placed beacons on ordinary ground.</summary>
    public const float ClearanceMeters = 0.2f;

    /// <summary>Marks ArtifactX's beacon seeds (the low 16 bits); the rest identifies the site.</summary>
    private const ulong SeedTag = 0xA87F;

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

    /// <summary>The seed ArtifactX gives a beacon at this site: the same site always gets the same seed.</summary>
    public static ulong SeedFor(ulong planetAddress, Vector3 localPos)
    {
        ulong hash = 1469598103934665603UL;
        foreach (char ch in PosKey(planetAddress, localPos))
            hash = (hash ^ ch) * 1099511628211UL;
        return (hash & ~0xFFFFUL) | SeedTag;
    }

    private static bool IsArtifactXBeacon(JObject obj) =>
        NmsSpaceStationPaths.ParsePackedAddress(obj["ofi"]) is ulong seed && (seed & 0xFFFF) == SeedTag;

    /// <summary>The c5s index of ArtifactX's beacon at this site (within 50 m of surface distance), or -1.</summary>
    private static int FindOwnBeacon(JArray objects, ulong planetAddress, Vector3 localPos)
    {
        for (int i = objects.Count - 1; i >= 0; i--)
        {
            if (objects[i] is not JObject obj || obj["r<7"]?.Value<string>() != "^BUILDBEACON" || !IsArtifactXBeacon(obj)) continue;
            if (NmsSpaceStationPaths.ParsePackedAddress(obj["oZw"]) != planetAddress) continue;
            if (obj["wMC"] is not JArray arr || arr.Count != 3) continue;
            var position = new Vector3(arr[0]?.Value<float>() ?? 0, arr[1]?.Value<float>() ?? 0, arr[2]?.Value<float>() ?? 0);
            if (MatchesSite(position, localPos, 50.0f)) return i;
        }
        return -1;
    }

    /// <summary>Checks if ArtifactX's Save Beacon exists in c5s at this planetary site.</summary>
    public static bool IsPlaced(ulong planetAddress, Vector3 localPos) =>
        SaveSessionManager.GetValue(NmsBuildCounts.LooseObjectsPath) is JArray objects
        && FindOwnBeacon(objects, planetAddress, localPos) >= 0;

    /// <summary>Checks if a beacon at this position was added in the current session and is waiting to be saved.</summary>
    public static bool IsUnsaved(ulong planetAddress, Vector3 localPos) =>
        AddedKeys.Contains(PosKey(planetAddress, localPos))
        && SaveSessionManager.HasStagedEdit(NmsBuildCounts.LooseObjectsPath)
        && IsPlaced(planetAddress, localPos);

    /// <summary>Places a Save Beacon in c5s at the given planet-local ground position.</summary>
    public static void Add(ulong planetAddress, Vector3 localPos)
    {
        if (!CanAdd) return;

        var objects = SaveSessionManager.GetValue(NmsBuildCounts.LooseObjectsPath) is JArray current
            ? (JArray)current.DeepClone()
            : new JArray();

        // Replace ArtifactX's own beacon at this site; never touch beacons the player built.
        if (FindOwnBeacon(objects, planetAddress, localPos) is int existing and >= 0)
            objects.RemoveAt(existing);

        Vector3 up = Vector3.Normalize(localPos);
        Vector3 temp = Math.Abs(up.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitZ;
        Vector3 right = Vector3.Normalize(Vector3.Cross(up, temp));
        Vector3 at = Vector3.Normalize(Vector3.Cross(right, up));

        Vector3 beaconPos = localPos + up * ClearanceMeters;

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var beacon = new JObject
        {
            ["b1:"] = now,
            ["r<7"] = "^BUILDBEACON",
            ["oZw"] = $"0x{planetAddress:X}",
            ["ofi"] = $"0x{SeedFor(planetAddress, localPos):X16}",
            ["CVX"] = 0,
            ["wMC"] = new JArray(Math.Round(beaconPos.X, 3), Math.Round(beaconPos.Y, 3), Math.Round(beaconPos.Z, 3)),
            ["wJ0"] = new JArray(Math.Round(up.X, 4), Math.Round(up.Y, 4), Math.Round(up.Z, 4)),
            ["aNu"] = new JArray(Math.Round(at.X, 4), Math.Round(at.Y, 4), Math.Round(at.Z, 4)),
        };

        objects.Add(beacon);
        SaveSessionManager.StageEdit(objects, NmsBuildCounts.LooseObjectsPath);
        AddedKeys.Add(PosKey(planetAddress, localPos));
    }

    /// <summary>Removes ArtifactX's Save Beacon at this site; beacons the player built are left alone.</summary>
    public static void Remove(ulong planetAddress, Vector3 localPos)
    {
        if (!CanRemove || SaveSessionManager.GetValue(NmsBuildCounts.LooseObjectsPath) is not JArray current) return;

        var objects = (JArray)current.DeepClone();
        int index = FindOwnBeacon(objects, planetAddress, localPos);
        if (index < 0) return;

        objects.RemoveAt(index);
        SaveSessionManager.StageEdit(objects, NmsBuildCounts.LooseObjectsPath);
        AddedKeys.Remove(PosKey(planetAddress, localPos));
    }
}
