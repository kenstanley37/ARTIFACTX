using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>A building type in the materials picker.</summary>
public sealed record SettlementBuildingOption(string Id, string Label, string? Tip)
{
    /// <summary>What screen readers say for the option.</summary>
    public override string ToString() => Label;
}

/// <summary>One lead on where to get an item; with a portal code when it's a place.</summary>
public sealed record MaterialLead(string Text, string Code)
{
    public bool HasCode => Code.Length > 0;
    public bool IsPlace => HasCode;
    public bool IsNote => !HasCode;
}

/// <summary>What one building stage asks for, and where to get it.</summary>
public sealed record MaterialRowViewModel(string Stage, ImageSource? Icon, string Title, List<MaterialLead> Leads);

/// <summary>
/// Building materials (user request 2026-10-14: "find items for settlement building
/// upgrades"). Each settlement building stage asks for one fixed item (catalog
/// "SettlementCosts", from the game's own settlement settings); for each, where to get
/// it from what ArtifactX already knows:
///  - trade goods (TRA_*): only trade terminals in systems of one economy sell them
///    (catalog "TradeClasses" - Scientific sells Exotic, ...); the known systems with
///    that economy are listed nearest-the-settlement first;
///  - anything listed by a "Sells items" find;
///  - raw resources recorded on a planet's details;
///  - craftable or refinable items point to Reference.
/// The building queued for its next upgrade is selected first. Read-only.
/// </summary>
public sealed partial class SettlementMaterialsViewModel : ObservableObject
{
    // The game's trading class -> the economy word the rest of ArtifactX uses
    // (UniverseInfo.EconomyLabel; "Advanced Materials" is the game's Fusion, both
    // selling Alloy - same pairing as the Item Values page).
    private static readonly Dictionary<string, string> EconomyLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Mining"] = "Mining", ["HighTech"] = "Technology", ["Trading"] = "Trading", ["Manufacturing"] = "Manufacturing",
        ["Fusion"] = "Advanced Materials", ["Scientific"] = "Scientific", ["PowerGeneration"] = "Power Generation",
    };

    private List<SettlementCost> _costs = new();
    private Dictionary<string, string> _economySells = new();
    private Dictionary<string, ItemValueInfo> _itemValues = new();
    private HashSet<string> _craftable = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _refinable = new(StringComparer.OrdinalIgnoreCase);
    private List<KnownSystem> _knownSystems = new();
    private ulong? _settlementAddress;
    private bool _loading;

    public ObservableCollection<SettlementBuildingOption> Buildings { get; } = new();
    public ObservableCollection<MaterialRowViewModel> Rows { get; } = new();

    [ObservableProperty] public partial int BuildingIndex { get; set; } = -1;
    [ObservableProperty] public partial string NextUpgrade { get; private set; } = "";

    /// <summary>The selected building's other names, or why nothing can be shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote))]
    public partial string Note { get; private set; } = "";

    public bool HasNote => Note.Length > 0;

    partial void OnBuildingIndexChanged(int value) => ShowRows();

    /// <summary>For the settlement at this slot (-1: none).</summary>
    public async Task LoadAsync(int settlementIndex)
    {
        if (_loading) return;
        _loading = true;
        try
        {
            if (_costs.Count == 0) await LoadCatalogAsync();

            _settlementAddress = settlementIndex >= 0 ? SettlementService.Address(settlementIndex) : null;
            string next = settlementIndex >= 0 ? SettlementService.NextBuilding(settlementIndex) : "";

            var buildings = _costs.Select(c => c.BuildingClass).Distinct()
                .OrderBy(b => b == next ? 0 : 1).ThenBy(SettlementService.BuildingLabel).ToList();
            Buildings.Clear();
            foreach (var b in buildings)
            {
                string label = SettlementService.BuildingLabel(b);
                string aliases = SettlementService.BuildingAliases(b);
                Buildings.Add(new SettlementBuildingOption(b, b == next ? $"{label} (next upgrade)" : label, aliases.Length > 0 ? aliases : null));
            }
            NextUpgrade = next.Length > 0 && buildings.Contains(next) ? $"Next upgrade here: {SettlementService.BuildingLabel(next)}" : "";

            if (buildings.Count == 0)
            {
                Rows.Clear();
                Note = "Update your game data (Settings › Game data) to see what settlement buildings need.";
                return;
            }
            BuildingIndex = -1; // so picking the first always shows it
            BuildingIndex = 0;
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task LoadCatalogAsync()
    {
        _costs = await CatalogService.GetSettlementCostsAsync();
        await SettlementService.LoadBuildingNamesAsync();
        _economySells = await CatalogService.GetEconomySellsAsync();
        _itemValues = (await CatalogService.GetItemValuesAsync()).GroupBy(v => v.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        _craftable = (await CatalogService.GetCraftingAsync()).Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var r in await CatalogService.GetRecipesAsync())
            if (r.Kind == "Refiner") _refinable.Add(r.Result.Id);
        await CatalogService.WarmCacheAsync(_costs.Select(c => c.Item));

        // Every system this account knows: this save's discoveries, then the other slots'.
        var mine = NmsPlanets.ReadSystems(SaveSessionManager.GetValue(S.DiscoveriesPath) as Newtonsoft.Json.Linq.JArray);
        var others = SaveSessionManager.ActivePlatformFolder is string folder && SaveSessionManager.ActiveSlot is { } slot
            ? await KnownPlanetsService.ReadOtherSlotsSystemsAsync(folder, slot.SlotId)
            : new List<KnownSystem>();
        _knownSystems = mine.Concat(others).GroupBy(s => S.ToSystemAddress(s.SystemAddress)).Select(g => g.First()).ToList();
    }

    private void ShowRows()
    {
        Rows.Clear();
        if (BuildingIndex < 0 || BuildingIndex >= Buildings.Count) return;
        string building = Buildings[BuildingIndex].Id;
        Note = SettlementService.BuildingAliases(building);
        foreach (var cost in _costs.Where(c => c.BuildingClass == building).OrderBy(c => c.Stage))
        {
            var entry = CatalogService.TryGet(cost.Item);
            string name = entry?.DisplayName ?? cost.Item;
            string amount = cost.AmountMin == cost.AmountMax ? $"{cost.AmountMax}" : $"{cost.AmountMin}-{cost.AmountMax}";
            Rows.Add(new MaterialRowViewModel(SettlementService.StageLabel(cost.Stage), entry?.Icon, $"{amount} × {name}", WhereToGet(cost.Item, name)));
        }
    }

    /// <summary>Where this item can be had, best lead first.</summary>
    private List<MaterialLead> WhereToGet(string itemId, string name)
    {
        var leads = new List<MaterialLead>();

        // Trade goods: the economy that sells them, and known systems with it.
        if (_itemValues.TryGetValue(itemId, out var value) && value.TradeCategory is { Length: > 0 } category
            && category != "None" && category != "SpecialShop")
        {
            var economies = _economySells.Where(kv => kv.Value == category).Select(kv => EconomyLabels.GetValueOrDefault(kv.Key, kv.Key)).ToList();
            if (economies.Count > 0)
            {
                string economy = string.Join(" or ", economies);
                leads.Add(new($"Trade good - sold at the trade terminal in {economy} systems.", ""));
                var systems = SystemsWithEconomy(economies).Take(3).ToList();
                if (systems.Count == 0)
                    leads.Add(new($"None of the systems you've visited are {economy}. The Galaxy Map's economy filter shows them nearby.", ""));
                foreach (var (address, distance) in systems)
                    leads.Add(new($"{UniverseInfo.SystemName(address)} · {UniverseInfo.Wealth(address)}{DistanceText(distance)}", S.PortalAddress(address, 1)));
            }
        }

        // Finds that say they sell it.
        foreach (var find in FindsService.All.Where(f => f.Type == "Sells items" && f.ItemIds.Contains(itemId, StringComparer.OrdinalIgnoreCase)).Take(3))
            leads.Add(new($"Sold at: {(find.Title.Length > 0 ? find.Title : "a find")}", find.PortalCode));

        // Planets where the resource was recorded (details hold names as the game shows them).
        foreach (var (system, planet, _) in PlanetDetailsService.AllWithPlanets
                     .Where(p => p.Details.Resources.Any(r => string.Equals(r, name, StringComparison.CurrentCultureIgnoreCase)))
                     .OrderBy(p => Distance(p.System) ?? double.MaxValue).Take(3))
            leads.Add(new($"Found on {UniverseInfo.PlanetName(system, planet)} ({UniverseInfo.SystemName(system)}){DistanceText(Distance(system))}",
                S.PortalAddress(system, planet)));

        if (_craftable.Contains(itemId))
            leads.Add(new("Can be crafted - see Reference › Crafting.", ""));
        if (_refinable.Contains(itemId))
            leads.Add(new("Can be made in a refiner - see Reference › Recipes.", ""));
        if (leads.Count == 0)
            leads.Add(new("Not recorded anywhere yet. Add a find when you see it for sale, or the resource to a planet's details.", ""));
        return leads;
    }

    /// <summary>Known systems (same galaxy) whose economy is one of these, nearest the settlement first.</summary>
    private IEnumerable<(ulong Address, double? Distance)> SystemsWithEconomy(List<string> economies) =>
        _knownSystems
            .Where(s => _settlementAddress is not ulong home || S.GalaxyNumber(s.SystemAddress) == S.GalaxyNumber(home))
            .Where(s => economies.Contains(UniverseInfo.Economy(s.SystemAddress)))
            .Select(s => (s.SystemAddress, Distance(s.SystemAddress)))
            .OrderBy(s => s.Item2 ?? double.MaxValue);

    private double? Distance(ulong address) =>
        _settlementAddress is ulong home ? NmsPlanets.DistanceLightYears(S.ToSystemAddress(home), S.ToSystemAddress(address)) : null;

    // Distances are region to region, so systems in the settlement's own region come out as 0.
    private static string DistanceText(double? ly) => ly switch
    {
        null => "",
        < 1 => " · same region as the settlement",
        double d => $" · {d.ToString("N0", CultureInfo.CurrentCulture)} ly",
    };
}
