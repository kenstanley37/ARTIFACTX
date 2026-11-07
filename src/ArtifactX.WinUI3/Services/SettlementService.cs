using ArtifactX.Core.NmsModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>A settlement the player owns: its slot in GQA and its name.</summary>
public sealed record SettlementEntry(int Index, string Name);

/// <summary>
/// Owned settlements. A settlement slot lives in a much bigger array (GQA, 100 entries in
/// the one real save checked) shared with OTHER real players' settlements the save has
/// encountered - occupancy means "has a name AND its Owner matches the local account",
/// not just "has a name" (see NmsSettlementPaths.LocalPlayerOnlineIdPath). Field names
/// were inferred by positional cross-reference against libMBIN's GcSettlementState.
/// </summary>
public static class SettlementService
{
    // Building names from the game (catalog "SettlementBuildingNames"): each type has a
    // pool and every settlement shows one of them. Loaded once for all pages.
    private static Dictionary<string, (string Name, List<string> Names, List<string> Upgraded)> _buildingNames = new();

    public static List<SettlementEntry> ReadOwned()
    {
        var result = new List<SettlementEntry>();
        if (SaveSessionManager.GetValue(NmsSettlementPaths.SettlementArrayPath) is not JArray array) return result;
        string localOnlineId = SaveSessionManager.GetValue(NmsSettlementPaths.LocalPlayerOnlineIdPath)?.Value<string>() ?? "";
        if (localOnlineId.Length == 0) return result;

        for (int i = 0; i < array.Count; i++)
        {
            string name = array[i]?["NKm"]?.Value<string>() ?? "";
            if (name.Length == 0) continue;
            if ((array[i]?["3?K"]?["K7E"]?.Value<string>() ?? "") != localOnlineId) continue;
            result.Add(new SettlementEntry(i, name));
        }
        return result;
    }

    public static async Task LoadBuildingNamesAsync()
    {
        if (_buildingNames.Count == 0) _buildingNames = await CatalogService.GetSettlementBuildingNamesAsync();
    }

    public static string BuildingLabel(string buildingClass) =>
        _buildingNames.TryGetValue(buildingClass, out var n) ? n.Name : buildingClass.Replace("Settlement_", "").Replace('_', ' ');

    /// <summary>"Each settlement names it differently - also: ... Once upgraded: ..."</summary>
    public static string BuildingAliases(string buildingClass)
    {
        if (!_buildingNames.TryGetValue(buildingClass, out var n)) return "";
        var others = n.Names.Where(x => !string.Equals(x, n.Name, StringComparison.OrdinalIgnoreCase)).ToList();
        var parts = new List<string>();
        if (others.Count > 0) parts.Add($"Each settlement names it differently - also: {string.Join(", ", others)}.");
        if (n.Upgraded.Count > 0) parts.Add($"Once upgraded: {string.Join(", ", n.Upgraded)}.");
        return string.Join(" ", parts);
    }

    /// <summary>The game's own stage names (UI_SETTLEMENT_BUILD_STAGE_*), by GcSettlementConstructionLevel.</summary>
    public static string StageLabel(int stage) => stage switch
    {
        0 => "Foundations", 1 => "Basic structure", 2 => "Upper storeys", 3 => "Roof", 4 => "Final fit out",
        5 => "Upgrade 1", 6 => "Upgrade 2", 7 => "Upgrade 3", _ => $"Stage {stage}",
    };

    /// <summary>The building queued for this settlement's next upgrade (NextBuildingUpgradeClass), or "".</summary>
    public static string NextBuilding(int index)
    {
        string next = SaveSessionManager.GetValue(NmsSettlementPaths.SettlementPath(index).Concat(new[] { ":Qn", "iqv" }).ToArray())?.ToString() ?? "";
        return next == "None" ? "" : next;
    }

    public static ulong? Address(int index) =>
        NmsSpaceStationPaths.ParsePackedAddress(SaveSessionManager.GetValue(NmsSettlementPaths.SettlementPath(index).Append("yhJ").ToArray()));

    /// <summary>A perk slot's perk id: "^" prefix and any "#NNNNN" per-instance roll suffix removed.</summary>
    public static string PerkId(string raw)
    {
        string stripped = raw.TrimStart('^');
        int hash = stripped.IndexOf('#');
        return hash >= 0 ? stripped[..hash] : stripped;
    }
}
