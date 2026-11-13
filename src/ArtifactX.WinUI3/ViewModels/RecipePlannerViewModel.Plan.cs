using ArtifactX.Core.Recipes;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels;

public sealed partial class RecipePlannerViewModel
{
    /// <summary>Works out the open recipe (one of it) and rebuilds the tree, keeping the selected step.</summary>
    private async Task RebuildAsync()
    {
        if (_target is not { } target || _catalog is not { } catalog) return;
        _planPending = true;
        _planning?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _planning = cancellation;
        try
        {
            var routeRecipes = IsCooking ? catalog.Recipes : catalog.Recipes.Where(r => r.Kind == "Refiner").ToArray();
            var plan = await RecipeRequirementsService.BuildAsync(target, routeRecipes, 1,
                new Dictionary<string, string>(_choices, StringComparer.OrdinalIgnoreCase), cancellation.Token,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            var ids = plan.Steps.SelectMany(s => s.Recipe.Ingredients.Select(i => i.Id).Append(s.Recipe.ResultId))
                .Concat(plan.Gather.Select(i => i.Id)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            await CatalogService.WarmCacheAsync(ids);
            cancellation.Token.ThrowIfCancellationRequested();

            TargetName = Name(target.Result.Id);
            TargetIcon = CatalogService.TryGet(target.Result.Id)?.Icon;
            TargetBuff = Buff(target.Result.Id);
            IngestorVisibility = IsCooking && TargetBuff.Length > 0 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
            string steps = IsCooking ? "cooking" : "refining";
            PlanSummary = $"{plan.Steps.Count} {steps} {(plan.Steps.Count == 1 ? "step" : "steps")} · {plan.Gather.Count} raw {(plan.Gather.Count == 1 ? "ingredient" : "ingredients")}";
            Status = plan.Notice;

            string? selected = SelectedItem?.TreeKey;
            var circular = plan.CircularSupplies.ToDictionary(s => (s.ParentId, s.Id));
            string SupplyKey(string parent, string id) => "supply:" + parent + ":" + id;
            RecipePlannerAlternative[] Alternatives(string id, string currentRecipe) => routeRecipes
                .Where(r => r.Result.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                .Select(r => new RecipePlannerAlternative(
                    string.Join(" + ", r.Ingredients.Select(i => $"{i.Amount:N0} × {Name(i.Id)}")) + $" → {r.Result.Amount:N0} × {Name(id)}",
                    Station(r), $"Use recipe for {Name(id)}: " + string.Join(" and ", r.Ingredients.Select(i => Name(i.Id))),
                    new RelayCommand(() => ChooseRecipe(id, r.Id)), r.Id == currentRecipe)).ToArray();
            string Description(string id) => catalog.Sources.GetValueOrDefault(id) ?? "";

            _allCards.Clear();
            foreach (var step in plan.Steps)
            {
                var recipe = catalog.Recipes.First(r => r.Id == step.Recipe.Id);
                string id = step.Recipe.ResultId;
                _allCards[id] = new()
                {
                    Id = id, RecipeId = recipe.Id, Name = Name(id),
                    Required = checked(step.Batches * Math.Max(1, recipe.Result.Amount)),
                    Verb = recipe.Kind == "Cooking" ? "Cook" : "Refine",
                    Station = $"{Station(recipe)} · {step.Batches:N0} {(step.Batches == 1 ? "batch" : "batches")}",
                    StationIcon = CatalogService.TryGet(MachineId(recipe))?.Icon,
                    Icon = CatalogService.TryGet(id)?.Icon, Buff = Buff(id), Source = Description(id),
                    Ingredients = recipe.Ingredients.Select(i => new RecipePlannerItemViewModel
                    {
                        Id = i.Id, Name = Name(i.Id), Icon = CatalogService.TryGet(i.Id)?.Icon,
                        TreeNodeId = circular.ContainsKey((id, i.Id)) ? SupplyKey(id, i.Id) : "",
                        Amount = $"{checked(step.Batches * i.Amount):N0} × {Name(i.Id)}",
                        SelectCommand = new RelayCommand(() => Select(circular.ContainsKey((id, i.Id)) ? SupplyKey(id, i.Id) : i.Id)),
                    }).ToArray(),
                    Alternatives = Alternatives(id, recipe.Id),
                    UsedFor = id == target.Result.Id ? "" : "Goes into " + string.Join(", ",
                        plan.Steps.Where(s => s.Recipe.Ingredients.Any(i => i.Id == id)).Select(s => Name(s.Recipe.ResultId)).Distinct()),
                    SelectCommand = new RelayCommand(() => Select(id)),
                };
            }
            Gather.Clear();
            foreach (var ingredient in plan.Gather)
            {
                string id = ingredient.Id;
                var row = new RecipePlannerItemViewModel
                {
                    Id = id, Name = Name(id), Required = ingredient.Amount, Icon = CatalogService.TryGet(id)?.Icon,
                    Buff = Buff(id), Source = Description(id), Alternatives = Alternatives(id, ""),
                    SelectCommand = new RelayCommand(() => Select(id)),
                };
                Gather.Add(row);
                _allCards.TryAdd(id, row);
            }
            foreach (var supply in plan.CircularSupplies)
            {
                string key = SupplyKey(supply.ParentId, supply.Id);
                _allCards[key] = new()
                {
                    Id = supply.Id, TreeNodeId = key, Name = Name(supply.Id), IsSupply = true, Required = supply.Amount,
                    Icon = CatalogService.TryGet(supply.Id)?.Icon,
                    Source = "This step repeats an earlier ingredient, so start with some you gathered or stored.\n\n" + Description(supply.Id),
                    SelectCommand = new RelayCommand(() => Select(key)),
                };
            }

            Tree.Clear();
            if (_allCards.ContainsKey(target.Result.Id)) Tree.Add(Node(target.Result.Id, [], true));
            HasRecipe = true;
            await RefreshOwnedAsync();
            cancellation.Token.ThrowIfCancellationRequested();
            Select(selected is not null && _allCards.ContainsKey(selected) ? selected : target.Result.Id);
            _planPending = false;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            if (!cancellation.IsCancellationRequested) Status = ex is InvalidOperationException ? ex.Message : "This recipe could not be worked out. Try another one.";
        }
        finally { if (ReferenceEquals(_planning, cancellation)) _planning = null; }
    }

    /// <summary>A tree node and, below it, what it's made from; a step already on the path above ends the branch
    /// (circular conversions start from a gathered supply instead).</summary>
    private RecipeTreeNode Node(string key, HashSet<string> above, bool last)
    {
        var item = _allCards[key];
        var path = new HashSet<string>(above, StringComparer.OrdinalIgnoreCase) { key };
        var keys = item.Ingredients.Select(i => i.TreeKey).Where(k => _allCards.ContainsKey(k) && !path.Contains(k)).ToList();
        return new RecipeTreeNode
        {
            Item = item, IsLast = last,
            Children = keys.Select((k, i) => Node(k, path, i == keys.Count - 1)).ToList(),
        };
    }

    private void Select(string key)
    {
        if (!_allCards.TryGetValue(key, out var item)) return;
        if (SelectedItem is { } previous) previous.IsSelected = false;
        item.IsSelected = true;
        SelectedItem = item;
        ShowShoppingList = false;
    }

    public void ChooseRecipe(string itemId, string recipeId)
    {
        if (_allCards.GetValueOrDefault(itemId)?.RecipeId == recipeId) return;
        if (_target?.Result.Id == itemId && _catalog?.Recipes.FirstOrDefault(r => r.Id == recipeId) is { } target) _target = target;
        else _choices[itemId] = recipeId;
        _ = RebuildAsync();
    }
}
