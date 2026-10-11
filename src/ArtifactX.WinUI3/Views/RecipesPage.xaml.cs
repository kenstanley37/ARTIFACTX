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
/// Read-only recipe lookup (refiner and Nutrient Processor) from the catalog's
/// "Recipes" category - the game's own nms_reality_gcrecipetable.mbin, see
/// DataCataloger add-recipes. Doesn't need a save; amounts are the game's own
/// per-batch ratios.
/// </summary>
public sealed partial class RecipesPage : Page
{
    private List<RecipeInfo> _recipes = new();
    private Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase);
    private string? _selectedId;
    private bool _ready;

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
        _recipes = await recipesTask;
        _names = await namesTask;

        if (_recipes.Count == 0)
        {
            CountTxt.Text = "No recipes in this catalog - it needs rebuilding with the add-recipes step.";
            return;
        }

        await CatalogService.WarmCacheAsync(_recipes.SelectMany(r => r.Ingredients.Select(i => i.Id).Append(r.Result.Id)).Distinct());
        _ready = true;
        Apply();
    }

    private string NameOf(string id) => _names.TryGetValue(id, out var n) ? n : CatalogService.TryGet(id)?.DisplayName ?? id;

    private void Apply()
    {
        if (!_ready) return;
        bool uses = ModeBox.SelectedIndex == 1;
        int kind = KindBox.SelectedIndex;
        int inputs = InputsBox.SelectedIndex;
        string text = SearchBox.Text.Trim();

        bool Matches(string id) => _selectedId is not null
            ? string.Equals(id, _selectedId, StringComparison.OrdinalIgnoreCase)
            : NameOf(id).Contains(text, StringComparison.CurrentCultureIgnoreCase);

        var shown = _recipes
            .Where(r => kind == 0 || (kind == 1 ? r.Kind == "Refiner" : r.Kind == "Cooking"))
            .Where(r => inputs == 0 || r.Ingredients.Count <= inputs)
            .Where(r => text.Length == 0 && _selectedId is null
                || (uses ? r.Ingredients.Any(i => Matches(i.Id)) : Matches(r.Result.Id)))
            // Best yield first when looking up how to make something.
            .OrderByDescending(r => uses ? 0 : (double)r.Result.Amount / Math.Max(1, r.Ingredients.Sum(i => i.Amount)))
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
        sender.ItemsSource = text.Length < 2 ? null : _recipes
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

    private void Item_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string id) return;
        _selectedId = id;
        SearchBox.Text = NameOf(id);
        ModeBox.SelectedIndex = 0; // how to make the clicked item
        Apply();
        if (RecipesList.Items.Count > 0) RecipesList.ScrollIntoView(RecipesList.Items[0]);
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => Apply();
}
