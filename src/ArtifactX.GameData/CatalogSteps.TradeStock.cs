using ArtifactX.Core.Universe;
using libMBIN;
using libMBIN.NMS;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS.Globals;

namespace ArtifactX.GameData;

public static partial class CatalogSteps
{
    private const string RealityFile = "METADATA/REALITY/DEFAULTREALITY.MBIN";
    private const string TradingClassFile = "METADATA/REALITY/TABLES/TRADINGCLASSDATATABLE.MBIN";

    /// <summary>
    /// What the station trade terminal can stock (Core StationTradeStock; docs/research/2026-11-24-STATION-TRADE-STOCK.md):
    /// TradeSettings.SpaceStation, the three product tables' size and trade goods in table order, the never-sold lists,
    /// what each trading class sells, and the standard TypeB station's Shop interaction per launch branch. Null when a
    /// file is missing or the station root isn't the verified TypeB scene.
    /// </summary>
    private static TradeStockTables? TradeStock(Func<string, NMSTemplate?> decode)
    {
        if (decode(RealityFile) is not GcRealityManagerData reality
            || decode(NormalizeScenePath(reality.SubstanceTable.Value)) is not GcSubstanceTable substanceTable
            || decode(TradingClassFile) is not GcTradingClassTable classes
            || decode(SimulationGlobalsFile) is not GcSimulationGlobals simulation)
            return null;

        var products = new List<GcProductData>();
        foreach (var file in reality.ProductTables)
        {
            // ModularCustomisation is named ".TXT" in the reality file; the PAK holds it as an MBIN like the others.
            string path = Path.ChangeExtension(NormalizeScenePath(file.Value), ".MBIN");
            if (decode(path) is not GcProductTable table) return null;
            products.AddRange(table.Table);
        }
        var productsById = products.GroupBy(p => p.ID.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var substanceIds = substanceTable.Table.Select(s => s.ID.Value).ToHashSet(StringComparer.Ordinal);

        TradeListItem Product(NMSString0x10 id) => new(id.Value, productsById.TryGetValue(id.Value, out var p),
            p?.Category.SubstanceCategory == GcRealitySubstanceCategory.SubstanceCategoryEnum.BuildingPart);
        TradeListItem Substance(NMSString0x10 id) => new(id.Value, substanceIds.Contains(id.Value));
        var station = reality.TradeSettings.SpaceStation;
        var list = new TradeListData(
            station.AlwaysPresentProducts.Select(Product).ToArray(),
            station.AlwaysPresentSubstances.Select(Substance).ToArray(),
            station.OptionalProducts.Select(Product).ToArray(),
            station.OptionalSubstances.Select(Substance).ToArray(),
            station.MinItemsForSale, station.MaxItemsForSale, station.PercentageOfItemsAreProducts,
            station.MinExtraSystemProducts.ToArray(), station.MaxExtraSystemProducts.ToArray());

        // Only the categories a trading class sells can be picked (None and SpecialShop never are), and the step
        // passes over building parts.
        var sold = classes.TradingClassesData.Select(c => (int)c.Sells.TradeCategory).ToHashSet();
        var goods = products
            .Where(p => sold.Contains((int)p.TradeCategory.TradeCategory)
                && p.Category.SubstanceCategory != GcRealitySubstanceCategory.SubstanceCategoryEnum.BuildingPart)
            .Select(p => new TradeGoodEntry(p.ID.Value, (int)p.TradeCategory.TradeCategory))
            .ToArray();
        var listed = list.AlwaysProducts.Concat(list.OptionalProducts).Select(i => i.Id)
            .Where(productsById.ContainsKey).Distinct().ToArray();

        string root = NormalizeScenePath(simulation.SpaceStationFile?.Value ?? "");
        if (!root.Equals(VerifiedStandardTypeBRoot, StringComparison.OrdinalIgnoreCase)) return null;
        var terminals = new[] { "_AttachPoint_LaunchTubeA", "_AttachPoint_LaunchTubeB", "_AttachPoint_LaunchTubeC" }
            .Select(reference => ExtractStationBranch(root, reference, decode, interaction: GcInteractionType.InteractionTypeEnum.Shop))
            .OfType<StationEquipmentScene>().ToArray();
        if (terminals.Length == 0) return null;

        return new TradeStockTables(list, products.Count, goods, listed,
            reality.NeverSellableItems.Select(i => i.Value).ToArray(),
            reality.NeverOfferedForSale.Select(i => i.Value).ToArray(),
            classes.TradingClassesData.Select(c => (int)c.Sells.TradeCategory).ToArray(),
            terminals);
    }
}
