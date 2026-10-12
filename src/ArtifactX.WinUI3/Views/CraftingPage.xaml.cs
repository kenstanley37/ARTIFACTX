using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Tools > Crafting: expands what a product or technology takes, down to raw
/// materials (CatalogService.GetCraftingAsync - the game's own
/// GcProductData/GcTechnology Requirements). Anything without requirements
/// counts as raw; raw materials a refiner can also make say so (refiner
/// recipes from CatalogService.GetRecipesAsync). Read-only.
/// </summary>
public sealed partial class CraftingPage : Page
{
    // Deepest real chain is well under this; it only guards against bad data.
    private const int MaxDepth = 12;

    private Dictionary<string, CraftInfo> _craft = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, List<RecipeInfo>> _refinedFrom = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _searchLabels = new(StringComparer.CurrentCultureIgnoreCase);
    private readonly Stack<string> _history = new();
    private string? _target;
    private List<CraftRawRowViewModel> _raw = new();

    public CraftingPage()
    {
        InitializeComponent();
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        _craft = await CatalogService.GetCraftingAsync();
        _names = await CatalogService.GetItemNamesAsync();
        _refinedFrom = (await CatalogService.GetRecipesAsync())
            .Where(r => r.Kind != "Cooking")
            .GroupBy(r => r.Result.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // Search labels: technology is marked so it can't be confused with a
        // product of the same name; repeated names get the id.
        foreach (var c in _craft.Values.OrderBy(c => c.Kind).ThenBy(c => c.Id))
        {
            string label = c.Kind == "Technology" ? $"{NameOf(c.Id)} (technology)" : NameOf(c.Id);
            if (_searchLabels.ContainsKey(label)) label = $"{label} [{c.Id}]";
            _searchLabels[label] = c.Id;
        }
        if (_craft.Count == 0) EmptyTxt.Text = "The crafting list isn't in this app's game data yet.";
    }

    // ---------------------------------------------------------------- picking

    private void ItemSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        string text = sender.Text.Trim();
        sender.ItemsSource = text.Length < 2 ? null : _searchLabels.Keys
            .Where(l => l.Contains(text, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(l => l.StartsWith(text, StringComparison.CurrentCultureIgnoreCase) ? 0 : 1).ThenBy(l => l)
            .Take(15).ToList();
    }

    private void ItemSearch_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is string label && _searchLabels.TryGetValue(label, out var id))
        {
            _history.Clear();
            _ = ShowAsync(id);
        }
    }

