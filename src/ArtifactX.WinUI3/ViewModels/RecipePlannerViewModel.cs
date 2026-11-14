using ArtifactX.Core.Recipes;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using S = ArtifactX.Core.NmsModels.NmsSpaceStationPaths;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// The Cooking and Refiner Recipes page (redesigned 2026-11-13 with the owner): the open recipe's ingredient tree on the
/// left at full height, and on the right the recipe card (search, pin, Ingestor effect, whether you have every raw
/// ingredient) over two tabs, Selected step and Shopping list. One of the recipe, always: players want the recipe and
/// whether they have what it needs, not batch sizes. Live saves only change the have/need numbers in place.
/// </summary>
public sealed partial class RecipePlannerViewModel : ObservableObject
{
    private readonly IRecipePlannerCatalog _catalogService;
    private readonly RecipePlannerMode _mode;
    private RecipePlannerCatalog? _catalog;
    private readonly Dictionary<string, string> _choices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RecipePlannerItemViewModel> _allCards = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (FoodIngestorDefinition Food, float Bonus)> _buffs = new(StringComparer.OrdinalIgnoreCase);
    private RecipeInfo? _target;
    private CancellationTokenSource? _planning;
    private int _loadVersion;
    private bool _loadingCatalog;
    private HashSet<string> _pinned = new(StringComparer.OrdinalIgnoreCase);
    private long _catalogRevision = -1;
    private IReadOnlyList<CargoSupply>? _owned;
    private int _inventoryVersion;
    private object? _inventoryOwner;
    private bool _planPending;
    private ulong? _here;
    public bool IsReady => _catalog is not null && _catalogRevision == CatalogService.Revision;

    public RecipePlannerViewModel(RecipePlannerMode mode) : this(new RecipePlannerCatalogService(), mode) { }
    internal RecipePlannerViewModel(IRecipePlannerCatalog catalogService, RecipePlannerMode mode)
    { _catalogService = catalogService; _mode = mode; }
    private bool IsCooking => _mode == RecipePlannerMode.Cooking;
    private string RootKind => IsCooking ? "Cooking" : "Refiner";
    public string PageTitle => IsCooking ? "COOKING RECIPES" : "REFINER RECIPES";
    public string SearchPlaceholder => IsCooking ? "Search foods and ingredients" : "Search materials";
    public string EmptyHint => IsCooking ? "Choose a recipe on the right to see everything it needs."
        : "Choose a material on the right to see how to refine it.";
    public Visibility EffectVisibility => IsCooking ? Visibility.Visible : Visibility.Collapsed;

    public ObservableCollection<RecipePlannerItemViewModel> Results { get; } = [];
    /// <summary>The Shopping list: every raw ingredient of the recipe with its total.</summary>
    public ObservableCollection<RecipePlannerItemViewModel> Gather { get; } = [];
    /// <summary>The tree's root (none until a recipe is open).</summary>
    public ObservableCollection<RecipeTreeNode> Tree { get; } = [];
    public ObservableCollection<RecipePlannerEffect> Effects { get; } = [];
    public IngredientSourceViewModel Source { get; } = new();

