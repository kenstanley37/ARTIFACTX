using System.Text.Json;
using ArtifactX.Core.Frigates;
using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;
using libMBIN;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS.Toolkit;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    private const string AiSpaceshipManagerFile = "METADATA/SIMULATION/SPACE/AISPACESHIPMANAGER.MBIN";

    /// <summary>
    /// Each frigate class's model for the Frigates page's 3D preview (format 109, Core FrigateModel): the Frigate-role
    /// entries of aispaceshipmanager.mbin's ShipModels (build 185034: Combat COMBATFRIGATE, Exploration SCIENCEFRIGATE,
    /// Mining INDUSTRIALFRIGATE, Diplomacy DIPLOMATICFRIGATE, Support SUPPORTFRIGATE, Normandy NORMANDYFRIGATE,
    /// DeepSpace LIVINGFRIGATE, DeepSpaceCommon LIVINGFRIGATE_PROC, Pirate PIRATECRUISER, GhostShip GHOSTSHIPFRIGATE,
    /// Swarm SWARMFRIGATE), each with the LOD scene its TkLODComponentData lists second. One row, FRIGATEMODELS.
    /// </summary>
    public static void AddFrigateModels(string catalogPath, string pcbanksPath)
    {
        if (DecodeMbin(pcbanksPath, AiSpaceshipManagerFile) is not GcAISpaceshipManagerData manager)
        {
            ConsoleStyle.Warning($"{AiSpaceshipManagerFile} not found - frigate previews left out.");
            return;
        }
        var models = new List<FrigateModel>();
        foreach (var m in manager.ShipModels?.Elements ?? new())
        {
            if (m.AIRole?.AIShipRole != GcAISpaceshipRoles.AIShipRoleEnum.Frigate || m.Filename?.Value is not { Length: > 0 } file) continue;
            int frigateClass = (int)m.FrigateClass.FrigateClass;
            if (models.Any(x => x.Class == frigateClass)) continue;
            string scene = file.Replace('\\', '/').ToUpperInvariant();
            models.Add(new FrigateModel(frigateClass, scene, PreviewLod(scene) ?? scene));
        }

        using var db = new CatalogDbContext(catalogPath);
        Replace(db, "FrigateModels", "GcAISpaceshipManagerData", AiSpaceshipManagerFile.ToLowerInvariant(), new List<CatalogItem>
        {
            new CatalogItem { GameId = "FRIGATEMODELS", NameEnglish = "Frigate models by class", DescriptionEnglish = JsonSerializer.Serialize(models) }
        });
        ConsoleStyle.Success($"Wrote {models.Count} frigate models to {catalogPath}.");

        // The second LODModel of the first TkLODComponentData in the scene's ATTACHMENT entities.
        string? PreviewLod(string scenePath)
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
                        if (component.Template is TkLODComponentData { LODModels.Count: > 0 } lod)
                        {
                            var pick = lod.LODModels[Math.Min(1, lod.LODModels.Count - 1)];
                            if (pick.LODModel?.Filename?.Value is { Length: > 0 } lodScene) return lodScene.Replace('\\', '/').ToUpperInvariant();
                        }
                }
                foreach (var child in node.Children ?? new()) stack.Push(child);
            }
            return null;
        }
    }
}
