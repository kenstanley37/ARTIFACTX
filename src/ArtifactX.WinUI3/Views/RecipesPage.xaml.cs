using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Read-only recipe lookup (refiner and Nutrient Processor) from the catalog's
/// "Recipes" category - the game's own nms_reality_gcrecipetable.mbin, see
/// DataCataloger add-recipes. Doesn't need a save; amounts are the game's own
/// per-batch ratios.
/// </summary>
public sealed partial class RecipesPage : Page
{
    private List<RecipeInfo> _recipes = new();
    private Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _types = new(StringComparer.OrdinalIgnoreCase);
    private string? _selectedId;
    private bool _ready;
    private string? _highlightedId;

    public RecipesPage()
    {
        InitializeComponent();
        ModeBox.SelectedIndex = 0;
        KindBox.SelectedIndex = 0;
        InputsBox.SelectedIndex = 0;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        CountTxt.Text = "Loading recipes...";
        var recipesTask = CatalogService.GetRecipesAsync();
        var namesTask = CatalogService.GetItemNamesAsync();
        var typesTask = CatalogService.GetItemTypesAsync();
        _recipes = await recipesTask;
        _names = await namesTask;
        _types = await typesTask;

        if (_recipes.Count == 0)
        {
            CountTxt.Text = "No recipes in this catalog - it needs rebuilding with the add-recipes step.";
            return;
        }

        await CatalogService.WarmCacheAsync(_recipes.SelectMany(r => r.Ingredients.Select(i => i.Id).Append(r.Result.Id)).Distinct());

        TypeBox.Items.Add(new ComboBoxItem { Content = $"Anything ({_recipes.Count:N0})", Tag = "" });
        foreach (var group in _recipes.GroupBy(r => TypeOf(r.Result.Id)).OrderByDescending(g => g.Count()))
            TypeBox.Items.Add(new ComboBoxItem { Content = $"{group.Key} ({group.Count():N0})", Tag = group.Key });
        TypeBox.SelectedIndex = 0;
        _ready = true;
        Apply();
        RefreshPinned();
    }

