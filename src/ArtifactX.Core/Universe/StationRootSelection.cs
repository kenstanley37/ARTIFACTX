namespace ArtifactX.Core.Universe;

/// <summary>Station resource dispatch, separate from procedural descriptor selection.</summary>
public static class StationRootSelection
{
    /// <summary>
    /// Resolves only the native predicate's unconditional early returns: the catalog's
    /// debug override forces legacy; inactive seasonal rules or a disabled seasonal
    /// legacy-station flag select standard. Active seasonal legacy rules still require
    /// runtime universe-address/distance checks and yield null here.
    /// </summary>
    public static bool? ResolveLegacy(StationEquipmentTables tables, bool? seasonalRulesActive,
        bool? useLegacyStationsNearStart = null)
    {
        if (tables.ForceLegacySpaceStations is not { } forced) return null;
        if (forced) return true;
        if (seasonalRulesActive is false || useLegacyStationsNearStart is false) return false;
        return null;
    }

    /// <summary>
    /// Ports the root priority at NMS build 180836's 0x1416377CA and 0x14163C5E6:
    /// abandoned, pirate, legacy, then standard. The legacy predicate is runtime state
    /// (0x1416434D0), not a system-address property; unknown must remain null.
    /// Station presence must be established separately by the caller.
    /// </summary>
    public static string? Select(StationSceneRoots roots, bool abandoned, bool pirate, bool? useLegacy)
    {
        string? root = abandoned ? roots.Abandoned : pirate ? roots.Pirate
            : useLegacy is true ? roots.Legacy : useLegacy is false ? roots.Standard : null;
        return string.IsNullOrWhiteSpace(root) ? null : root;
    }
}
