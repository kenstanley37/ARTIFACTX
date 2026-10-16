using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Building materials (user request 2026-10-14: "find items for settlement
/// building upgrades"). Each settlement building stage asks for one fixed item
/// (catalog "SettlementCosts", from the game's own settlement settings); for
/// each, where to get it from what ArtifactX already knows:
///  - trade goods (TRA_*): only trade terminals in systems of one economy sell
///    them (catalog "TradeClasses" - Scientific sells Exotic, ...); the known
///    systems with that economy are listed nearest-the-settlement first;
///  - anything listed by a "Sells items" find;
///  - raw resources recorded on a planet's details;
///  - craftable items point to Crafting.
/// The building queued for its next upgrade (NextBuildingUpgradeClass) is
/// selected first. Read-only - nothing here writes the save.
/// </summary>
public sealed partial class SettlementPage
{
    private List<SettlementCost> _costs = new();
    private Dictionary<string, string> _economySells = new();
    private Dictionary<string, ItemValueInfo> _itemValues = new();
    private HashSet<string> _craftable = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _refinable = new(StringComparer.OrdinalIgnoreCase);
    private List<KnownSystem> _knownSystems = new();
    private ulong? _settlementAddress;
    private bool _materialsLoading;

    // The game's trading class -> the economy word the rest of ArtifactX uses
    // (UniverseInfo.EconomyLabel; "Advanced Materials" is the game's Fusion,
    // both selling Alloy - same pairing as the Item Values page).
    private static readonly Dictionary<string, string> EconomyLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Mining"] = "Mining", ["HighTech"] = "Technology", ["Trading"] = "Trading", ["Manufacturing"] = "Manufacturing",
        ["Fusion"] = "Advanced Materials", ["Scientific"] = "Scientific", ["PowerGeneration"] = "Power Generation",
    };

    // The game's own stage names (UI_SETTLEMENT_BUILD_STAGE_*), by GcSettlementConstructionLevel.
    private static string StageLabel(int stage) => stage switch
    {
        0 => "Foundations", 1 => "Basic structure", 2 => "Upper storeys", 3 => "Roof", 4 => "Final fit out",
        5 => "Upgrade 1", 6 => "Upgrade 2", 7 => "Upgrade 3", _ => $"Stage {stage}",
    };

    // Building names from the game (catalog "SettlementBuildingNames"): each
    // type has a pool and every settlement shows one of them, so the type's own
    // name is shown with the others in a tooltip. Loaded once for all pages.
    private static Dictionary<string, (string Name, List<string> Names, List<string> Upgraded)> _buildingNames = new();

    private static string BuildingLabel(string buildingClass) =>
        _buildingNames.TryGetValue(buildingClass, out var n) ? n.Name : buildingClass.Replace("Settlement_", "").Replace('_', ' ');

    /// <summary>"Also called: Manufacturing Unit, Workshop... Once upgraded: Nano-Foundry..."</summary>
    private static string BuildingAliases(string buildingClass)
    {
        if (!_buildingNames.TryGetValue(buildingClass, out var n)) return "";
        var others = n.Names.Where(x => !string.Equals(x, n.Name, StringComparison.OrdinalIgnoreCase)).ToList();
        var parts = new List<string>();
        if (others.Count > 0) parts.Add($"Each settlement names it differently - also: {string.Join(", ", others)}.");
        if (n.Upgraded.Count > 0) parts.Add($"Once upgraded: {string.Join(", ", n.Upgraded)}.");
        return string.Join(" ", parts);
    }

    // Details needs ~620 px (feature dropdowns + stat boxes), materials ~600 px
    // to read its "where to get it" lines; below that they stack (user 2026-10-14).
    private const double DetailsWidth = 640, MaterialsMinWidth = 600;

    private void SettlementCards_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool beside = e.NewSize.Width >= DetailsWidth + SettlementCardsGrid.ColumnSpacing + MaterialsMinWidth;
        var columns = SettlementCardsGrid.ColumnDefinitions;
        columns[0].Width = beside ? new GridLength(DetailsWidth) : new GridLength(1, GridUnitType.Star);
        columns[1].Width = beside ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Grid.SetRow(MaterialsCard, beside ? 0 : 1);
        Grid.SetColumn(MaterialsCard, beside ? 1 : 0);
    }

    private async Task LoadMaterialsAsync()
    {
        if (_materialsLoading) return;
        _materialsLoading = true;
        try
        {
            if (_costs.Count == 0)
            {
                _costs = await CatalogService.GetSettlementCostsAsync();
                _buildingNames = await CatalogService.GetSettlementBuildingNamesAsync();
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

            _settlementAddress = _selectedIndex >= 0
                ? S.ParsePackedAddress(SaveSessionManager.GetValue(NmsSettlementPaths.SettlementPath(_selectedIndex).Append("yhJ").ToArray()))
                : null;
            string next = _selectedIndex >= 0
                ? SaveSessionManager.GetValue(NmsSettlementPaths.SettlementPath(_selectedIndex).Concat(new[] { ":Qn", "iqv" }).ToArray())?.ToString() ?? ""
                : "";

            var buildings = _costs.Select(c => c.BuildingClass).Distinct()
                .OrderBy(b => b == next ? 0 : 1).ThenBy(BuildingLabel).ToList();
            MaterialsBuildingBox.Items.Clear();
            foreach (var b in buildings)
            {
                var item = new ComboBoxItem { Content = b == next ? $"{BuildingLabel(b)} (next upgrade)" : BuildingLabel(b), Tag = b };
                if (BuildingAliases(b) is { Length: > 0 } aliases) ToolTipService.SetToolTip(item, aliases);
                MaterialsBuildingBox.Items.Add(item);
            }
            MaterialsNextTxt.Text = next.Length > 0 && next != "None" && buildings.Contains(next)
                ? $"Next upgrade here: {BuildingLabel(next)}" : "";

            if (buildings.Count == 0)
            {
                MaterialsPanel.Children.Clear();
                MaterialsPanel.Children.Add(Dim("Update your game data (Settings › Game data) to see what settlement buildings need."));
                return;
            }
            MaterialsBuildingBox.SelectedIndex = 0;
        }
        finally
        {
            _materialsLoading = false;
        }
    }

    private void MaterialsBuilding_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (MaterialsBuildingBox.SelectedItem is ComboBoxItem { Tag: string building }) ShowMaterials(building);
    }

    private void ShowMaterials(string building)
    {
        MaterialsPanel.Children.Clear();
        if (BuildingAliases(building) is { Length: > 0 } aliases) MaterialsPanel.Children.Add(Dim(aliases));
        foreach (var cost in _costs.Where(c => c.BuildingClass == building).OrderBy(c => c.Stage))
            MaterialsPanel.Children.Add(MaterialRow(cost));
    }

    private UIElement MaterialRow(SettlementCost cost)
    {
        var entry = CatalogService.TryGet(cost.Item);
        string name = entry?.DisplayName ?? cost.Item;
        string amount = cost.AmountMin == cost.AmountMax ? $"{cost.AmountMax}" : $"{cost.AmountMin}-{cost.AmountMax}";

        var grid = new Grid { ColumnSpacing = 14, Padding = new Thickness(12, 10, 12, 10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        grid.Children.Add(new TextBlock { Text = StageLabel(cost.Stage), FontSize = 13, Foreground = ThemeResources.Brush("ThemeTextDimBrush"), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
        if (entry?.Icon is { } icon)
        {
            var image = new Image { Source = icon, Width = 40, Height = 40, VerticalAlignment = VerticalAlignment.Top };
            Grid.SetColumn(image, 1);
            grid.Children.Add(image);
        }

        var text = new StackPanel { Spacing = 4 };
        text.Children.Add(new TextBlock { Text = $"{amount} × {name}", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = ThemeResources.Brush("ThemeTextBrush"), TextWrapping = TextWrapping.Wrap });
        foreach (var line in WhereToGet(cost.Item, name)) text.Children.Add(line);
        Grid.SetColumn(text, 2);
        grid.Children.Add(text);

        return new Border
        {
            Child = grid,
            Background = ThemeResources.Brush("ThemePanelAltBrush"),
            BorderBrush = ThemeResources.Brush("ThemeLineBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
        };
    }

    /// <summary>Lines saying where this item can be had, best lead first.</summary>
    private IEnumerable<UIElement> WhereToGet(string itemId, string name)
    {
        var lines = new List<UIElement>();

        // Trade goods: the economy that sells them, and known systems with it.
        if (_itemValues.TryGetValue(itemId, out var value) && value.TradeCategory is { Length: > 0 } category
            && category != "None" && category != "SpecialShop")
        {
            var economies = _economySells.Where(kv => kv.Value == category).Select(kv => EconomyLabels.GetValueOrDefault(kv.Key, kv.Key)).ToList();
            if (economies.Count > 0)
            {
                string economy = string.Join(" or ", economies);
                lines.Add(Dim($"Trade good - sold at the trade terminal in {economy} systems."));
                var systems = SystemsWithEconomy(economies).Take(3).ToList();
                if (systems.Count == 0)
                    lines.Add(Dim($"None of the systems you've visited are {economy}. The Galaxy Map's economy filter shows them nearby."));
                foreach (var (address, distance) in systems)
                    lines.Add(SystemLine(address, distance));
            }
        }

        // Finds that say they sell it.
        foreach (var find in FindsService.All.Where(f => f.Type == "Sells items" && f.ItemIds.Contains(itemId, StringComparer.OrdinalIgnoreCase)).Take(3))
            lines.Add(CodeLine($"Sold at: {(find.Title.Length > 0 ? find.Title : "a find")}", find.PortalCode));

        // Planets where the resource was recorded (details hold names as the game shows them).
        foreach (var (system, planet, _) in PlanetDetailsService.AllWithPlanets
                     .Where(p => p.Details.Resources.Any(r => string.Equals(r, name, StringComparison.CurrentCultureIgnoreCase)))
                     .OrderBy(p => Distance(p.System) ?? double.MaxValue).Take(3))
            lines.Add(CodeLine($"Found on {UniverseInfo.PlanetName(system, planet)} ({UniverseInfo.SystemName(system)}){DistanceText(Distance(system))}",
                S.PortalAddress(system, planet)));

        if (_craftable.Contains(itemId))
            lines.Add(Dim("Can be crafted - see Reference › Crafting."));
        if (_refinable.Contains(itemId))
            lines.Add(Dim("Can be made in a refiner - see Reference › Recipes."));
        if (lines.Count == 0)
            lines.Add(Dim("Not recorded anywhere yet. Add a find when you see it for sale, or the resource to a planet's details."));
        return lines;
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

    private UIElement SystemLine(ulong address, double? distance) =>
        CodeLine($"{UniverseInfo.SystemName(address)} · {UniverseInfo.Wealth(address)}{DistanceText(distance)}", S.PortalAddress(address, 1));

    /// <summary>A lead with its portal code and a Copy button.</summary>
    private static UIElement CodeLine(string text, string code)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new TextBlock { Text = text, FontSize = 13, Foreground = ThemeResources.Brush("ThemeTextBrush"), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
        if (code.Length > 0)
        {
            row.Children.Add(new TextBlock { Text = code, FontSize = 12, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono, Consolas"), Foreground = ThemeResources.Brush("ThemeTextDimBrush"), VerticalAlignment = VerticalAlignment.Center, IsTextSelectionEnabled = true });
            var copy = new Button { Content = "Copy", Padding = new Thickness(8, 1, 8, 1), FontSize = 12 };
            ToolTipService.SetToolTip(copy, "Copy the portal code");
            copy.Click += (_, _) =>
            {
                var data = new Windows.ApplicationModel.DataTransfer.DataPackage();
                data.SetText(code);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data);
            };
            row.Children.Add(copy);
        }
        return row;
    }

    private static TextBlock Dim(string text) => new()
    {
        Text = text, FontSize = 13, TextWrapping = TextWrapping.Wrap, Foreground = ThemeResources.Brush("ThemeTextDimBrush"),
    };
}
