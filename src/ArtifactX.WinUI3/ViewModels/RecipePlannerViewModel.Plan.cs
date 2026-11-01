using ArtifactX.Core.Recipes;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels;

public sealed partial class RecipePlannerViewModel
{
    private async Task RebuildAsync()
    {
        if (_target is not { } target || _catalog is not { } catalog) return;
        _planPending = true;
        _planning?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _planning = cancellation;
        Status = "Calculating requirements…";
        try
        {
            int quantity = double.IsFinite(Quantity) ? (int)Math.Clamp(Quantity, 1, 99999) : 1;
            var plan = await RecipeRequirementsService.BuildAsync(target, catalog.Recipes, quantity,
                new Dictionary<string, string>(_choices, StringComparer.OrdinalIgnoreCase), cancellation.Token);
            var ids = plan.Steps.SelectMany(s => s.Recipe.Ingredients.Select(i => i.Id).Append(s.Recipe.ResultId))
                .Concat(plan.Gather.Select(i => i.Id)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            await CatalogService.WarmCacheAsync(ids);
            cancellation.Token.ThrowIfCancellationRequested();
            TargetName = $"{quantity:N0} × {Name(target.Result.Id)}";
            TargetBuff = Buff(target.Result.Id);
            IngestorVisibility = _buffs.ContainsKey(target.Result.Id) || target.Kind == "Cooking"
                ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
            if (IngestorVisibility == Microsoft.UI.Xaml.Visibility.Visible && TargetBuff.Length == 0)
                TargetBuff = catalog.Food is null ? "Update game data to see Ingestor bonuses." : "No Ingestor effect listed for this item.";
            PlanSummary = $"{plan.Steps.Count} preparation steps · {plan.Gather.Count} starting ingredients";
            Status = plan.Notice;
            string? selected = SelectedItem?.Id;
            _allCards.Clear();
            var levels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [target.Result.Id] = 0 };
            foreach (var step in plan.Steps.Reverse())
                foreach (var ingredient in step.Recipe.Ingredients)
                    levels[ingredient.Id] = Math.Max(levels.GetValueOrDefault(ingredient.Id), levels.GetValueOrDefault(step.Recipe.ResultId) + 1);
            RecipePlannerItemViewModel Item(string id, long amount, int input = 0) => new()
            {
                Id = id, Name = Name(id), Required = amount,
                Amount = (input > 0 ? $"Input {input}: " : "") + $"{amount:N0} × {Name(id)}",
                Icon = CatalogService.TryGet(id)?.Icon, Buff = Buff(id),
                Source = catalog.Sources.GetValueOrDefault(id) is { Length: > 0 } source ? source
                    : "No source information is available for this ingredient.",
                SelectCommand = new RelayCommand(() => Select(id)), ExploreCommand = new RelayCommand(() => Explore(id))
            };
            foreach (var step in plan.Steps)
            {
                var recipe = catalog.Recipes.First(r => r.Id == step.Recipe.Id);
                var choices = catalog.Recipes.Where(r => step.Alternatives.Contains(r.Id))
                    .Select(r => new RecipePlanChoice(r.Id, string.Join(" + ", r.Ingredients.Select(i => $"{i.Amount} × {Name(i.Id)}"))
                        + $" → {r.Result.Amount} × {Name(r.Result.Id)}")).ToArray();
                string id = step.Recipe.ResultId;
                _allCards[id] = new()
                {
                    Id = id, RecipeId = recipe.Id, Name = Name(id), Level = levels.GetValueOrDefault(id),
                    Heading = $"STEP {plan.Steps.IndexOfStep(step) + 1}",
                    Amount = $"{checked(step.Batches * Math.Max(1, recipe.Result.Amount)):N0} × {Name(id)}",
                    Station = $"{Station(recipe)} · run {step.Batches:N0} {(step.Batches == 1 ? "batch" : "batches")}",
                    StationIcon = CatalogService.TryGet(MachineId(recipe))?.Icon,
                    Icon = CatalogService.TryGet(id)?.Icon, Buff = Buff(id),
                    Ingredients = recipe.Ingredients.Select((i, slot) => Item(i.Id, checked(step.Batches * i.Amount), slot + 1)).ToArray(),
                    Choices = choices, SelectedChoice = choices.FirstOrDefault(c => c.Id == recipe.Id),
                    UsedFor = id == target.Result.Id ? "Your requested recipe" : "Used in " + string.Join(", ",
                        plan.Steps.Where(s => s.Recipe.Ingredients.Any(i => i.Id == id)).Select(s => Name(s.Recipe.ResultId))),
                    SelectCommand = new RelayCommand(() => Select(id)), ExploreCommand = new RelayCommand(() => Explore(id))
                };
            }
            Gather.Clear();
            foreach (var ingredient in plan.Gather)
            {
                var row = Item(ingredient.Id, ingredient.Amount);
                Gather.Add(row);
                _allCards.TryAdd(row.Id, row);
            }
            RefreshOwned();
            _path.RemoveAll(id => !_allCards.ContainsKey(id));
            if (_path.Count == 0) _path.Add(target.Result.Id);
            ShowBranch();
            Select(selected is not null && _allCards.ContainsKey(selected) ? selected : _path[^1]);
            _planPending = false;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            if (!cancellation.IsCancellationRequested) Status = ex is InvalidOperationException ? ex.Message : "This route could not be calculated. Try another recipe.";
        }
        finally { if (ReferenceEquals(_planning, cancellation)) _planning = null; }
    }

