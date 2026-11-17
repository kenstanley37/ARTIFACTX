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
/// tables, or for a loadout the evaluator can't read (cargo technology, high-grade modules).
/// Corvettes (ship class 10): Shield and Hyperdrive match the game exactly on Slot 8's corvette (394.9 / 1344.0,
/// flown, 2026-11-15; catalog format 102). Maneuverability is the ordinary value damped by the build's size
/// (ShipBaseService.AssembleAsync, StarshipDisplayStatGeneration.CorvetteManeuverability; catalog format 103):
/// with the size the running game measured that's 454.66 (game 454.7); the assembled size gives 454.81 - the
/// magnet's animated tractor-beam cone sets the height (CorvetteAssembly); the game's own reading drifts with that
/// animation (454.7 on 2026-11-15, 453.3 on 2026-11-17, build unchanged). Damage matches the game's 220.6 once corvette
/// weapons skip adjacency (StarshipDisplayEvaluation, controlled in-game test 2026-11-17; issue #73).
/// </summary>
internal static class OwnedShipDisplayStatsService
{
    private const int CorvetteClass = 10;   // GcSpaceshipClasses.Corvette

    /// <summary>The totals keyed like <see cref="StatBonusService.ShipStats"/> ("^SHIP_DAMAGE" ...).</summary>
    public static async Task<IReadOnlyDictionary<string, double>?> EvaluateAsync(int shipIndex)
    {
        if (shipIndex < 0 || SaveSessionManager.GetValue("vLc", "6f=", "@Cs", shipIndex.ToString()) is not JObject source)
            return null;
        string? scene = source["NTx"]?["93M"]?.Value<string>();
        bool corvette = ShipBaseService.IsCorvetteModel(scene);
        if ((corvette ? CorvetteClass : NmsStarshipComparisonPaths.NativeShipClass(scene)) is not { } shipClass)
            return null;
        int rating = StarshipService.ReadClass(shipIndex) is { Length: 1 } letter ? "CBAS".IndexOf(letter[0]) : -1;
        if (rating < 0) return null;
        var ship = (JObject)source.DeepClone();
        if (await CatalogService.GetStarshipEvaluationTablesAsync() is not { } rules) return null;
        var build = corvette ? await ShipBaseService.AssembleAsync(shipIndex) : null;
        return await Task.Run<IReadOnlyDictionary<string, double>?>(() =>
            NmsStarshipComparisonPaths.Read(ship, rules) is { } context
            && StarshipDisplayEvaluation.EvaluateOwned(context, shipClass, rating, rules) is { } stats
                ? corvette
                    ? CorvetteStats(stats, build, rules)
                    : new Dictionary<string, double>
                    {
                        ["^SHIP_DAMAGE"] = stats.Damage,
                        ["^SHIP_SHIELD"] = stats.Shield,
                        ["^SHIP_HYPERDRIVE"] = stats.Hyperdrive,
                        ["^SHIP_AGILE"] = stats.Maneuverability,
                    }
                : null);
    }

    private static Dictionary<string, double> CorvetteStats(StarshipDisplayedStats stats,
        ArtifactX.GameData.CorvetteBuild? build, StarshipEvaluationTables rules)
    {
        var result = new Dictionary<string, double>
        {
            ["^SHIP_DAMAGE"] = stats.Damage, ["^SHIP_SHIELD"] = stats.Shield, ["^SHIP_HYPERDRIVE"] = stats.Hyperdrive,
        };
        if (build is not null && StarshipDisplayStatGeneration.CorvetteManeuverability(stats.Maneuverability,
                build.Size.X, build.Size.Y, build.Size.Z, rules.CorvetteSize) is { } agile)
            result["^SHIP_AGILE"] = agile;
        return result;
    }
}
