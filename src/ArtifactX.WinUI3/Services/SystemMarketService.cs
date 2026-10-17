using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>One item a station sells, as its trade terminal last showed it.</summary>
public sealed record MarketItem(string ItemId, string Name, int Price, string Change, int Stock, string SeenAt);

/// <summary>
/// What each system's space station sells (GitHub #9, 2026-10-17), read off the trade
/// terminal by game screen recording: the banner names the system ("System - Amogat-Aizu
/// XIV"), each row an item with its price change, and the right column "Buy for 42,847"
/// and "1 / 96" in stock. The game reads the two columns separately, so rows are paired
/// by height on screen, not by order. Only the Buy list counts ("Buy for"), and only
/// names that are real items in the catalog. Kept in user.db (system_market, step 16);
/// each sighting updates price and stock and keeps the time it was seen.
/// </summary>
public static class SystemMarketService
{
    private static readonly object Lock = new();
    private static Dictionary<string, List<MarketItem>>? _markets;
    private static Dictionary<string, string>? _itemIds; // name (any case) -> item id

    public static event Action<ulong>? Changed;
    public static int Read { get; private set; }

    private static string Key(ulong system) => system.ToString("X", CultureInfo.InvariantCulture);

    private static Dictionary<string, List<MarketItem>> Markets
    {
        get
        {
            lock (Lock)
            {
                if (_markets is not null) return _markets;
                _markets = new();
                try
                {
                    using var connection = UserDatabase.Open();
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT system_address, item_id, name, price, change, stock, seen_at FROM system_market WHERE deleted = 0";
                    using var r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        if (!_markets.TryGetValue(r.GetString(0), out var list)) _markets[r.GetString(0)] = list = new();
                        list.Add(new MarketItem(r.GetString(1), r.GetString(2), r.GetInt32(3), r.GetString(4), r.GetInt32(5), r.GetString(6)));
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SystemMarketService] {ex.Message}");
                }
                return _markets;
            }
        }
    }

    /// <summary>What the system's station sells, A-Z.</summary>
    public static IReadOnlyList<MarketItem> For(ulong system)
    {
        lock (Lock) return Markets.TryGetValue(Key(system), out var list) ? list.OrderBy(i => i.Name, StringComparer.CurrentCulture).ToList() : new List<MarketItem>();
    }

    /// <summary>Every system known to sell an item ("where can I buy X?").</summary>
    public static List<(ulong System, MarketItem Item)> WhereSold(string itemId)
    {
        lock (Lock)
            return Markets.SelectMany(kv => kv.Value.Where(i => i.ItemId.Equals(itemId, StringComparison.OrdinalIgnoreCase))
                .Select(i => (ulong.Parse(kv.Key, NumberStyles.HexNumber, CultureInfo.InvariantCulture), i))).ToList();
    }

    /// <summary>A screen's OCR lines with their height on screen (centre, pixels).</summary>
    public static async Task<string?> TryReadAsync(IReadOnlyList<(string Text, double Y)> lines)
    {
        var banner = lines.FirstOrDefault(l => Regex.IsMatch(l.Text, @"^\s*System\s*[-–—]\s*\S", RegexOptions.IgnoreCase));
        var buys = lines.Where(l => Regex.IsMatch(l.Text, @"Buy\s*f\w{0,2}r", RegexOptions.IgnoreCase)).ToList();
        if (banner.Text is null || buys.Count == 0) return null;

        string systemName = Regex.Replace(banner.Text, @"^\s*System\s*[-–—]\s*", "", RegexOptions.IgnoreCase).Trim();
        if (SystemPageReader.MatchSystem(systemName) is not ulong system) return $"trade terminal \"{systemName}\": not a system you were recently in";

        if (_itemIds is null)
        {
            var names = await CatalogService.GetItemNamesAsync();
            _itemIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (id, name) in names) _itemIds.TryAdd(name, id);
        }
        var allNames = _itemIds.Keys.ToList();

        // Each item: the real item name just above its "Price: ..." / "Demand: ..." line, by
        // height - not the line before it in reading order, which can be the tiny label on
        // the item's icon ("NaCl" for Salt, 2026-10-17).
        var items = new List<(string Id, string Name, string Change, double Y)>();
        for (int i = 0; i < lines.Count; i++)
        {
            var m = Regex.Match(lines[i].Text, @"^\s*(Price|Demand)\s*:\s*([+\-−]?\s*[\d.,]+\s*%)", RegexOptions.IgnoreCase);
            if (!m.Success) continue;
            double y = lines[i].Y;
            foreach (var above in lines.Where(l => y - l.Y > 5 && y - l.Y < 50).OrderBy(l => y - l.Y))
            {
                string name = PlanetPageReader.Snap(PlanetPageReader.Clean(above.Text), allNames);
                if (name.Length < 3 || !_itemIds.TryGetValue(name, out var id)) continue;
                items.Add((id, Title(name), m.Groups[2].Value.Replace(" ", "").Replace('−', '-'), above.Y));
                break;
            }
        }
        if (items.Count == 0) return "trade terminal: no items read";

        // Each "Buy for N" (and the "1 / N" under it) goes with the item row at its height.
        // The row height, from the price column: it's complete even when a name wasn't
        // read, so a missing item's price can't fall to its neighbour (Salt's to Residual Goop).
        var buyYs = buys.Select(b => b.Y).OrderBy(v => v).ToList();
        double rowGap = buyYs.Count > 1 ? buyYs.Zip(buyYs.Skip(1), (a, b) => b - a).Where(d => d > 20).DefaultIfEmpty(110).Min()
            : items.Count > 1 ? items.Zip(items.Skip(1), (a, b) => Math.Abs(b.Y - a.Y)).Min() : 110;
        var stocks = lines.Where(l => Regex.IsMatch(l.Text, @"^\s*\d+\s*/\s*[\d,]+\s*$")).ToList();
        var saved = new List<MarketItem>();
        string now = UserDatabase.Now();
        foreach (var buy in buys)
        {
            if (Regex.Match(buy.Text, @"([\d][\d,.]*)\s*$") is not { Success: true } price
                || !int.TryParse(price.Groups[1].Value.Replace(",", "").Replace(".", ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out int units)) continue;
            var item = items.OrderBy(it => Math.Abs(it.Y - buy.Y)).First();
            if (Math.Abs(item.Y - buy.Y) > rowGap / 2) continue;
            var stock = stocks.Where(s => s.Y > buy.Y && s.Y - buy.Y < rowGap / 2).OrderBy(s => s.Y - buy.Y).FirstOrDefault();
            int left = 0;
            if (stock.Text is not null && Regex.Match(stock.Text, @"/\s*([\d,]+)") is { Success: true } st)
                int.TryParse(st.Groups[1].Value.Replace(",", ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out left);
            saved.Add(new MarketItem(item.Id, item.Name, units, item.Change, left, now));
        }
        if (saved.Count == 0) return "trade terminal: no prices matched to items";

        Save(system, saved);
        // The banner's "High Tech // Prosperous" is the system's economy and wealth too.
        var economy = lines.FirstOrDefault(l => l.Y > banner.Y && l.Text.Contains("//", StringComparison.Ordinal));
        string extra = "";
        if (economy.Text is not null && await SystemPageReader.TryReadEconomyAsync(system, economy.Text) is { } e) extra = $"; {e}";
        Read++;
        Changed?.Invoke(system);
        return $"trade terminal {UniverseInfo.SystemName(system)}: {string.Join(", ", saved.Select(s => $"{s.Name} {s.Price:N0} ({s.Change}) x{s.Stock}"))}{extra}";
    }

    private static string Title(string s) => s.Any(char.IsLower) ? s : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());

    // A price that differs from the one stored is only taken once read the same way twice -
    // a single misread ("950" for 350) overwrote a good price once (2026-10-17).
    private static readonly Dictionary<(string, string), int> PendingPrice = new();

    private static void Save(ulong system, List<MarketItem> items)
    {
        string key = Key(system);
        lock (Lock)
        {
            if (Markets.TryGetValue(key, out var known))
                items = items.Where(item =>
                {
                    var old = known.FirstOrDefault(i => i.ItemId.Equals(item.ItemId, StringComparison.OrdinalIgnoreCase));
                    if (old is null || old.Price == item.Price) { PendingPrice.Remove((key, item.ItemId)); return true; }
                    if (PendingPrice.TryGetValue((key, item.ItemId), out int seen) && seen == item.Price) { PendingPrice.Remove((key, item.ItemId)); return true; }
                    PendingPrice[(key, item.ItemId)] = item.Price;
                    return false;
                }).ToList();
            if (items.Count == 0) return;
            if (!Markets.TryGetValue(key, out var list)) Markets[key] = list = new();
            for (int k = 0; k < items.Count; k++)
            {
                var item = items[k];
                // A hovered row's stock reads badly (arrows around it): keep the last good one.
                if (item.Stock == 0 && list.FirstOrDefault(i => i.ItemId.Equals(item.ItemId, StringComparison.OrdinalIgnoreCase)) is { Stock: > 0 } old)
                    items[k] = item = item with { Stock = old.Stock };
                list.RemoveAll(i => i.ItemId.Equals(item.ItemId, StringComparison.OrdinalIgnoreCase));
                list.Add(item);
            }
        }
        try
        {
            using var connection = UserDatabase.Open();
            using var tx = connection.BeginTransaction();
            foreach (var item in items)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO system_market (system_address, item_id, name, price, change, stock, seen_at, updated_at, source, synced_at, deleted, added_by)
                    VALUES ($s, $id, $name, $price, $change, $stock, $now, $now, 'captured', NULL, 0, $by)
                    ON CONFLICT(system_address, item_id) DO UPDATE SET name = $name, price = $price, change = $change, stock = $stock,
                        seen_at = $now, updated_at = $now, synced_at = NULL, deleted = 0";
                cmd.Parameters.AddWithValue("$s", key);
                cmd.Parameters.AddWithValue("$id", item.ItemId);
                cmd.Parameters.AddWithValue("$name", item.Name);
                cmd.Parameters.AddWithValue("$price", item.Price);
                cmd.Parameters.AddWithValue("$change", item.Change);
                cmd.Parameters.AddWithValue("$stock", item.Stock);
                cmd.Parameters.AddWithValue("$now", item.SeenAt);
                cmd.Parameters.AddWithValue("$by", GameAccountsService.ActiveKey);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SystemMarketService] {ex.Message}");
        }
    }

    /// <summary>Drops the in-memory copy (after a user.db import).</summary>
    public static void Reload()
    {
        lock (Lock) _markets = null;
    }
}
