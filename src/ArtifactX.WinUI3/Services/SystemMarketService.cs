using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>One item a station sells, as its trade terminal last showed it.</summary>
/// <param name="Place">Where in the system: "station" (the space station) or "planet:N" (a trading
/// post on planet N) - their terminals sell different stock at different prices (2026-10-17).</param>
public sealed record MarketItem(string ItemId, string Name, int Price, string Change, int Stock, string SeenAt, string Place = "station");

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

    private static string Hex(ulong system) => system.ToString("X", CultureInfo.InvariantCulture);
    private static string Key(ulong system, string place) => $"{Hex(system)}|{place}";

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
                    cmd.CommandText = "SELECT system_address, place, item_id, name, price, change, stock, seen_at FROM market_items WHERE deleted = 0";
                    using var r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        string key = $"{r.GetString(0)}|{r.GetString(1)}";
                        if (!_markets.TryGetValue(key, out var list)) _markets[key] = list = new();
                        list.Add(new MarketItem(r.GetString(2), r.GetString(3), r.GetInt32(4), r.GetString(5), r.GetInt32(6), r.GetString(7), r.GetString(1)));
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

    /// <summary>What's sold in the system - its station and any trading posts - by place, then A-Z.</summary>
    public static IReadOnlyList<MarketItem> For(ulong system)
    {
        string prefix = Hex(system) + "|";
        lock (Lock)
            return Markets.Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal)).SelectMany(kv => kv.Value)
                .OrderBy(i => i.Place == "station" ? "" : i.Place, StringComparer.Ordinal).ThenBy(i => i.Name, StringComparer.CurrentCulture).ToList();
    }

    /// <summary>Every system known to sell an item ("where can I buy X?").</summary>
    public static List<(ulong System, MarketItem Item)> WhereSold(string itemId)
    {
        lock (Lock)
            return Markets.SelectMany(kv => kv.Value.Where(i => i.ItemId.Equals(itemId, StringComparison.OrdinalIgnoreCase))
                .Select(i => (ulong.Parse(kv.Key.Split('|')[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture), i))).ToList();
    }

    /// <summary>A screen's OCR lines with their height on screen (centre, pixels).</summary>
    /// <param name="readStrip">Reads one strip of the frame on its own, enlarged (left and right
    /// edge as fractions of the width, centre height in pixels): the whole-screen read skips short
    /// text like "1 / 5" or "-4.1%" now and then, while the strip reads it (tested on the
    /// Belezna frames, 2026-10-18). Only used for values the whole-screen read missed.</param>
    public static async Task<string?> TryReadAsync(IReadOnlyList<(string Text, double Y)> lines,
        Func<double, double, double, Task<string>>? readStrip = null)
    {
        var banner = lines.FirstOrDefault(l => Regex.IsMatch(l.Text, @"^\s*System\s*[-–—]\s*\S", RegexOptions.IgnoreCase));
        var buys = lines.Where(l => Regex.IsMatch(l.Text, @"Buy\s*f\w{0,2}r", RegexOptions.IgnoreCase)).ToList();
        if (banner.Text is null || buys.Count == 0) return null;

        string systemName = Regex.Replace(banner.Text, @"^\s*System\s*[-–—]\s*", "", RegexOptions.IgnoreCase).Trim();
        if (SystemPageReader.MatchSystem(systemName) is not ulong system) return $"trade terminal \"{systemName}\": not a system you were recently in";
        // The station or a planet's trading post: where the save last put the player.
        string place = PlanetPageReader.CurrentPlanet(system) is int planet and > 0 ? $"planet:{planet}" : "station";

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
            // OCR reads "Demand:" as "Demand." and "%" as "0/0" ("+1.40/0" for +1.4%, Belezna 2026-10-17).
            var m = Regex.Match(lines[i].Text, @"^\s*(Price|Demand)\s*[:.;,]?\s*([+\-−]?\s*[\d.,]+?\s*(?:%|0/0|o/o))?", RegexOptions.IgnoreCase);
            // Black market goods show "Illegal Black Market Goods" where others show a price change.
            bool illegal = !m.Success && Regex.IsMatch(lines[i].Text, @"Illegal\s+Black\s+Market", RegexOptions.IgnoreCase);
            // An abandoned system's terminal shows static instead ("kzzzkt", "ERROR", "Data Missing",
            // "Connection Lost", "Economy Data Unavailable" - Degtyab-Famil, 2026-10-18): no price change.
            bool noData = !m.Success && !illegal && Regex.IsMatch(lines[i].Text.Trim(),
                @"^(k\w{0,2}z+\w{0,2}t|ERR?OR|Data\s+Missing|Connection\s+Lost|Economy\s+Data\s+Unavailable)$", RegexOptions.IgnoreCase);
            if (!m.Success && !illegal && !noData) continue;
            double y = lines[i].Y;
            string change = illegal ? "Illegal" : noData ? "No data" : m.Groups[2].Value;
            // "Demand:" and its "-6.0%" can be read as two lines (Gold, alone on a page,
            // 2026-10-17): take the percentage at the same height.
            if (change.Length == 0 && !illegal)
            {
                var pct = lines.Where(l => Math.Abs(l.Y - y) < 12 && Regex.IsMatch(l.Text, @"^\s*[+\-−]?\s*[\d.,]+?\s*(%|0/0|o/o)\s*$", RegexOptions.IgnoreCase)).FirstOrDefault();
                // Sometimes the percentage isn't read at all (Microprocessor "+0.0%"): read that
                // strip again on its own; failing that, keep the item without it.
                change = pct.Text?.Trim() ?? "";
                if (change.Length == 0 && readStrip is not null
                    && Regex.Match(await readStrip(0.58, 0.79, y), @"[+\-−]?\s*[\d.,]+?\s*(?:%|0/0|o/o)", RegexOptions.IgnoreCase) is { Success: true } again)
                    change = again.Value;
            }
            foreach (var above in lines.Where(l => y - l.Y > 5 && y - l.Y < 50).OrderBy(l => y - l.Y))
            {
                string name = PlanetPageReader.Snap(PlanetPageReader.Clean(above.Text), allNames);
                if (name.Length < 3 || !_itemIds.TryGetValue(name, out var id)) continue;
                // Tidy a percentage only - "No data" keeps its space (showed as "Nodata", 2026-10-18).
                if (!illegal && !noData)
                    change = Regex.Replace(change.Replace(" ", "").Replace('−', '-'), @"(0/0|o/o)$", "%", RegexOptions.IgnoreCase);
                items.Add((id, Title(name), change, above.Y));
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
            // Everything after "Buy for": OCR can put a space inside the number ("42,01 5").
            string digits = new string(Regex.Replace(buy.Text, @"^.*?Buy\s*f\w{0,2}r", "", RegexOptions.IgnoreCase).Where(char.IsDigit).ToArray());
            if (digits.Length == 0 || digits.Length > 9
                || !int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int units)
                || units <= 0) continue; // a hovered row reads "0" (Magno-Gold, 2026-10-17)
            var item = items.OrderBy(it => Math.Abs(it.Y - buy.Y)).First();
            if (Math.Abs(item.Y - buy.Y) > rowGap / 2) continue;
            var stock = stocks.Where(s => s.Y > buy.Y && s.Y - buy.Y < rowGap / 2).OrderBy(s => s.Y - buy.Y).FirstOrDefault();
            int left = 0;
            string? stockText = stock.Text;
            // Not read: the "1 / N" bar sits about a third of a row below the price - read it alone.
            if (stockText is null && readStrip is not null) stockText = await readStrip(0.79, 0.94, buy.Y + rowGap * 35 / 110);
            if (stockText is not null && Regex.Match(stockText, @"^\s*\d+\s*/\s*([\d,]+)") is { Success: true } st)
                int.TryParse(st.Groups[1].Value.Replace(",", ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out left);
            saved.Add(new MarketItem(item.Id, item.Name, units, item.Change, left, now, place));
        }
        if (saved.Count == 0) return "trade terminal: no prices matched to items";

        Save(system, place, saved);
        // The banner's "High Tech // Prosperous" is the system's economy and wealth too.
        var economy = lines.FirstOrDefault(l => l.Y > banner.Y && l.Text.Contains("//", StringComparison.Ordinal));
        string extra = "";
        if (economy.Text is not null && await SystemPageReader.TryReadEconomyAsync(system, economy.Text) is { } e) extra = $"; {e}";
        Read++;
        Changed?.Invoke(system);
        return $"trade terminal {UniverseInfo.SystemName(system)} ({place}): {string.Join(", ", saved.Select(s => $"{s.Name} {s.Price:N0} ({s.Change}) x{s.Stock}"))}{extra}";
    }

    private static string Title(string s) => s.Any(char.IsLower) ? s : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());

    // A price that differs from the one stored is only taken once read the same way twice -
    // a single misread ("950" for 350) overwrote a good price once (2026-10-17).
    private static readonly Dictionary<(string, string), int> PendingPrice = new();

    private static void Save(ulong system, string place, List<MarketItem> items)
    {
        string key = Key(system, place);
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
                var last = list.FirstOrDefault(i => i.ItemId.Equals(item.ItemId, StringComparison.OrdinalIgnoreCase));
                if (item.Stock == 0 && last is { Stock: > 0 })
                    items[k] = item = item with { Stock = last.Stock };
                // A price change not read this time keeps the last one (Viscous Fluids lost its "+1.1%").
                if (item.Change.Length == 0 && last is { Change.Length: > 0 })
                    items[k] = item = item with { Change = last.Change };
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
                    INSERT INTO market_items (system_address, place, item_id, name, price, change, stock, seen_at, updated_at, source, synced_at, deleted, added_by, game_build)
                    VALUES ($s, $place, $id, $name, $price, $change, $stock, $now, $now, 'captured', NULL, 0, $by, $build)
                    ON CONFLICT(system_address, place, item_id) DO UPDATE SET name = $name, price = $price, change = $change, stock = $stock,
                        seen_at = $now, updated_at = $now, synced_at = NULL, deleted = 0, game_build = $build";
                cmd.Parameters.AddWithValue("$s", Hex(system));
                cmd.Parameters.AddWithValue("$place", place);
                cmd.Parameters.AddWithValue("$id", item.ItemId);
                cmd.Parameters.AddWithValue("$name", item.Name);
                cmd.Parameters.AddWithValue("$price", item.Price);
                cmd.Parameters.AddWithValue("$change", item.Change);
                cmd.Parameters.AddWithValue("$stock", item.Stock);
                cmd.Parameters.AddWithValue("$now", item.SeenAt);
                cmd.Parameters.AddWithValue("$by", GameAccountsService.ActiveKey);
                cmd.Parameters.AddWithValue("$build", FieldSources.GameBuild);
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
