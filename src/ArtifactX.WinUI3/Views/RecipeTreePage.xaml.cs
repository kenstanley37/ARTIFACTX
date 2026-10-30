using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// Full-page requirements tree for one recipe: the result at the top, then every
/// ingredient, then every ingredient's own recipes, step by step down to raw
/// materials. Read-only - no save is needed; amounts are the game's own
/// per-batch ratios from the catalog's "Recipes" category (DataCataloger
/// add-recipes). Navigated to from RecipesPage with the recipe id as the
/// Frame parameter.
/// </summary>
public sealed partial class RecipeTreePage : Page
{
    // Deepest real chain is well under this; it only guards against bad data.
    private const int MaxDepth = 16;

    private List<RecipeInfo> _recipes = new();
    private Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, List<RecipeInfo>> _byResult = new(StringComparer.OrdinalIgnoreCase);
    private string? _recipeId;
    private bool _ready;

    public RecipeTreePage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _recipeId = e.Parameter as string;
        _ready = false;
        TreeList.ItemsSource = null;
        ResultGrid.Visibility = Visibility.Collapsed;
        EmptyTxt.Visibility = Visibility.Visible;
        EmptyTxt.Text = _recipeId is null
            ? "Open this page from a recipe on the Recipes page - click any recipe card to see its full requirements tree."
            : "Loading recipe...";
        TargetIcon.Source = null;
        TargetNameTxt.Text = "";
        TargetKindTxt.Text = "";
        TargetBigTxt.Text = "";
        TargetBatchTxt.Text = "";
        TargetStationTxt.Text = "";
        TargetCountTxt.Text = "";
        CountTxt.Text = "";
        UpdateBackButton();
        _ = LoadAsync();
        base.OnNavigatedTo(e);
    }

    private async Task LoadAsync()
    {
        var recipesTask = CatalogService.GetRecipesAsync();
        var namesTask = CatalogService.GetItemNamesAsync();
        _recipes = await recipesTask;
        _names = await namesTask;

        if (_recipes.Count == 0)
        {
            EmptyTxt.Text = "No recipes in this catalog - it needs rebuilding with the add-recipes step.";
            return;
        }

        _byResult = _recipes.GroupBy(r => r.Result.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Kind, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Id, StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.OrdinalIgnoreCase);

        _ready = true;
        Apply();
    }

    private int Quantity => double.IsNaN(QuantityBox.Value) ? 1 : Math.Clamp((int)QuantityBox.Value, 1, 99999);

    private void Quantity_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.NumberBoxValueChangedEventArgs e) => Apply();

    private void Apply()
    {
        if (!_ready) return;
        if (string.IsNullOrEmpty(_recipeId))
        {
            EmptyTxt.Text = "No recipe selected - open this page from a recipe card on the Recipes page.";
            return;
        }

        RecipeInfo? recipe = _recipes.FirstOrDefault(r => string.Equals(r.Id, _recipeId, StringComparison.OrdinalIgnoreCase));
        if (recipe is null)
        {
            EmptyTxt.Text = "That recipe isn't in this catalog - it may have been removed by a game data rebuild.";
            return;
        }

        int quantity = Quantity;
        int resultAmount = recipe.Result.Amount <= 0 ? 1 : recipe.Result.Amount;
        long batches = (long)Math.Ceiling((double)quantity / resultAmount);

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { recipe.Result.Id };
        var rows = BuildTree(recipe, batches, 0, ids);

        // Icons for everything the tree shows, fetched once in the background.
        _ = CatalogService.WarmCacheAsync(ids);
        if (!string.Equals(_recipeId, recipe.Id, StringComparison.OrdinalIgnoreCase)) return; // a newer pick started meanwhile

        EmptyTxt.Visibility = Visibility.Collapsed;
        ResultGrid.Visibility = Visibility.Visible;

        TargetIcon.Source = CatalogService.TryGet(recipe.Result.Id)?.Icon;
        string resultName = NameOf(recipe.Result.Id);
        string resultLabel = quantity > 1 ? $"{resultName} × {quantity:N0}" : resultName;
        TargetNameTxt.Text = resultLabel;
        TargetBigTxt.Text = resultLabel;
        TargetBatchTxt.Text = $"Makes ×{resultAmount:N0} per batch · {batches} batch{(batches == 1 ? "" : "es")} for ×{quantity:N0}";
        TargetStationTxt.Text = recipe.Kind == "Cooking"
            ? "Nutrient Processor"
            : $"Refiner · {recipe.Ingredients.Count} input{(recipe.Ingredients.Count == 1 ? "" : "s")}";

        TreeList.ItemsSource = rows;
        int raw = rows.Count(r => !r.IsRecipeStep && string.Equals(r.Badge, "RAW", StringComparison.OrdinalIgnoreCase));
        TargetCountTxt.Text = $"{rows.Count:N0} requirement{(rows.Count == 1 ? "" : "s")}, {raw} raw material{(raw == 1 ? "" : "s")}";
        CountTxt.Text = TargetCountTxt.Text;

        UpdateBackButton();
    }

    /// <summary>Walks one recipe's ingredients, then every recipe that can make
    /// each ingredient, depth-first. Amounts multiply down the tree, so a row's
    /// amount is the total needed of that item for the quantity asked for.</summary>
    private List<RecipeTreeRowViewModel> BuildTree(RecipeInfo recipe, long multiplier, int depth, HashSet<string> ids)
    {
        var rows = new List<RecipeTreeRowViewModel>();
        void Walk(RecipeInfo r, long mult, int d)
        {
            foreach (var (ingId, ingAmt) in r.Ingredients)
            {
                if (string.IsNullOrEmpty(ingId) || ingAmt <= 0) continue;
                ids.Add(ingId);
                long needed = ingAmt * mult;
                bool crafted = _byResult.TryGetValue(ingId, out var subs) && subs.Count > 0;
                string station = StationLabel(crafted ? subs[0].Kind : "");
                rows.Add(new RecipeTreeRowViewModel
                {
                    Label = NameOf(ingId),
                    Icon = CatalogService.TryGet(ingId)?.Icon,
                    Amount = $"× {needed:N0}",
                    Depth = d,
                    Indent = new Thickness(d * 26, 0, 0, 0),
                    Badge = crafted ? station : "RAW",
                    BadgeBrush = ThemeResources.Brush(crafted ? "ThemeAccentBrush" : "ThemeTextDimBrush"),
                    ToolTip = crafted
                        ? $"Crafted: {subs.Count} recipe{(subs.Count == 1 ? "" : "s")} can make this item."
                        : "Raw material - gather it, buy it, or get it from a refiner.",
                    IsRecipeStep = false,
                });
                if (crafted && d < MaxDepth)
                {
                    foreach (var sub in subs)
                    {
                        long subBatches = needed <= 0 || sub.Result.Amount <= 0 ? 1 : (long)Math.Ceiling((double)needed / sub.Result.Amount);
                        ids.Add(sub.Result.Id);
                        rows.Add(new RecipeTreeRowViewModel
                        {
                            Label = $"via {StationLabel(sub.Kind)} · × {subBatches:N0} batch{(subBatches == 1 ? "" : "es")}",
                            Depth = d + 1,
                            Indent = new Thickness((d + 1) * 26, 0, 0, 0),
                            Badge = "",
                            BadgeBrush = ThemeResources.Brush("ThemeLineBrush"),
                            Amount = "",
                            ToolTip = $"Run this {StationLabel(sub.Kind)} recipe × {subBatches:N0} time{(subBatches == 1 ? "" : "s")} to make the ×{needed:N0} {NameOf(ingId)} above.",
                            IsRecipeStep = true,
                        });
                        Walk(sub, subBatches, d + 1);
                    }
                }
            }
        }
        Walk(recipe, multiplier, depth);
        return rows;
    }

    private static string StationLabel(string kind) => kind == "Cooking" ? "Nutrient Processor" : "Refiner";

    private string NameOf(string id) => _names.TryGetValue(id, out var n) ? n : CatalogService.TryGet(id)?.DisplayName ?? id;

    private void UpdateBackButton()
    {
        bool canGo = this.Frame is { CanGoBack: true };
        BackBtn.Visibility = canGo ? Visibility.Visible : Visibility.Collapsed;
        BackTxt.Text = canGo ? "Back to recipes" : "";
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        this.Frame?.GoBack();
    }
}