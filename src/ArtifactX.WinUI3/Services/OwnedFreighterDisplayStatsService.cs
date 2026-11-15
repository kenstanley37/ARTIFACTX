using System.Collections.Generic;
using System.Threading.Tasks;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using Newtonsoft.Json.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// The Hyperdrive Range and Fleet Coordination the game's freighter stats panel shows (#17): the freighter's
/// technology (0wS) with its Cargo copy of the stat bonuses (8ZP.@bB, the copy the game reads - see
/// NmsInventoryContainer.FreighterStatBonusesPath) through FreighterDisplayEvaluation. Reads with staged edits.
/// Null on a catalog older than format 97 (no freighter stat mapping) or an unreadable inventory.
/// </summary>
internal static class OwnedFreighterDisplayStatsService
{
    /// <summary>The totals keyed like <see cref="StatBonusService.FreighterStats"/> ("^FREI_HYPERDRIVE", "^FREI_FLEET").</summary>
    public static async Task<IReadOnlyDictionary<string, double>?> EvaluateAsync()
    {
        if (SaveSessionManager.GetValue(NmsInventoryContainer.FreighterTechnologyPath) is not JObject techSource
            || SaveSessionManager.GetValue(NmsInventoryContainer.FreighterStatBonusesPath) is not JArray bonusSource) return null;
        var tech = (JObject)techSource.DeepClone();
        var bonusArray = (JArray)bonusSource.DeepClone();
        if (await CatalogService.GetStarshipEvaluationTablesAsync() is not { Freighter: not null } rules) return null;
        return await Task.Run<IReadOnlyDictionary<string, double>?>(() =>
            NmsTechnologyInventoryPaths.ReadTechnology(tech, rules.Technology) is { } inventory
            && NmsTechnologyInventoryPaths.ReadBonuses(bonusArray) is { } bonuses
            && FreighterDisplayEvaluation.Evaluate(inventory.Inventory, inventory.DamagedPositions, bonuses, rules) is { } stats
                ? new Dictionary<string, double> { ["^FREI_HYPERDRIVE"] = stats.Hyperdrive, ["^FREI_FLEET"] = stats.FleetCoordination }
                : null);
    }
}
