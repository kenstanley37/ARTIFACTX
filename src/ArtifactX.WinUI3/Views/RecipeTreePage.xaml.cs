using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Views;

/// <summary>Read-only recipe route. Dependency calculation runs in the requirements service;
/// this page wires navigation, quantity and alternative selection to themed recipe cards.</summary>
public sealed partial class RecipeTreePage : Page
{
    private CancellationTokenSource? _cancellation;
    private int _version;
    private RecipeInfo? _target;
    private List<RecipeInfo> _recipes = new();
    private Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _choices = new(StringComparer.OrdinalIgnoreCase);

    public RecipeTreePage() => InitializeComponent();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        int version = ++_version;
        StatusTxt.Text = "Loading recipe steps...";
        try
        {
            var recipesTask = CatalogService.GetRecipesAsync();
            var namesTask = CatalogService.GetItemNamesAsync();
            var recipes = await recipesTask;
            var names = await namesTask;
            if (version != _version) return;
            _recipes = recipes;
            _names = names;
            _target = recipes.FirstOrDefault(r => string.Equals(r.Id, e.Parameter as string, StringComparison.OrdinalIgnoreCase));
            await UpdateAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RecipeTreePage] {ex}");
            if (version == _version) StatusTxt.Text = "Unable to load recipes. Go back and try again.";
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        ++_version;
        _cancellation?.Cancel();
        base.OnNavigatedFrom(e);
    }

    private async void Quantity_ValueChanged(object sender, NumberBoxValueChangedEventArgs e)
    { if (_target is not null) await UpdateAsync(); }

    private async void Recipe_Changed(string item, string recipe)
    {
        _choices[item] = recipe;
        await UpdateAsync();
    }

    private async Task UpdateAsync()
    {
        if (_target is not { } target) { StatusTxt.Text = "Open a recipe from the Recipes page."; return; }
        _cancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        int quantity = double.IsFinite(QuantityBox.Value) ? Math.Clamp((int)QuantityBox.Value, 1, 99999) : 1;
        try
        {
            var plan = await RecipeRequirementsService.BuildAsync(target, _recipes, quantity,
                new Dictionary<string, string>(_choices, StringComparer.OrdinalIgnoreCase), cancellation.Token);
            if (cancellation.IsCancellationRequested) return;
            var ids = plan.Steps.SelectMany(s => s.Recipe.Ingredients.Select(i => i.Id).Append(s.Recipe.ResultId))
                .Concat(plan.Gather.Select(i => i.Id)).Distinct(StringComparer.OrdinalIgnoreCase);
            await CatalogService.WarmCacheAsync(ids);
            if (cancellation.IsCancellationRequested) return;
            var cards = RecipePlanPresentation.Create(plan, target.Id, _recipes, _names);
            // Reconcile rows in place so quantity/alternative changes keep the page's scroll position.
            while (Cards.Items.Count > cards.Count) Cards.Items.RemoveAt(Cards.Items.Count - 1);
            for (int i = 0; i < cards.Count; i++)
                if (i < Cards.Items.Count) Cards.Items[i] = cards[i]; else Cards.Items.Add(cards[i]);
            GatherTxt.Text = string.Join("   +   ", plan.Gather.Select(i => $"{i.Amount:N0} x {NameOf(i.Id)}"));
            TargetTxt.Text = $"{quantity:N0} x {NameOf(target.Result.Id)}";
            StatusTxt.Text = $"{cards.Count} steps, in order. {plan.Notice}";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RecipeTreePage] {ex}");
            if (!cancellation.IsCancellationRequested)
                StatusTxt.Text = ex is InvalidOperationException ? ex.Message : "Unable to calculate this route. Try a smaller quantity or another recipe.";
        }
        finally { if (ReferenceEquals(_cancellation, cancellation)) _cancellation = null; }
    }

    private string NameOf(string id) => _names.GetValueOrDefault(id) ?? "Unknown ingredient";
    private void Back_Click(object sender, RoutedEventArgs e) { if (Frame.CanGoBack) Frame.GoBack(); }
}