    /// <summary>Rebuilds the Pinned strip (newest pin last).</summary>
    private void RefreshPinned()
    {
        var byId = _recipes.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);
        var pinned = PinnedRecipesService.All.Where(byId.ContainsKey).Select(id => ToRow(byId[id])).ToList();
        PinnedList.ItemsSource = pinned;
        PinnedHeaderTxt.Text = $"PINNED ({pinned.Count})";
        PinnedPanel.Visibility = pinned.Count > 0 && PinnedOnlyBox.IsChecked != true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string id) return;
        bool pinned = PinnedRecipesService.Toggle(id);
        // Update the card in the grid in place, so the grid doesn't jump.
        if (RecipesList.ItemsSource is List<RecipeRowViewModel> rows)
            foreach (var row in rows.Where(r => string.Equals(r.RecipeId, id, StringComparison.OrdinalIgnoreCase)))
                row.IsPinned = pinned;
        if (PinnedOnlyBox.IsChecked == true) Apply(); // an unpinned card leaves the list
        RefreshPinned();
    }

    private string TypeOf(string id) => _types.TryGetValue(id, out var t) ? t : "Crafted products";

    private string NameOf(string id) => _names.TryGetValue(id, out var n) ? n : CatalogService.TryGet(id)?.DisplayName ?? id;

    private void Apply()
    {
        if (!_ready) return;
        bool uses = ModeBox.SelectedIndex == 1;
        int kind = KindBox.SelectedIndex;
        int inputs = InputsBox.SelectedIndex;
        string text = SearchBox.Text.Trim();
        string type = (TypeBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        bool searching = text.Length > 0 || _selectedId is not null;

        bool Matches(string id) => _selectedId is not null
            ? string.Equals(id, _selectedId, StringComparison.OrdinalIgnoreCase)
            : NameOf(id).Contains(text, StringComparison.CurrentCultureIgnoreCase);

        var shown = _recipes
            .Where(r => kind == 0 || (kind == 1 ? r.Kind == "Refiner" : r.Kind == "Cooking"))
            .Where(r => inputs == 0 || r.Ingredients.Count <= inputs)
            .Where(r => type.Length == 0 || TypeOf(r.Result.Id) == type)
            .Where(r => PinnedOnlyBox.IsChecked != true || PinnedRecipesService.IsPinned(r.Id))
            .Where(r => text.Length == 0 && _selectedId is null
                || (uses ? r.Ingredients.Any(i => Matches(i.Id)) : Matches(r.Result.Id)))
            // Browsing (no search): alphabetical by what it makes, so a type reads
            // like a list. Searching how to make something: best yield first.
            .OrderBy(r => searching ? "" : NameOf(r.Result.Id), StringComparer.CurrentCultureIgnoreCase)
            .ThenByDescending(r => uses || !searching ? 0 : (double)r.Result.Amount / Math.Max(1, r.Ingredients.Sum(i => i.Amount)))
            .ThenBy(r => r.Ingredients.Count)
            .Select(ToRow)
            .ToList();

        RecipesList.ItemsSource = shown;
        CountTxt.Text = $"{shown.Count:N0} of {_recipes.Count:N0} recipes";
    }

    private RecipeRowViewModel ToRow(RecipeInfo r)
    {
        RecipeItemViewModel Item((string Id, int Amount) e) => new()
        {
            ItemId = e.Id,
            Label = $"{e.Amount} × {NameOf(e.Id)}",
            Icon = CatalogService.TryGet(e.Id)?.Icon,
        };

        string station = r.Kind == "Cooking" ? "Nutrient Processor"
            : r.Ingredients.Count switch { 1 => "Any refiner", 2 => "Medium or Large Refiner", _ => "Large Refiner" };

        return new RecipeRowViewModel
        {
            RecipeId = r.Id,
            IsPinned = PinnedRecipesService.IsPinned(r.Id),
            IsHighlighted = string.Equals(r.Id, _highlightedId, StringComparison.OrdinalIgnoreCase),
            Name = r.Name.ToUpperInvariant(),
            Station = station,
            Ingredients = r.Ingredients.Select(Item).ToList(),
            Result = Item(r.Result),
        };
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        _selectedId = null;
        string text = sender.Text.Trim();
        string type = (TypeBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        sender.ItemsSource = text.Length < 2 ? null : _recipes
            .Where(r => type.Length == 0 || TypeOf(r.Result.Id) == type)
            .SelectMany(r => r.Ingredients.Select(i => i.Id).Append(r.Result.Id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(NameOf)
            .Where(n => n.Contains(text, StringComparison.CurrentCultureIgnoreCase))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(n => n.StartsWith(text, StringComparison.CurrentCultureIgnoreCase) ? 0 : 1).ThenBy(n => n)
            .Take(12)
            .ToList();
        Apply();
    }

    private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        string name = args.SelectedItem as string ?? "";
        _selectedId = _names.FirstOrDefault(kv => string.Equals(kv.Value, name, StringComparison.CurrentCultureIgnoreCase)).Key;
        sender.Text = name;
        Apply();
    }

    /// <summary>Tap the card background to open the full requirements tree for
    /// that recipe on its own page. Taps on the pin button or the result
    /// button are handled by those buttons' own clicks.</summary>
    private void Card_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (e.OriginalSource is DependencyObject src && IsInsideButton(src)) return;
        if ((sender as FrameworkElement)?.Tag is not string id) return;
        this.Frame?.Navigate(typeof(RecipeTreePage), id);
    }

    /// <summary>Click the recipe's result to open the full requirements tree
    /// for that recipe on its own page (keyboard reachable).</summary>
    private void OpenTree(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string id) return;
        this.Frame?.Navigate(typeof(RecipeTreePage), id);
    }

    /// <summary>True when the tapped element sits inside a button, whose own
    /// click handles the tap.</summary>
    private static bool IsInsideButton(DependencyObject d)
    {
        while (d is not null)
        {
            if (d is Button) return true;
            d = VisualTreeHelper.GetParent(d);
        }
        return false;
    }

    /// <summary>A pinned tile was clicked: highlight it and its card, and
    /// scroll the grid to the card. If the filters hide it, they're cleared.
    /// Clicking the highlighted pin again turns the highlight off.</summary>
    private void PinnedTile_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string id) return;
        bool turnOff = string.Equals(id, _highlightedId, StringComparison.OrdinalIgnoreCase);
        _highlightedId = turnOff ? null : id;

        foreach (var list in new[] { PinnedList.ItemsSource, RecipesList.ItemsSource }.OfType<List<RecipeRowViewModel>>())
            foreach (var row in list)
                row.IsHighlighted = string.Equals(row.RecipeId, _highlightedId, StringComparison.OrdinalIgnoreCase);
        if (turnOff) return;

        var card = (RecipesList.ItemsSource as List<RecipeRowViewModel>)?
            .FirstOrDefault(r => string.Equals(r.RecipeId, id, StringComparison.OrdinalIgnoreCase));
        if (card is null)
        {
            // Hidden by the current filters - show everything, then find it.
            _selectedId = null;
            SearchBox.Text = "";
            TypeBox.SelectedIndex = 0;
            KindBox.SelectedIndex = 0;
            InputsBox.SelectedIndex = 0;
            Apply();
            card = (RecipesList.ItemsSource as List<RecipeRowViewModel>)?
                .FirstOrDefault(r => string.Equals(r.RecipeId, id, StringComparison.OrdinalIgnoreCase));
        }
        if (card is not null) RecipesList.ScrollIntoView(card, ScrollIntoViewAlignment.Leading);
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => Apply();

    /// <summary>"Pinned only" shows the pins in the main grid, so the separate
    /// Pinned strip is hidden meanwhile (it would show the same cards twice).</summary>
    private void PinnedOnly_Click(object sender, RoutedEventArgs e)
    {
        Apply();
        RefreshPinned();
    }
}