    [ObservableProperty] public partial string Search { get; set; } = "";
    [ObservableProperty] public partial RecipePlannerEffect? SelectedEffect { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StatusVisibility))] public partial string Status { get; set; } = "Loading recipes…";
    public Visibility StatusVisibility => Status.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    [ObservableProperty] public partial string TargetName { get; set; } = "";
    [ObservableProperty] public partial ImageSource? TargetIcon { get; set; }
    [ObservableProperty] public partial string TargetBuff { get; set; } = "";
    [ObservableProperty] public partial Visibility IngestorVisibility { get; set; } = Visibility.Collapsed;
    [ObservableProperty] public partial bool IsTargetPinned { get; set; }
    [ObservableProperty] public partial RecipePlannerItemViewModel? SelectedItem { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PlannerVisibility), nameof(EmptyVisibility))] public partial bool HasRecipe { get; set; }
    public Visibility PlannerVisibility => HasRecipe ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyVisibility => HasRecipe ? Visibility.Collapsed : Visibility.Visible;
    [ObservableProperty] public partial string PlanSummary { get; set; } = "";
    [ObservableProperty] public partial string ResultCount { get; set; } = "";
    [ObservableProperty] public partial bool PinnedOnly { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PickerVisibility), nameof(PickerButtonText), nameof(PickerButtonVisibility))]
    public partial bool IsPickerOpen { get; set; } = true;
    public Visibility PickerVisibility => IsPickerOpen ? Visibility.Visible : Visibility.Collapsed;
    public string PickerButtonText => IsPickerOpen ? "Close search" : "Change recipe";
    public Visibility PickerButtonVisibility => HasRecipe ? Visibility.Visible : Visibility.Collapsed;
    partial void OnHasRecipeChanged(bool value) => OnPropertyChanged(nameof(PickerButtonVisibility));
    [ObservableProperty] public partial string HaveSummary { get; set; } = "";
    [ObservableProperty] public partial Brush? HaveSummaryBrush { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(SelectedTabVisibility), nameof(ShoppingTabVisibility))]
    public partial bool ShowShoppingList { get; set; }
    public Visibility SelectedTabVisibility => ShowShoppingList ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ShoppingTabVisibility => ShowShoppingList ? Visibility.Visible : Visibility.Collapsed;

    partial void OnSearchChanged(string value) => Filter();
    partial void OnPinnedOnlyChanged(bool value) => Filter();
    partial void OnSelectedEffectChanged(RecipePlannerEffect? value) => Filter();
    partial void OnSelectedItemChanged(RecipePlannerItemViewModel? value)
    {
        // Every item, not just raw ones: a recipe also makes Meaty Chunks or Mordite, but players mostly gather them.
        if (value is { IsSupply: false }) _ = Source.ShowAsync(value.Id, _here);
    }

    [RelayCommand] private void TogglePicker() => IsPickerOpen = !IsPickerOpen || !HasRecipe;
    [RelayCommand] private async Task TogglePinTargetAsync() { if (_target is not null) await TogglePinAsync(_target.Id); }

    public async Task LoadAsync()
    {
        int version = ++_loadVersion;
        _planning?.Cancel();
        _loadingCatalog = true;
        try
        {
            var catalog = await _catalogService.LoadAsync(CancellationToken.None);
            if (version != _loadVersion) return;
            _catalog = catalog;
            _pinned = (await Task.Run(() => PinnedRecipesService.All)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            _buffs.Clear();
            foreach (var food in IsCooking ? catalog.Food?.Foods ?? [] : [])
                if (food.Id.Length <= 16) _buffs[food.Id] = (food, FoodIngestorBonus.Calculate(food));
            string? selection = SelectedEffect?.Id;
            Effects.Clear();
            Effects.Add(new("", "All Ingestor effects"));
            foreach (var effect in _buffs.Values.Select(b => b.Food.Effect).Distinct().OrderBy(EffectName))
                Effects.Add(new(effect, EffectName(effect)));
            SelectedEffect = Effects.FirstOrDefault(e => e.Id == selection) ?? Effects[0];
            await CatalogService.WarmCacheAsync(catalog.Recipes.Where(r => r.Kind == RootKind).Select(r => r.Result.Id)
                .Concat(new[] { "COOKER", "BUILD_REFINER1", "BUILD_REFINER2", "BUILD_REFINER3" }));
            if (version != _loadVersion) return;
            _loadingCatalog = false;
            Filter();
            Status = !catalog.Recipes.Any(r => r.Kind == RootKind) ? "No recipes available. Update game data to load them." : "";
            if (_target is not null)
            {
                _target = catalog.Recipes.FirstOrDefault(r => r.Id == _target.Id);
                await RebuildAsync();
            }
            if (version == _loadVersion) _catalogRevision = CatalogService.Revision;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); Status = "Recipes could not be loaded. Try updating game data."; }
        finally { if (version == _loadVersion) _loadingCatalog = false; }
    }

    private string Name(string id) => _catalog?.Names.GetValueOrDefault(id) ?? "Unknown ingredient";
    private string Buff(string id)
    {
        if (!IsCooking) return "";
        if (!_buffs.TryGetValue(id, out var value)) return "";
        double seconds = Math.Max(0, (_catalog?.Food?.BaseDuration ?? 0) * value.Food.CookingValue);
        // The game's own amount (FoodIngestorDisplay); stats whose amount depends on the
        // player's equipment show the effect alone rather than a made-up number.
        string effect = FoodIngestorDisplay.Text(value.Food.Stat, value.Bonus) is { } amount
            ? $"{amount} {EffectName(value.Food.Effect)}" : $"Boosts {EffectName(value.Food.Effect).ToLowerInvariant()}";
        return $"{effect} · {TimeSpan.FromSeconds(seconds):m\\:ss} per item";
    }

    private void Filter()
    {
        if (_catalog is null || _loadingCatalog) return;
        string effect = SelectedEffect?.Id ?? "";
        var shown = _catalog.Recipes.Where(r => r.Kind == RootKind).GroupBy(r => r.Result.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.FirstOrDefault(r => _pinned.Contains(r.Id)) ?? g.First())
            .Where(r => !PinnedOnly || _pinned.Contains(r.Id))
            .Where(r => Name(r.Result.Id).Contains(Search.Trim(), StringComparison.CurrentCultureIgnoreCase))
            .Where(r => effect.Length == 0 || _buffs.TryGetValue(r.Result.Id, out var food) && food.Food.Effect == effect)
            .OrderByDescending(r => _pinned.Contains(r.Id))
            .ThenByDescending(r => effect.Length > 0 && _buffs.TryGetValue(r.Result.Id, out var food)
                ? food.Bonus * (food.Food.LowerIsBetter ? -1 : 1) : 0)
            .ThenByDescending(r => effect.Length > 0 && _buffs.TryGetValue(r.Result.Id, out var food) ? food.Food.CookingValue : 0)
            .ThenBy(r => Name(r.Result.Id), StringComparer.CurrentCultureIgnoreCase).ToArray();
        Results.Clear();
        foreach (var recipe in shown)
            Results.Add(new() { Id = recipe.Result.Id, RecipeId = recipe.Id, Name = Name(recipe.Result.Id), IsPinned = _pinned.Contains(recipe.Id),
                Icon = CatalogService.TryGet(recipe.Result.Id)?.Icon, Buff = Buff(recipe.Result.Id), Station = Station(recipe),
                SelectCommand = new RelayCommand(() => Open(recipe)),
                PinCommand = new AsyncRelayCommand(() => TogglePinAsync(recipe.Id)) });
        ResultCount = $"{shown.Length:N0} recipes" + (effect.Length > 0 ? " · strongest first" : "");
    }

    private void Open(RecipeInfo recipe)
    {
        _target = recipe; _choices.Clear();
        SelectedItem = null;
        IsPickerOpen = false;
        ShowShoppingList = false;
        IsTargetPinned = _pinned.Contains(recipe.Id);
        _ = RebuildAsync();
    }

    private async Task TogglePinAsync(string recipeId)
    {
        bool pinned = await Task.Run(() => PinnedRecipesService.Toggle(recipeId));
        if (pinned) _pinned.Add(recipeId); else _pinned.Remove(recipeId);
        foreach (var row in Results.Where(r => r.RecipeId == recipeId)) row.IsPinned = pinned;
        if (_target?.Id == recipeId) IsTargetPinned = pinned;
        if (PinnedOnly && !pinned) Filter();
    }

    public void Stop() { ++_loadVersion; _planning?.Cancel(); Source.Stop(); }
    public Task ResumeAsync() => _planPending ? RebuildAsync() : Task.CompletedTask;

    /// <summary>What the save holds of every item on the page, and where the save is (the start of the "closest to
    /// you" search). Changes numbers in place; the tree and selection stay.</summary>
    public async Task RefreshOwnedAsync(PlanetSnapshot? snapshot = null)
    {
        int version = ++_inventoryVersion;
        object? owner = SaveSessionManager.SessionIdentity;
        if (!ReferenceEquals(_inventoryOwner, owner)) { _owned = null; _inventoryOwner = owner; }
        try
        {
            var player = snapshot?.CargoSupplies is null && _owned is null
                ? SaveSessionManager.GetValue("vLc", "6f=") as JObject : null;
            var supplies = snapshot?.CargoSupplies ?? _owned
                ?? await Task.Run(() => CargoSupplies.Read(player));
            IReadOnlyList<BaseExtractionSite> baseSources = [];
            if (SaveSessionManager.IsSaveLoaded && _catalog?.Extraction is { } extraction)
            {
                var bases = snapshot?.Bases ?? SaveSessionManager.GetValue("vLc", "6f=", "F?0") as JArray;
                try { baseSources = await Task.Run(() => BaseExtractionSites.Read(bases, extraction)); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Recipe extraction sites] {ex}"); }
            }
            if (version != _inventoryVersion || !ReferenceEquals(owner, SaveSessionManager.SessionIdentity)) return;
            _owned = supplies;
            ulong? here = SaveSessionManager.IsSaveLoaded ? S.ReadSystemAddress((snapshot ?? LiveSaveWatcher.FromSession()).Universe) : null;
            if (here != _here)
            {
                _here = here;
                if (SelectedItem is { IsSupply: false } item) _ = Source.ShowAsync(item.Id, _here);
            }
            bool loaded = SaveSessionManager.IsSaveLoaded;
            foreach (var item in _allCards.Values.Concat(Gather).Distinct())
            {
                var locations = supplies.Where(s => s.ItemId.Equals(item.Id, StringComparison.OrdinalIgnoreCase)).ToArray();
                item.Owned = loaded ? locations.Sum(s => s.Amount) : null;
                item.InventoryLocations = loaded && locations.Length > 0
                    ? string.Join("\n", locations.Select(s => $"{s.Location}: {s.Amount:N0}")) : "";
                item.BaseSources = string.Join("\n", baseSources.Where(s => s.ItemId.Equals(item.Id, StringComparison.OrdinalIgnoreCase))
                    .Select(s => $"{s.BaseName}: {s.Extractors:N0} {(s.Kind == "Gas" ? "gas" : "mineral")} extractor{(s.Extractors == 1 ? "" : "s")}"));
            }
            UpdateHaveSummary();
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Recipe inventory] {ex}"); }
    }

    private void UpdateHaveSummary()
    {
        if (!SaveSessionManager.IsSaveLoaded || Gather.Count == 0)
        {
            HaveSummary = Gather.Count == 0 ? "" : "Load a save to see whether you have the ingredients.";
            HaveSummaryBrush = ThemeResources.Brush("ThemeTextDimBrush");
            return;
        }
        int missing = Gather.Count(g => !g.HaveEnough);
        HaveSummary = missing == 0 ? "✓ You have every raw ingredient for this recipe."
            : missing == 1 ? "1 raw ingredient short: see the Shopping list." : $"{missing} raw ingredients short: see the Shopping list.";
        HaveSummaryBrush = ThemeResources.Brush(missing == 0 ? "ThemeDoneBrush" : "ThemeWarningBrush");
    }

    private string Station(RecipeInfo r) => _catalog?.Names.GetValueOrDefault(MachineId(r)) ?? "Required equipment";
    private static string MachineId(RecipeInfo r) => r.Kind == "Cooking" ? "COOKER" : "BUILD_REFINER" + Math.Clamp(r.Ingredients.Count, 1, 3);

    private static string EffectName(string effect) => effect switch
    {
        "Weapon_Scan_Discovery_Creature" => "Fauna analysis rewards", "Weapon_Scan_Discovery_Flora" => "Flora analysis rewards",
        "Weapon_Scan_Discovery_Mineral" => "Mineral analysis rewards", "Weapon_Laser_MiningBonus" => "Resources mined",
        "Weapon_Laser_Mining_Speed" => "Mining speed", "Suit_Protection" => "Hazard protection battery",
        "Suit_Armour_Shield_Strength" => "Shield strength", "Suit_Energy" => "Life support tanks",
        "Suit_Energy_Regen" => "Solar panel power",
        "Suit_Stamina_Strength" => "Sprint distance", "Suit_Stamina_Recovery" => "Sprint recovery time",
        "Suit_Jetpack_Tank" => "Jetpack tanks", "Suit_AutoTranslator" => "Words translated",
        "Suit_Protection_WaterDrain" => "Breathing efficiency", "Suit_Protection_Cold" => "Cold resistance",
        "Suit_Protection_Heat" => "Heat resistance", "Suit_Protection_Toxic" => "Toxic resistance",
        "Suit_Protection_Radiation" => "Radiation resistance", "Weapon_Scan_Recharge_Time" => "Recharge time",
        "Suit_Protection_HeatDrain" => "Heat resistance", "Suit_Protection_ColdDrain" => "Cold resistance",
        "Suit_Protection_ToxDrain" => "Toxic resistance", "Suit_Protection_RadDrain" => "Radiation resistance",
        _ => "Other Ingestor effect"
    };
}
