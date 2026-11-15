using System.Globalization;
using ArtifactX.Core.Universe;
using Newtonsoft.Json.Linq;

namespace ArtifactX.Core.NmsModels;

/// <summary>Read-only comparison context from one owned ship. Slot 8 inspection on
/// 2026-10-31 confirmed NTx/@EL, PMT slot positions, damage, special cells and cargo
/// intrinsic bonuses against both native reference loadouts. Unsupported equipment
/// returns no result rather than silently omitting its contribution.</summary>
public static class NmsStarshipComparisonPaths
{
    /// <summary>
    /// The native GcSpaceshipClasses index (Dropship 1, Fighter 2, Scientific 3, Shuttle 4, Royal 6, Alien 7,
    /// Sail 8, Robot 9) for a saved ship's model scene (NTx.93M), which the displayed-stat flight rules are
    /// keyed by. Folder names checked on Slot 8 on 2026-11-14 (Living Ship = S-CLASS/BIOPARTS/BIOSHIP_PROC,
    /// so it is tested before the Exotic S-CLASS folder). Corvettes (BIGGS) return null: their technology
    /// lives on the built parts, which this evaluator doesn't read.
    /// </summary>
    public static int? NativeShipClass(string? scenePath)
    {
        if (string.IsNullOrEmpty(scenePath)) return null;
        bool Has(string folder) => scenePath.Contains(folder, StringComparison.OrdinalIgnoreCase);
        if (Has("SPACECRAFT/DROPSHIPS/")) return 1;
        if (Has("SPACECRAFT/FIGHTERS/")) return 2;
        if (Has("SPACECRAFT/SCIENTIFIC/")) return 3;
        if (Has("SPACECRAFT/SHUTTLE/")) return 4;
        if (Has("SPACECRAFT/S-CLASS/BIOPARTS/")) return 7;
        if (Has("SPACECRAFT/S-CLASS/")) return 6;
        if (Has("SPACECRAFT/SAILSHIP/")) return 8;
        if (Has("SPACECRAFT/SENTINELSHIP/")) return 9;
        return null;
    }

    public static StarshipComparisonContext? Read(JObject? ship, StarshipEvaluationTables rules)
    {
        if (ship?["NTx"]?["@EL"] is not JArray seed || seed.Count != 2
            || !ulong.TryParse(seed[1]?.Value<string>()?.Replace("0x", "", StringComparison.OrdinalIgnoreCase),
                NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong resource)
            || ship[";l5"] is not JObject cargo) return null;
        // Cargo technology requires the native dual-inventory evaluator.
        if (cargo[":No"] is not JArray cargoSlots
            || cargoSlots.Any(s => s["Vn8"]?["elv"]?.Value<string>() == "Technology")) return null;
        // The game reads the Cargo copy of the stat bonuses (NmsInventoryContainer.ShipStatBonusesPath).
        return NmsTechnologyInventoryPaths.ReadTechnology(ship["PMT"] as JObject, rules.Technology) is { } tech
            && NmsTechnologyInventoryPaths.ReadBonuses(cargo["@bB"]) is { } intrinsic
            ? new(resource, tech.Inventory, intrinsic, tech.DamagedPositions) : null;
    }
}
