using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>What one system's space station trade terminal sells, with prices (Core StationTradeStock).</summary>
public sealed record TradeFinderStation(ulong SystemAddress, IReadOnlyList<TradeStockItem> Stock);

/// <summary>
/// The Trade Finder's search (issues #9 and #20): the station trade terminal stock and buy prices of every system around
/// a start (Core NearbySystems + StationTradeStock, so the mobile app can run the same search), for the save's Item
/// Shop Availability and Currency Cost settings. Trading posts are left out: they are per planet, hundreds each.
/// </summary>
public static class TradeFinderService
{
    /// <summary>The stations within <paramref name="regionRadius"/> regions of <paramref name="start"/>, off the UI
    /// thread; empty without the game data, or when the save's stations use the older layout.</summary>
    public static async Task<IReadOnlyList<TradeFinderStation>> FindAsync(ulong start, int regionRadius, JObject? saveInputs,
        IProgress<int>? progress = null, CancellationToken cancel = default)
    {
        var settingsTask = CatalogService.GetGalaxyGenerationSettingsAsync();
        var stationsTask = CatalogService.GetStationEquipmentTablesAsync();
        var tradeTask = CatalogService.GetTradeStockTablesAsync();
        await Task.WhenAll(settingsTask, stationsTask, tradeTask);
        if (settingsTask.Result is not { } settings || stationsTask.Result is not { } stations || tradeTask.Result is not { Prices: not null } trade)
            return [];
        bool? legacy = NmsMultiToolGenerationPaths.ReadSavedLegacyStationRule(saveInputs, stations);
        if (legacy is not false) return [];
        string shops = SaveSessionManager.GetValue(NmsDifficultySettings.ItemShopAvailabilityPath)?.Value<string>() ?? "Normal";
        string currency = SaveSessionManager.GetValue(NmsDifficultySettings.CurrencyCostPath)?.Value<string>() ?? "Normal";
        return await Task.Run(() =>
        {
            var systems = NearbySystems.Around(start, regionRadius, settings).ToList();
            var found = new ConcurrentBag<TradeFinderStation>();
            int done = 0;
            Parallel.ForEach(systems, new ParallelOptions { CancellationToken = cancel }, system =>
            {
                ulong address = system.SystemAddress & ~(0xFUL << 52);
                var (portal, galaxy) = SystemProfiles.PortalOf(address);
                if (StationTradeStock.ForSystem(portal, galaxy, legacy, shops, currency, stations, trade) is { Count: > 0 } stock)
                    found.Add(new TradeFinderStation(address, stock));
                int n = Interlocked.Increment(ref done);
                if (n % 250 == 0) progress?.Report(n);
            });
            return (IReadOnlyList<TradeFinderStation>)found.ToList();
        }, cancel);
    }

    /// <summary>Whether the save's stations can be searched (see <see cref="FindAsync"/>); null when unknown.</summary>
    public static async Task<bool?> StationsSupportedAsync(JObject? saveInputs) =>
        await CatalogService.GetStationEquipmentTablesAsync() is { } stations
            ? NmsMultiToolGenerationPaths.ReadSavedLegacyStationRule(saveInputs, stations) is false
            : null;
}
