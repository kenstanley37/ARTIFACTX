namespace ArtifactX.Core.Universe;

/// <summary>
/// What a trade terminal can stock, from the player's own game data (DefaultReality TradeSettings, the three product
/// tables, the trading class table). Stored in catalog category <c>PlanetGeneration</c> under GameId
/// <c>TRADESTOCK</c>; read by <see cref="StationTradeStock"/>.
/// </summary>
/// <param name="SpaceStation">TradeSettings.SpaceStation: the station trade terminal's lists.</param>
/// <param name="ProductTableEntries">Entries in all three product tables (Main, BaseParts, ModularCustomisation). The
/// trade-goods step draws once per entry not already on sale, so this count is part of the stream: it changes with
/// every game update that adds products.</param>
/// <param name="TradeGoods">The product table entries that step can add, in table order: a trade category a trading
/// class sells (Mineral to Energy), not a building part.</param>
/// <param name="TableProducts">The trade lists' products that are in the product tables (one draw fewer each when on sale).</param>
/// <param name="NeverSellable">DefaultReality NeverSellableItems.</param>
/// <param name="NeverOffered">DefaultReality NeverOfferedForSale.</param>
/// <param name="NeverSoldBySetting">GcDifficultyConfig ItemShopAvailabilityOptionData: what shops never sell under
/// each Item Shop Availability setting ("High", "Normal", "Low"). Normal leaves out Oxygen, Sodium, Sodium Nitrate,
/// Ion Batteries and Life Support Gel.</param>
/// <param name="ClassSells">GcTradeCategory each GcTradingClass sells, in GcTradingClass order.</param>
/// <param name="StandardTypeBTerminals">The standard TypeB station's Shop interactions per launch branch (same shape as
/// the cabinet scenes; <see cref="StationEquipmentScene.Cabinets"/> holds the terminals).</param>
public sealed record TradeStockTables(
    TradeListData SpaceStation,
    int ProductTableEntries,
    TradeGoodEntry[] TradeGoods,
    string[] TableProducts,
    string[] NeverSellable,
    string[] NeverOffered,
    Dictionary<string, string[]> NeverSoldBySetting,
    int[] ClassSells,
    StationEquipmentScene[] StandardTypeBTerminals);

/// <summary>One GcTradeData: fixed and optional items, how many are for sale, and the trade goods count per wealth
/// (GcWealthClass order: Poor, Average, Wealthy, Pirate).</summary>
public sealed record TradeListData(
    TradeListItem[] AlwaysProducts,
    TradeListItem[] AlwaysSubstances,
    TradeListItem[] OptionalProducts,
    TradeListItem[] OptionalSubstances,
    int MinItems,
    int MaxItems,
    float ProductShare,
    int[] MinTradeGoods,
    int[] MaxTradeGoods);

/// <summary>An item of a trade list. <paramref name="Known"/>: the game's product or substance lookup finds it (an
/// unknown id is passed over without a draw). <paramref name="BuildingPart"/>: product category BuildingPart, which
/// the optional pick never sells.</summary>
public sealed record TradeListItem(string Id, bool Known, bool BuildingPart = false);

/// <summary>A product the trade-goods step can add, with its GcTradeCategory value.</summary>
public sealed record TradeGoodEntry(string Id, int TradeCategory);
