using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

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
    private CancellationTokenSource? _buildCancellation;
    private int _loadVersion;

    private List<RecipeInfo> _recipes = new();
    private Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase);
    private string? _recipeId;
    private bool _ready;

    public RecipeTreePage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _loadVersion++;
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
        int version = _loadVersion;
        try
        {
            var recipesTask = CatalogService.GetRecipesAsync();
            var namesTask = CatalogService.GetItemNamesAsync();
            var recipes = await recipesTask;
            var names = await namesTask;

            if (version != _loadVersion) return;
            _recipes = recipes;
            _names = names;

            if (_recipes.Count == 0)
            {
                EmptyTxt.Text = "No recipes in this catalog - it needs rebuilding with the add-recipes step.";
                return;
            }

            _ready = true;
            await ApplyAsync();
        }
        catch (Exception)
        {
            if (version == _loadVersion) EmptyTxt.Text = "Unable to load recipes. Go back and try again.";
        }
    }

    private int Quantity => double.IsNaN(QuantityBox.Value) ? 1 : Math.Clamp((int)QuantityBox.Value, 1, 99999);

    private async void Quantity_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.NumberBoxValueChangedEventArgs e) => await ApplyAsync();

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _loadVersion++;
        _ready = false;
        _buildCancellation?.Cancel();
        base.OnNavigatedFrom(e);
    }

    private async Task ApplyAsync()
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

        _buildCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _buildCancellation = cancellation;
        List<RecipeTreeRowViewModel> rows;
        try
        {
            var requirements = await RecipeRequirementsService.BuildAsync(recipe, _recipes, quantity, cancellation.Token);
            if (cancellation.IsCancellationRequested || !_ready) return;
            await CatalogService.WarmCacheAsync(requirements.Select(r => r.ItemId).Append(recipe.Result.Id).Distinct(StringComparer.OrdinalIgnoreCase));
            if (cancellation.IsCancellationRequested || !_ready) return;
            rows = requirements.Select(r => new RecipeTreeRowViewModel
            {
                Label = r.StopReason is not null && r.IsStep ? r.StopReason
                    : r.IsStep ? $"Alternative via {StationLabel(r.Kind)} ? ? {r.Amount:N0} batches" : r.StopReason is not null ? $"{NameOf(r.ItemId)} ? {r.StopReason}" : NameOf(r.ItemId),
                Icon = CatalogService.TryGet(r.ItemId)?.Icon,
                Amount = r.IsStep ? "" : $"? {r.Amount:N0}",
                Depth = r.Depth,
                Indent = new Thickness(r.Depth * 26, 0, 0, 0),
                Badge = r.StopReason is not null ? "STOP" : string.IsNullOrEmpty(r.Kind) ? "RAW" : StationLabel(r.Kind),
                BadgeBrush = ThemeResources.Brush("ThemeAccentBrush"),
                ToolTip = r.StopReason ?? (r.IsStep ? "Choose this alternative to make the ingredient above." : "Total required for the quantity selected."),
                IsRecipeStep = r.IsStep,
            }).ToList();
        }
        catch (OperationCanceledException) { return; }
        catch (Exception)
        {
            if (!cancellation.IsCancellationRequested)
            {
                EmptyTxt.Text = "Unable to load the recipe requirements. Go back and try again.";
                EmptyTxt.Visibility = Visibility.Visible;
            }
            return;
        }
        finally
        {
            if (ReferenceEquals(_buildCancellation, cancellation)) _buildCancellation = null;
            cancellation.Dispose();
        }

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

    private static string StationLabel(string kind) => kind == "Cooking" ? "Nutrient Processor" : "Refiner";

    private string NameOf(string id) => _names.TryGetValue(id, out var n) ? n : CatalogService.TryGet(id)?.DisplayName ?? "Unknown ingredient";

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
