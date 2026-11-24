namespace ArtifactX.Core.Universe;

/// <summary>How an item got onto a trade terminal: the list's fixed items, its random pick, or the system's trade goods.</summary>
public enum TradeStockKind { Product, Substance, TradeGood }

/// <summary>One item a trade terminal sells.</summary>
public sealed record TradeStockItem(string Id, TradeStockKind Kind, bool Always);

/// <summary>
/// What a space station's trade terminal sells (issue #9), generated as the game does it. Traced in NMS build 184271
/// (Ghidra, 2026-11-24; docs/research/2026-11-24-STATION-TRADE-STOCK.md):
/// <list type="bullet">
/// <item>The terminal is the station scene's Shop interaction (TERMINALSHOP.SCENE). Its seed is the hash of its world
/// position (<see cref="MultiToolSiteGeneration.SeedForPosition"/>, as for the cabinets); 0x14085d720 passes
/// Mix64 of it to the stock routine 0x14087b4d0, which starts the usual stream from it.</item>
/// <item>0x14087ae50 picks the list: TradeSettings.SpaceStation for a station terminal.</item>
/// </list>
/// Checked against 170 station terminals read off the game's screen in October and November 2026: every captured item
/// of 159 is generated; the other 11 captures carry planet trading-post items. Pirate stations (SmugglerStation plus
/// PirateStationExtraProds) are not ported. 0x140887450 also leaves out what the save's Item Shop Availability setting
/// never sells (<see cref="TradeStockTables.NeverSoldBySetting"/>): confirmed 2026-11-24 on Ialenn (Slot 8, Normal),
/// where the terminal had no Oxygen, Sodium, Ion Battery or Life Support Gel, and the two items those leave off sale
/// shift the substance pick through the trade-goods step's draws.
/// </summary>
public static class StationTradeStock
{
    /// <summary>
    /// The station terminal's stock, or null where it isn't generated: no station (uncharted, abandoned), pirate
    /// stations, the legacy station layout (<paramref name="useLegacyStation"/> must be false: the save's station
    /// rule), missing tables.
    /// </summary>
    /// <param name="shopAvailability">The save's Item Shop Availability setting (NmsDifficultySettings.ItemShopAvailabilityPath).</param>
    public static IReadOnlyList<TradeStockItem>? ForSystem(ulong portalCode, int galaxy, bool? useLegacyStation,
        string shopAvailability, StationEquipmentTables stations, TradeStockTables trade)
    {
        var attributes = SystemGenerator.Attributes(portalCode, galaxy);
        if (attributes.Uncharted || attributes.Abandoned || attributes.Pirate || attributes.Wealth is < 1 or > 3
            || useLegacyStation is not false || stations.ForceLegacySpaceStations is true) return null;
        if (StationPoseGeneration.GenerateForSystem(portalCode, galaxy, stations.SolarLayout) is not { } pose) return null;
        if (StationSceneSelection.SelectStandardTypeB(stations, pose.Seed)?.LaunchReference is not { } branch) return null;
        var terminal = trade.StandardTypeBTerminals.FirstOrDefault(s => s.LaunchReference == branch)?.Cabinets.FirstOrDefault();
        if (terminal is not { LocalPosition.Length: 3 }) return null;
        if (StationCabinetLocations.ToWorld(pose, terminal.LocalPosition) is not { } world) return null;
        ulong seed = DiscoveryKeys.Mix64(MultiToolSiteGeneration.SeedForPosition(world));
        return Generate(trade, trade.SpaceStation, seed, attributes.Wealth - 1, SystemGenerator.TradingClass(attributes.Economy), shopAvailability);
    }

