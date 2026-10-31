using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using Newtonsoft.Json.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>Evaluates only requested cards, outside the planet cache and UI thread.
/// The normal Exotic scene and both screenshot references were checked 2026-10-31.
/// Catalog-confirmed normal models share the pipeline; food effects, special
/// inventory categories and seasonal module rolls remain guarded.
/// Uses the loaded save's active ship; live comparison caching is not established.</summary>
internal static class CrashedShipDisplayStatsService
{
    public static async Task<IReadOnlyList<CrashedShipSite>> ApplyAsync(IReadOnlyList<CrashedShipSite> sites)
    {
        if (!sites.Any(s => s.Ship is not null && !s.Sentinel)) return sites;
        var identity = SaveSessionManager.SessionIdentity;
        var player = new JObject();
        foreach (string key in new[] { "DtI", "4kj", "nsH" })
            if (SaveSessionManager.GetValue("vLc", "6f=", key) is { } value) player[key] = value.DeepClone();
        if (SaveSessionManager.GetValue("vLc", "6f=", "aBE") is not { Type: JTokenType.Integer } index
            || SaveSessionManager.GetValue("vLc", "6f=", "@Cs") is not JArray ships
            || index.Value<int>() < 0 || index.Value<int>() >= ships.Count
            || ships[index.Value<int>()] is not JObject source) return sites;
        var ship = (JObject)source.DeepClone();
        var rules = await CatalogService.GetStarshipEvaluationTablesAsync();
        var inventory = await CatalogService.GetInventoryGenerationTablesAsync();
        if (rules is null || inventory?.ShipLayouts is null) return sites;
        var result = await Task.Run(() =>
        {
            if (NmsStarshipComparisonPaths.Read(ship, rules) is not { } context) return sites;
            return sites.Select(site =>
            {
                if (site.Ship is not { } wreck || site.Inventory is not { } summary
                    || site.Sentinel || site.ShownClass is not { } rating
                    || !StarshipDisplayEvaluation.SupportsNormalModel(wreck.Type, inventory)) return site;
                var (code, galaxy) = UniverseInfo.Generator(site.PlanetAddress & ~(0xFUL << 52));
                var system = SystemGenerator.Attributes(code, galaxy);
                int wealth = system.Pirate ? 3 : System.Math.Clamp(system.Wealth - 1, 0, 2);
                if (NmsMultiToolGenerationPaths.ReadStandardGameState(player, wealth) is not { HasActiveSpecialTechnology: false } runtime
                    || inventory.ShipLayouts.FirstOrDefault(l => l.SizeType == summary.SizeType) is not { } size
                    || StarshipInventoryGeneration.TechLayout(site.Key, size) is not { } layout) return site;
                KeyValuePair<string, float>[] bonuses = [new("SHIP_DAMAGE", summary.DamageBonus),
                    new("SHIP_SHIELD", summary.ShieldBonus), new("SHIP_HYPERDRIVE", summary.HyperdriveBonus),
                    new("SHIP_AGILE", summary.ManeuverabilityBonus)];
                var stats = StarshipDisplayEvaluation.Evaluate(site.Key, wreck.Seed, (int)wreck.Type, rating, layout,
                    bonuses, rules, context, wealth, runtime.RequiredLevel, runtime.KnownTechnologies, false);
                return site with { DisplayedStats = stats };
            }).ToArray();
        });
        return ReferenceEquals(identity, SaveSessionManager.SessionIdentity) ? result : sites;
    }
}
