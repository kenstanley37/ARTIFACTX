using System.Collections.Generic;
using System.Threading.Tasks;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using Newtonsoft.Json.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// The four totals the game shows for an owned starship (#17): base stats plus installed technology,
/// supercharged slots and adjacency, through the same evaluator as a wreck's Compare card
/// (<see cref="StarshipDisplayEvaluation.EvaluateOwned"/>). Reads the ship with staged edits, so the
/// totals follow tech and base-stat edits before saving. Null when the catalog predates the display
/// tables, for Corvettes, or for a loadout the evaluator can't read (cargo technology, high-grade modules).
/// </summary>
internal static class OwnedShipDisplayStatsService
{
    /// <summary>The totals keyed like <see cref="StatBonusService.ShipStats"/> ("^SHIP_DAMAGE" ...).</summary>
    public static async Task<IReadOnlyDictionary<string, double>?> EvaluateAsync(int shipIndex)
    {
        if (shipIndex < 0
            || SaveSessionManager.GetValue("vLc", "6f=", "@Cs", shipIndex.ToString()) is not JObject source
            || NmsStarshipComparisonPaths.NativeShipClass(source["NTx"]?["93M"]?.Value<string>()) is not { } shipClass)
            return null;
        int rating = StarshipService.ReadClass(shipIndex) is { Length: 1 } letter ? "CBAS".IndexOf(letter[0]) : -1;
        if (rating < 0) return null;
        var ship = (JObject)source.DeepClone();
        if (await CatalogService.GetStarshipEvaluationTablesAsync() is not { } rules) return null;
        return await Task.Run<IReadOnlyDictionary<string, double>?>(() =>
            NmsStarshipComparisonPaths.Read(ship, rules) is { } context
            && StarshipDisplayEvaluation.EvaluateOwned(context, shipClass, rating, rules) is { } stats
                ? new Dictionary<string, double>
                {
                    ["^SHIP_DAMAGE"] = stats.Damage,
                    ["^SHIP_SHIELD"] = stats.Shield,
                    ["^SHIP_HYPERDRIVE"] = stats.Hyperdrive,
                    ["^SHIP_AGILE"] = stats.Maneuverability,
                }
                : null);
    }
}
