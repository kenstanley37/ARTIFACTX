using System.Text.Json;
using ArtifactX.Core.Squadron;
using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;
using libMBIN.NMS.Globals;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    /// <summary>
    /// The squadron pilot stat card's rules (format 110, Core SquadronFactRules, row SQUADRONFACTS): the per-rank grade
    /// ranges from gcaispaceshipglobals.global.mbin and the text of the UI_SQUADRON_FACT_* and UI_SQUADRON_SHIP_FACT* keys.
    /// </summary>
    public static void AddSquadronFacts(string catalogPath, string pcbanksPath)
    {
        if (DecodeMbin(pcbanksPath, "GCAISPACESHIPGLOBALS.GLOBAL.MBIN") is not GcAISpaceshipGlobals globals
            || globals.PlayerSquadronConfig?.PilotRankTraitRanges is not { Length: > 0 } ranges)
        {
            ConsoleStyle.Warning("Squadron config not found - pilot stat cards left out.");
            return;
        }
        var words = GetMergedEnglishLookup(pcbanksPath)
            .Where(kv => kv.Key.StartsWith("UI_SQUADRON_FACT_", StringComparison.OrdinalIgnoreCase)
                         || kv.Key.StartsWith("UI_SQUADRON_SHIP_FACT", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(kv => kv.Key.ToUpperInvariant(), kv => kv.Value);
        var rules = new SquadronFactRules(ranges.Select(r => r.X).ToArray(), ranges.Select(r => r.Y).ToArray(), words);

        using var db = new CatalogDbContext(catalogPath);
        Replace(db, "SquadronFacts", "GcPlayerSquadronConfig", "gcaispaceshipglobals.global.mbin", new List<CatalogItem>
        {
            new CatalogItem { GameId = "SQUADRONFACTS", NameEnglish = "Squadron pilot stat card rules", DescriptionEnglish = JsonSerializer.Serialize(rules) }
        });
        ConsoleStyle.Success($"Wrote squadron pilot rules ({ranges.Length} ranks, {words.Count} words) to {catalogPath}.");
    }
}
