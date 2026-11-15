using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using Newtonsoft.Json.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// The Damage Potential and Scanner Range the game shows for an owned multi-tool (#17): its saved technology,
/// module rolls, supercharged slots, adjacency and stat bonuses through the evaluators that reproduce the
/// station cabinet tools' displayed values (MultiToolDamageGeneration.StandardPotential, standard OVERHEAT
/// animation; MultiToolScannerGeneration.Range). Reads the tool with staged edits. Mining has no displayed total
/// here (not researched). Null when the catalog lacks the tables, a technology can't be read, or a technology is
/// damaged (the damage evaluator supports intact inventories only). Not yet checked in game on owned tools.
/// </summary>
internal static class OwnedMultiToolDisplayStatsService
{
    /// <summary>The totals keyed like <see cref="StatBonusService.MultiToolStats"/> ("^WEAPON_DAMAGE", "^WEAPON_SCAN").</summary>
    public static async Task<IReadOnlyDictionary<string, double>?> EvaluateAsync(int toolIndex)
    {
        if (toolIndex < 0 || SaveSessionManager.GetValue(NmsInventoryContainer.MultiToolTechnologyPath(toolIndex)) is not JObject source)
            return null;
        var store = (JObject)source.DeepClone();
        if (await CatalogService.GetMultiToolTechnologyTablesAsync() is not { } tables) return null;
        return await Task.Run<IReadOnlyDictionary<string, double>?>(() =>
        {
            if (NmsTechnologyInventoryPaths.ReadTechnology(store, tables) is not { } tech
                || NmsTechnologyInventoryPaths.ReadBonuses(store["@bB"]) is not { } bonuses) return null;
            var intrinsic = bonuses.GroupBy(b => b.Key).ToDictionary(g => g.Key, g => g.First().Value);
            var totals = new Dictionary<string, double>();
            if (tech.DamagedPositions.Count == 0 && MultiToolDamageGeneration.StandardPotential(tech.Inventory, tables, intrinsic) is { } damage)
                totals["^WEAPON_DAMAGE"] = damage;
            if (intrinsic.TryGetValue("WEAPON_SCAN", out float scan) && MultiToolScannerGeneration.Range(tech.Inventory, tables, scan) is { } range)
                totals["^WEAPON_SCAN"] = range;
            return totals.Count == 0 ? null : totals;
        });
    }
}