    /// <summary>
    /// The stock routine (0x14087b4d0) for a Shop interaction outside pirate systems. Every draw is one step of the
    /// stream; a range is Next(max - min + 1) + min. <paramref name="wealth"/> is the GcWealthClass index,
    /// <paramref name="tradingClass"/> the GcTradingClass index.
    /// </summary>
    public static IReadOnlyList<TradeStockItem> Generate(TradeStockTables tables, TradeListData list, ulong seed,
        int wealth, int tradingClass, string shopAvailability)
    {
        var rng = new UniverseRandom(Start(seed));
        int Range(int min, int max) => (int)rng.Next((uint)(max - min + 1)) + min;
        var never = tables.NeverSellable.ToHashSet(StringComparer.Ordinal);
        var notOffered = tables.NeverOffered.ToHashSet(StringComparer.Ordinal);
        if (tables.NeverSoldBySetting.TryGetValue(shopAvailability, out var setting)) notOffered.UnionWith(setting);
        bool Sellable(string id) => !never.Contains(id) && !notOffered.Contains(id);
        var stock = new List<TradeStockItem>();
        var onSale = new HashSet<string>(StringComparer.Ordinal);
        void Add(string id, TradeStockKind kind, bool always)
        {
            if (onSale.Add(id)) stock.Add(new TradeStockItem(id, kind, always));
        }

        int count = Range(list.MinItems, list.MaxItems);
        int products = Math.Clamp((int)(count * list.ProductShare), 0, list.AlwaysProducts.Length + list.OptionalProducts.Length);
        int substances = Math.Clamp(count - products, 0, list.AlwaysSubstances.Length + list.OptionalSubstances.Length);

        // Fixed items: one draw each (the stock amount); each uses up a place whether or not it can be sold.
        foreach (var item in list.AlwaysProducts.Where(i => i.Known))
        {
            rng.Step();
            if (Sellable(item.Id)) Add(item.Id, TradeStockKind.Product, true);
            products--;
        }
        foreach (var item in list.AlwaysSubstances.Where(i => i.Known))
        {
            rng.Step();
            if (Sellable(item.Id)) Add(item.Id, TradeStockKind.Substance, true);
            substances--;
        }

        foreach (var item in Pick(rng, list.OptionalProducts, products))
        {
            if (!item.Known || onSale.Contains(item.Id)) continue;
            rng.Step();
            if (!item.BuildingPart && Sellable(item.Id)) Add(item.Id, TradeStockKind.Product, false);
        }

        // Trade goods: one draw for how many, then one draw per product table entry not already on sale; the first
        // goods of the class's trade category are taken in table order.
        int goods = Range(list.MinTradeGoods[wealth], list.MaxTradeGoods[wealth]);
        var tableProducts = tables.TableProducts.ToHashSet(StringComparer.Ordinal);
        int sells = tables.ClassSells[tradingClass];
        int draws = tables.ProductTableEntries - onSale.Count(tableProducts.Contains);
        foreach (var good in tables.TradeGoods)
        {
            if (goods == 0) break;
            if (good.TradeCategory == sells && !onSale.Contains(good.Id) && Sellable(good.Id))
            {
                Add(good.Id, TradeStockKind.TradeGood, false);
                goods--;
            }
        }
        for (int i = 0; i < draws; i++) rng.Step();

        foreach (var item in Pick(rng, list.OptionalSubstances, substances))
        {
            if (!item.Known || onSale.Contains(item.Id)) continue;
            rng.Step();
            if (Sellable(item.Id)) Add(item.Id, TradeStockKind.Substance, false);
        }
        return stock;
    }

    /// <summary>The first <paramref name="count"/> of the list after the game's shuffle: from the last index down,
    /// swap with Next(i + 1).</summary>
    private static IEnumerable<TradeListItem> Pick(UniverseRandom rng, TradeListItem[] items, int count)
    {
        var order = Enumerable.Range(0, items.Length).ToArray();
        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = (int)rng.Next((uint)(i + 1));
            (order[i], order[j]) = (order[j], order[i]);
        }
        return order.Take(Math.Max(count, 0)).Select(i => items[i]).ToList();
    }

    /// <summary>The stream's start for a 64-bit seed: low word (1 when zero), rol16(low) ^ high ^ low.</summary>
    private static ulong Start(ulong seed)
    {
        uint low = (uint)seed;
        uint carry = ((low >> 16) | (low << 16)) ^ (uint)(seed >> 32) ^ low;
        return ((ulong)carry << 32) | (low == 0 ? 1u : low);
    }
}
