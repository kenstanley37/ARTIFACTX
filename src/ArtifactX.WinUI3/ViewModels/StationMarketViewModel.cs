using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Universe;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One item the station's trade terminal sells: icon, name and why it's there.</summary>
public sealed record MarketCard(ImageSource? Icon, string Name, string Details)
{
    public string Tooltip => $"{Name}\n{Details}";
}

/// <summary>
/// What the system's space station trade terminal sells, worked out with the game's own rules
/// (Core StationTradeStock, issue #9) - no visit or screen reading needed. Depends only on the system and the save's
/// station layout rule, so it's worked out again only when either changes.
/// </summary>
public sealed partial class StationMarketViewModel : ObservableObject
{
    private (ulong System, bool? Legacy)? _shown;
    private int _version;

    [ObservableProperty] public partial string Summary { get; private set; } = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasMessage))] public partial string Message { get; private set; } = "";
    [ObservableProperty] public partial IReadOnlyList<MarketCard> Items { get; private set; } = [];

    public bool HasMessage => Message.Length > 0;

    public async Task UpdateAsync(ulong? system, PlanetSnapshot snapshot)
    {
        if (system is not ulong address) return;
        int version = ++_version;
        try
        {
            var stationsTask = CatalogService.GetStationEquipmentTablesAsync();
            var tradeTask = CatalogService.GetTradeStockTablesAsync();
            await Task.WhenAll(stationsTask, tradeTask);
            var stations = stationsTask.Result;
            var trade = tradeTask.Result;
            var saveInputs = snapshot.EquipmentSaveInputs ?? (await LiveSaveWatcher.GetLatestAsync()).EquipmentSaveInputs;
            bool? legacy = stations is null ? null : NmsMultiToolGenerationPaths.ReadSavedLegacyStationRule(saveInputs, stations);
            if (version != _version || _shown == (address, legacy)) return;

            if (stations is null || trade is null)
            {
                Show(address, legacy, [], 0, "Rebuild the game data (Settings) to see what the station sells.");
                return;
            }
            var (code, galaxy) = UniverseInfo.Generator(address);
            var attributes = SystemGenerator.Attributes(code, galaxy);
            var stock = await Task.Run(() => StationTradeStock.ForSystem(code, galaxy, legacy, stations, trade));
            if (version != _version) return;
            if (stock is null)
            {
                Show(address, legacy, [], 0, attributes.Uncharted || attributes.Abandoned ? "This system has no working space station."
                    : attributes.Pirate ? "What pirate stations sell isn't worked out yet."
                    : "Your save uses the older station layout, which isn't worked out yet.");
                return;
            }

            await CatalogService.WarmCacheAsync(stock.Select(s => s.Id));
            if (version != _version) return;
            int unnamed = 0;
            var cards = stock
                .OrderBy(s => s.Kind == TradeStockKind.TradeGood ? 0 : s.Always ? 2 : 1)
                .ThenBy(s => s.Kind)
                .Select(s =>
                {
                    var entry = CatalogService.TryGet(s.Id);
                    string name = entry?.DisplayName is { Length: > 0 } known ? known : $"Item {++unnamed}";
                    string details = s.Kind == TradeStockKind.TradeGood ? "Trade good of this system's economy"
                        : s.Always ? "Every station sells it"
                        : "This station's pick";
                    return new MarketCard(entry?.Icon, name, details);
                })
                .ToList();
            Show(address, legacy, cards, stock.Count(s => s.Kind == TradeStockKind.TradeGood), "");
        }
        catch (Exception error)
        {
            Debug.WriteLine($"[StationMarket] Station stock unavailable: {error.Message}");
        }
    }

    private void Show(ulong system, bool? legacy, IReadOnlyList<MarketCard> cards, int goods, string message)
    {
        _shown = (system, legacy);
        Message = message;
        Items = cards;
        Summary = cards.Count == 0 ? "" : $"{cards.Count} items" + (goods > 0 ? $" · {goods} trade good{(goods == 1 ? "" : "s")}" : "");
    }
}
