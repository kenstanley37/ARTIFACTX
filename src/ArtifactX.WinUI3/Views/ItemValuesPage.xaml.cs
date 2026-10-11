using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Read-only base values of every product and substance (catalog
/// "ItemValues", DataCataloger add-item-values). The game's TradeCategory on
/// the 35 trade goods says which economy deals in them; mapped here to the
/// economy names the Galaxy Map uses (and that Planets' system notes record).
/// </summary>
public sealed partial class ItemValuesPage : Page
{
    // Trade category -> economy type, as the system info panel names them.
    private static readonly (string Category, string Economy)[] Economies =
    {
        ("Commodity", "Trading"),
        ("Mineral", "Mining"),
        ("Component", "Manufacturing"),
        ("Tech", "Technology"),
        ("Exotic", "Scientific"),
        ("Energy", "Power Generation"),
        ("Alloy", "Advanced Materials"),
    };

    private List<ItemValueInfo> _items = new();
    private bool _ready;

    public ItemValuesPage()
    {
        InitializeComponent();
        KindBox.SelectedIndex = 0;
        SortBox.SelectedIndex = 0;
        EconomyBox.Items.Add(new ComboBoxItem { Content = "Any economy", Tag = "" });
        foreach (var (category, economy) in Economies)
            EconomyBox.Items.Add(new ComboBoxItem { Content = economy, Tag = category });
        EconomyBox.SelectedIndex = 0;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        CountTxt.Text = "Loading values...";
        _items = await CatalogService.GetItemValuesAsync();
        if (_items.Count == 0)
        {
            CountTxt.Text = "No values in this catalog - it needs rebuilding with the add-item-values step.";
            return;
        }
        await CatalogService.WarmCacheAsync(_items.Select(i => i.Id));
        _ready = true;
        Apply();
    }

    private static string? EconomyFor(string category) =>
        Economies.FirstOrDefault(e => e.Category == category).Economy;

    private void Apply()
    {
        if (!_ready) return;
        string text = SearchBox.Text.Trim();
        int kind = KindBox.SelectedIndex;
        string economy = (EconomyBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        bool hideZero = HideZeroBox.IsChecked == true;

        var filtered = _items
            .Where(i => text.Length == 0 || i.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase))
            .Where(i => kind switch
            {
                1 => i.Kind == "Product",
                2 => i.Kind == "Substance",
                3 => EconomyFor(i.TradeCategory) is not null,
                _ => true,
            })
            .Where(i => economy.Length == 0 || i.TradeCategory == economy)
            .Where(i => !hideZero || i.Value > 0);

        var byName = StringComparer.CurrentCultureIgnoreCase;
        var rows = (SortBox.SelectedIndex switch
        {
            1 => filtered.OrderBy(i => i.Value).ThenBy(i => i.Name, byName),
            2 => filtered.OrderBy(i => i.Name, byName),
            3 => filtered.OrderByDescending(i => i.Name, byName),
            _ => filtered.OrderByDescending(i => i.Value).ThenBy(i => i.Name, byName),
        }).Select(ToRow).ToList();

        ItemsList.ItemsSource = rows;
        CountTxt.Text = $"{rows.Count:N0} of {_items.Count:N0} items";
    }

    private static ItemValueRowViewModel ToRow(ItemValueInfo i)
    {
        var parts = new List<string> { i.Kind == "Substance" ? "Raw substance" : "Product" };
        if (EconomyFor(i.TradeCategory) is string economy) parts.Add($"Trade good · {economy} economies");
        if (i.Legality == "Illegal") parts.Add("Contraband - sold at outlaw stations");
        return new ItemValueRowViewModel
        {
            Name = i.Name,
            Detail = string.Join(" · ", parts),
            Value = $"{i.Value:N0} u",
            Icon = CatalogService.TryGet(i.Id)?.Icon,
        };
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => Apply();

    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => Apply();

    private void Filter_Click(object sender, RoutedEventArgs e) => Apply();
}