    private void ItemSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is not null) return;
        string text = sender.Text.Trim();
        string? label = _searchLabels.Keys.FirstOrDefault(l => string.Equals(l, text, StringComparison.CurrentCultureIgnoreCase))
            ?? _searchLabels.Keys.Where(l => l.Contains(text, StringComparison.CurrentCultureIgnoreCase)).OrderBy(l => l.Length).FirstOrDefault();
        if (text.Length >= 2 && label is not null)
        {
            _history.Clear();
            _ = ShowAsync(_searchLabels[label]);
        }
    }

    private void Quantity_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_target is not null) _ = ShowAsync(_target);
    }

    private void TreeList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not CraftTreeRowViewModel row || !row.IsCrafted || _target is null) return;
        _history.Push(_target);
        _ = ShowAsync(row.Id);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_history.Count > 0) _ = ShowAsync(_history.Pop());
    }

    // ---------------------------------------------------------------- building

    private int Quantity => double.IsNaN(QuantityBox.Value) ? 1 : Math.Max(1, (int)QuantityBox.Value);

    private async Task ShowAsync(string id)
    {
        if (!_craft.TryGetValue(id, out var target)) return;
        _target = id;
        int quantity = Quantity;

        // Walk the tree once for ids (icons), then build the rows.
        var tree = new List<(string Id, long Amount, int Depth)>();
        var raw = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        void Walk(string itemId, long amount, int depth)
        {
            foreach (var (reqId, reqAmount) in _craft.TryGetValue(itemId, out var c) ? c.Requirements : new())
            {
                long needed = amount * reqAmount;
                tree.Add((reqId, needed, depth));
                if (_craft.ContainsKey(reqId) && depth < MaxDepth) Walk(reqId, needed, depth + 1);
                else raw[reqId] = raw.GetValueOrDefault(reqId) + needed;
            }
        }
        Walk(id, quantity, 0);

        await CatalogService.WarmCacheAsync(tree.Select(t => t.Id).Append(id)
            .Concat(_refinedFrom.Where(r => raw.ContainsKey(r.Key)).SelectMany(r => r.Value.SelectMany(x => x.Ingredients.Select(i => i.Id)))));
        if (_target != id) return; // a newer pick started meanwhile

        TargetIcon.Source = CatalogService.TryGet(id)?.Icon;
        TargetNameTxt.Text = quantity > 1 ? $"{NameOf(id)} × {quantity:N0}" : NameOf(id);
        int crafted = tree.Count(t => _craft.ContainsKey(t.Id));
        TargetKindTxt.Text = (target.Kind == "Technology" ? "Technology - cost to build or install" : "Crafted product")
            + $" · {crafted} crafted part{(crafted == 1 ? "" : "s")}, {raw.Count} raw material{(raw.Count == 1 ? "" : "s")}";

        TreeList.ItemsSource = tree.Select(t =>
        {
            bool isCrafted = _craft.ContainsKey(t.Id);
            return new CraftTreeRowViewModel
            {
                Id = t.Id,
                Name = NameOf(t.Id),
                Icon = CatalogService.TryGet(t.Id)?.Icon,
                Amount = $"× {t.Amount:N0}",
                Indent = new Thickness(t.Depth * 26, 0, 0, 0),
                Tag = isCrafted ? "CRAFTED" : "RAW",
                TagBrush = ThemeResources.Brush(isCrafted ? "ThemeAccentBrush" : "ThemeTextDimBrush"),
                IsCrafted = isCrafted,
                ToolTip = isCrafted ? $"Click to open {NameOf(t.Id)} on its own" : "A raw material - gather, buy or refine it",
            };
        }).ToList();

        _raw = raw.OrderByDescending(r => r.Value).ThenBy(r => NameOf(r.Key)).Select(r =>
        {
            var recipes = _refinedFrom.GetValueOrDefault(r.Key) ?? new();
            return new CraftRawRowViewModel
            {
                Id = r.Key,
                Name = NameOf(r.Key),
                Icon = CatalogService.TryGet(r.Key)?.Icon,
                Total = r.Value.ToString("N0", CultureInfo.CurrentCulture),
                RefineHint = recipes.Count == 0 ? "" : $"Can also be made in a refiner ({recipes.Count} recipe{(recipes.Count == 1 ? "" : "s")})",
                RefineVisibility = recipes.Count == 0 ? Visibility.Collapsed : Visibility.Visible,
                RefineToolTip = string.Join("\n", recipes.Take(6).Select(x =>
                    string.Join(" + ", x.Ingredients.Select(i => $"{NameOf(i.Id)} × {i.Amount}")) + $"  →  × {x.Result.Amount}")),
            };
        }).ToList();
        RawList.ItemsSource = _raw;
        RawSummaryTxt.Text = $"{_raw.Count} material{(_raw.Count == 1 ? "" : "s")}, {raw.Values.Sum():N0} units in all, counted from scratch - anything you already carry comes off these totals.";

        BackBtn.Visibility = _history.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_history.Count > 0) BackTxt.Text = $"Back to {NameOf(_history.Peek())}";
        EmptyTxt.Visibility = Visibility.Collapsed;
        ResultGrid.Visibility = Visibility.Visible;
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (_target is null) return;
        await CsvExportService.ExportAsync(ExportBtn, $"ArtifactX {NameOf(_target)} materials",
            new[] { "Material", "Amount", "Also from a refiner" },
            _raw.Select(r => new[] { r.Name, r.Total, r.RefineHint.Length > 0 ? "Yes" : "" }));
    }

    private string NameOf(string id) =>
        _names.TryGetValue(id, out var n) ? n
        : _craft.TryGetValue(id, out var c) && c.Name.Length > 0 ? c.Name
        : CatalogService.TryGet(id)?.DisplayName is { Length: > 0 } raw
            ? CultureInfo.InvariantCulture.TextInfo.ToTitleCase(raw.ToLowerInvariant()) : id;
}
