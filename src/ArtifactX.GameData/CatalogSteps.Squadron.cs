using System.Text.Json;
using ArtifactX.Core.Squadron;
using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;
using System.Text.RegularExpressions;
using ArtifactX.Core.Universe;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS.Globals;
using libMBIN.NMS.Toolkit;

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
        var titles = new Regex($"^({string.Join("|", NpcNames.TitlePrefixes)})_TITLE_\\d+$", RegexOptions.IgnoreCase);
        var words = GetMergedEnglishLookup(pcbanksPath)
            .Where(kv => kv.Key.StartsWith("UI_SQUADRON_FACT_", StringComparison.OrdinalIgnoreCase)
                         || kv.Key.StartsWith("UI_SQUADRON_SHIP_FACT", StringComparison.OrdinalIgnoreCase)
                         || titles.IsMatch(kv.Key) || kv.Key.Equals("NPC_NAME_LAYOUT", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(kv => kv.Key.ToUpperInvariant(), kv => kv.Value);

        // The pilot name's race: the game reads it from the NPC scene's GcNPCComponentData (NMS.exe 184271 0x140604b10).
        var sceneRaces = new Dictionary<string, int>();
        foreach (var file in globals.PlayerSquadronConfig.RandomPilotNPCResources ?? new())
        {
            string scene = file.Value?.Replace('\\', '/').ToUpperInvariant() ?? "";
            if (scene.Length > 0 && NpcRace(scene) is int race) sceneRaces[scene] = race;
        }
        var rules = new SquadronFactRules(ranges.Select(r => r.X).ToArray(), ranges.Select(r => r.Y).ToArray(), words, sceneRaces);

        using var db = new CatalogDbContext(catalogPath);
        Replace(db, "SquadronFacts", "GcPlayerSquadronConfig", "gcaispaceshipglobals.global.mbin", new List<CatalogItem>
        {
            new CatalogItem { GameId = "SQUADRONFACTS", NameEnglish = "Squadron pilot stat card rules", DescriptionEnglish = JsonSerializer.Serialize(rules) }
        });
        ConsoleStyle.Success($"Wrote squadron pilot rules ({ranges.Length} ranks, {words.Count} words, {sceneRaces.Count} pilot races) to {catalogPath}.");

        // The first GcNPCComponentData in the scene's ATTACHMENT entities.
        int? NpcRace(string scenePath)
        {
            if (DecodeMbin(pcbanksPath, scenePath) is not TkSceneNodeData root) return null;
            var stack = new Stack<TkSceneNodeData>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                foreach (var attr in node.Attributes ?? new())
                {
                    if (attr.Name?.Value != "ATTACHMENT" || attr.Value?.Value is not { Length: > 0 } entity) continue;
                    if (DecodeMbin(pcbanksPath, entity.Replace('\\', '/').ToUpperInvariant()) is not TkAttachmentData data) continue;
                    foreach (var component in data.Components ?? new())
                        if (component.Template is GcNPCComponentData npc) return (int)npc.Race.AlienRace;
                }
                foreach (var child in node.Children ?? new()) stack.Push(child);
            }
            return null;
        }
    }
}
