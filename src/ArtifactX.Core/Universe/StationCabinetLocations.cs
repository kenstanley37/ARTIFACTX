using System.Numerics;

namespace ArtifactX.Core.Universe;

/// <summary>An interaction origin and its position hash; does not claim an item or inventory.</summary>
public sealed record StationCabinetLocation(StationCabinetDefinition Definition, Vector3 WorldPosition, ulong SiteSeed);

/// <summary>Transforms extracted station cabinet origins through a generated station pose.</summary>
public static class StationCabinetLocations
{
    /// <summary>
    /// Resolves supported geometry only after station presence and the native root have
    /// been established. Unsupported roots and an unknown legacy predicate yield no sites.
    /// </summary>
    public static IReadOnlyList<StationCabinetLocation> Locate(StationEquipmentTables tables,
        StationPose pose, bool stationPresent, bool abandoned, bool pirate, bool? useLegacy)
    {
        if (!stationPresent || abandoned) return [];
        if (pirate) return Transform(StationSceneSelection.SelectPirate(tables, pose.Seed), pose);
        if (tables.ForceLegacySpaceStations is true
            || useLegacy is not false || !string.Equals(
                StationRootSelection.Select(tables.StationRoots, abandoned, pirate, useLegacy),
                tables.StationRoots.Standard, StringComparison.OrdinalIgnoreCase)) return [];
        return LocateStandardTypeB(tables, pose);
    }

    /// <summary>
    /// Caller must establish that this is a standard TypeB station. Other roots must not
    /// use this method. Missing branch selection or invalid geometry yields no locations.
    /// Position hashes for both reference cabinets were checked against the purchased
    /// inventory seed and screenshot references on 2026-10-25 using selected LaunchTubeC.
    /// </summary>
    public static IReadOnlyList<StationCabinetLocation> LocateStandardTypeB(StationEquipmentTables tables, StationPose pose)
    {
        return Transform(StationSceneSelection.SelectStandardTypeB(tables, pose.Seed), pose);
    }

    private static IReadOnlyList<StationCabinetLocation> Transform(StationEquipmentScene? scene, StationPose pose)
    {
        if (scene is null || !Finite(pose.Position) || !Finite(pose.Facing)
            || pose.Facing.LengthSquared() < 1.52587890625e-05f) return [];
        Vector3 forward = Vector3.Normalize(pose.Facing);
        Vector3 cross = Vector3.Cross(Vector3.UnitY, forward);
        if (cross.LengthSquared() < 1.52587890625e-05f) return [];
        Vector3 right = Vector3.Normalize(cross);
        Vector3 up = Vector3.Cross(forward, right);
        var result = new List<StationCabinetLocation>();
        foreach (var cabinet in scene.Cabinets)
        {
            if (cabinet.InteractionType != "WeaponTerminal" || cabinet.LocalPosition.Length != 3
                || cabinet.LocalPosition.Any(v => !float.IsFinite(v))) return [];
            var local = cabinet.LocalPosition;
            Vector3 world = local[0] * right + local[1] * up + local[2] * forward + pose.Position;
            if (!Finite(world)) return [];
            result.Add(new StationCabinetLocation(cabinet, world, MultiToolSiteGeneration.SeedForPosition(world)));
        }
        return result;
    }

    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