    private void Select(string id)
    {
        if (!_allCards.TryGetValue(id, out var item)) return;
        if (SelectedItem is { } previous) previous.IsSelected = false;
        SelectedItem = item; item.IsSelected = true;
    }

    private void Explore(string id)
    {
        if (!_allCards.ContainsKey(id)) return;
        int index = _path.IndexOf(id);
        if (index >= 0) _path.RemoveRange(index + 1, _path.Count - index - 1);
        else _path.Add(id);
        ShowBranch(); Select(id);
    }

    private void ShowBranch()
    {
        if (_path.Count == 0) return;
        string start = _path[^1];
        var visible = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Walk(string id)
        {
            if (!visible.Add(id) || !_allCards.TryGetValue(id, out var item)) return;
            foreach (var child in item.Ingredients) Walk(child.Id);
        }
        Walk(start);
        Cards.Clear(); Levels.Clear();
        foreach (var card in _allCards.Values.Where(c => visible.Contains(c.Id) && c.RecipeId.Length > 0)) Cards.Add(card);
        foreach (var group in Cards.GroupBy(c => c.Level).OrderBy(g => g.Key))
            Levels.Add(new(new ObservableCollection<RecipePlannerItemViewModel>(group)));
        Breadcrumbs.Clear();
        foreach (string id in _path) Breadcrumbs.Add(new(Name(id), new RelayCommand(() => Explore(id))));
        CanGoBack = _path.Count > 1;
        MapHeading = _path.Count == 1 ? "All requirements" : Name(start) + " requirements";
    }

    public void ChooseRecipe(string itemId, string recipeId)
    {
        if (_choices.GetValueOrDefault(itemId) == recipeId) return;
        if (_allCards.GetValueOrDefault(itemId)?.RecipeId == recipeId) return;
        if (_target?.Result.Id == itemId && _catalog?.Recipes.FirstOrDefault(r => r.Id == recipeId) is { } target) _target = target;
        else _choices[itemId] = recipeId;
        _ = RebuildAsync();
    }

    [RelayCommand] private void Back() { if (_path.Count > 1) { _path.RemoveAt(_path.Count - 1); ShowBranch(); Select(_path[^1]); } }
    [RelayCommand] private void MainRecipe() { if (_target is not null) Explore(_target.Result.Id); }
    [RelayCommand] private void ShowMap() { MapVisibility = Microsoft.UI.Xaml.Visibility.Visible; OrderVisibility = Microsoft.UI.Xaml.Visibility.Collapsed; }
    [RelayCommand] private void ShowOrder() { MapVisibility = Microsoft.UI.Xaml.Visibility.Collapsed; OrderVisibility = Microsoft.UI.Xaml.Visibility.Visible; }
}

internal static class RecipePlanStepIndex
{
    public static int IndexOfStep(this IReadOnlyList<RecipePlanStep> steps, RecipePlanStep item)
    { for (int i = 0; i < steps.Count; i++) if (ReferenceEquals(steps[i], item)) return i; return -1; }
}
