using System.Text.Json;
using ArtifactX.Core.Inventory;
using ArtifactX.GameData.Data;
using ArtifactX.GameData.Models;
using ArtifactX.GameData.Services;
using libMBIN.NMS.GameComponents;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    private const string DifficultyConfigFile = "METADATA/GAMESTATE/DIFFICULTYCONFIG.MBIN";

    /// <summary>
    /// The game's inventory stack limits (catalog format 86, Bug 78): GcDifficultyConfig.InventoryStackLimitsOptionData,
    /// one entry per stack-limit setting (GcInventoryStackLimitsDifficultyOption: High, Normal, Low), each with a
    /// product and a substance stack size per inventory stack group (GcInventoryStackSizeGroup: Default, Personal ...
    /// SeasonTransfer). Build 185034: High = products Personal 10, Ship 10, Freighter 20, Chest 20, BaseCapsule 100,
    /// substances 9999; Normal and Low lower. Read by the app's InventoryStackLimitService; see Core
    /// InventoryStackLimits for how a full stack is worked out and the save evidence.
    /// </summary>
    public static void AddStackLimits(string catalogPath, string pcbanksPath)
    {
        var rows = new List<CatalogItem>();
        if (DecodeMbin(pcbanksPath, DifficultyConfigFile) is GcDifficultyConfig config && config.InventoryStackLimitsOptionData is { Length: > 0 } data)
        {
            string[] settings = Enum.GetNames<GcInventoryStackLimitsDifficultyOption.InventoryStackLimitsDifficultyEnum>();
            var tables = new InventoryStackLimitTables(
                Enum.GetNames<GcInventoryStackSizeGroup.InventoryStackSizeGroupEnum>(),
                data.Select((o, i) => new InventoryStackLimitOption(i < settings.Length ? settings[i] : $"Option{i}",
                    o.MaxProductStackSizes ?? [], o.MaxSubstanceStackSizes ?? [], o.ProductStackLimit, o.SubstanceStackLimit)).ToArray());
            rows.Add(new CatalogItem
            {
                GameId = "INVENTORYSTACKLIMITS",
                NameEnglish = "Inventory stack limits",
                DescriptionEnglish = JsonSerializer.Serialize(tables),
            });
            ConsoleStyle.Success($"Inventory stack limits: {tables.Options.Length} settings x {tables.Groups.Length} groups.");
        }
        else ConsoleStyle.Warning("Inventory stack limits not found - the app keeps its fixed stack sizes.");
        using var db = new CatalogDbContext(catalogPath);
        Replace(db, "InventoryStackLimits", "DifficultyConfig", DifficultyConfigFile.ToLowerInvariant(), rows);
    }
}
