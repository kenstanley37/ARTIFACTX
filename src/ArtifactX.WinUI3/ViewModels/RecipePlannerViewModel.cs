using ArtifactX.Core.Recipes;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>Single-page recipe browser and planner. User navigation changes the
/// branch; background inventory updates only change quantities on existing rows.</summary>
public sealed partial class RecipePlannerViewModel : ObservableObject
{
    private readonly IRecipePlannerCatalog _catalogService;
    private RecipePlannerCatalog? _catalog;
    private readonly Dictionary<string, string> _choices = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _path = [];
    private readonly Dictionary<string, RecipePlannerItemViewModel> _allCards = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (FoodIngestorDefinition Food, float Bonus)> _buffs = new(StringComparer.OrdinalIgnoreCase);
    private RecipeInfo? _target;
    private CancellationTokenSource? _planning;
    private int _loadVersion;
    private bool _loadingCatalog;
    private HashSet<string> _pinned = new(StringComparer.OrdinalIgnoreCase);
    private long _catalogRevision = -1;
    private Dictionary<string, long>? _owned;
    private object? _inventoryOwner;
    private bool _planPending;
    public bool IsReady => _catalog is not null && _catalogRevision == CatalogService.Revision;

    public RecipePlannerViewModel() : this(new RecipePlannerCatalogService()) { }
    internal RecipePlannerViewModel(IRecipePlannerCatalog catalogService) => _catalogService = catalogService;
    public ObservableCollection<RecipePlannerItemViewModel> Results { get; } = [];
    public ObservableCollection<RecipePlannerItemViewModel> Gather { get; } = [];
    public ObservableCollection<RecipePlannerItemViewModel> Cards { get; } = [];
    public ObservableCollection<RecipePlannerLevel> Levels { get; } = [];
    public ObservableCollection<RecipePlannerCrumb> Breadcrumbs { get; } = [];
    public ObservableCollection<RecipePlannerEffect> Effects { get; } = [];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(BrowserSummary))] public partial string Search { get; set; } = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(BrowserSummary))] public partial RecipePlannerEffect? SelectedEffect { get; set; }
    [ObservableProperty] public partial string Status { get; set; } = "Loading recipes…";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(RecipeSummary))] public partial string TargetName { get; set; } = "Choose a recipe to start";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(RecipeSummary))] public partial string TargetBuff { get; set; } = "";
    [ObservableProperty] public partial Visibility IngestorVisibility { get; set; } = Visibility.Collapsed;
    [ObservableProperty] public partial double Quantity { get; set; } = 1;
    [ObservableProperty] public partial RecipePlannerItemViewModel? SelectedItem { get; set; }
    [ObservableProperty] public partial Visibility PlannerVisibility { get; set; } = Visibility.Collapsed;
    [ObservableProperty] public partial Visibility MapVisibility { get; set; } = Visibility.Visible;
    [ObservableProperty] public partial Visibility OrderVisibility { get; set; } = Visibility.Collapsed;
    [ObservableProperty] public partial bool CanGoBack { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(BrowserSummary))] public partial string ResultCount { get; set; } = "";
    [ObservableProperty] public partial bool PinnedOnly { get; set; }
    [ObservableProperty] public partial bool IsBrowserExpanded { get; set; } = true;
    [ObservableProperty] public partial bool IsSummaryExpanded { get; set; } = true;
    public string BrowserSummary => string.Join(" · ", new[]
    {
        Search.Trim(), SelectedEffect is { Id.Length: > 0 } effect ? effect.Name : "", ResultCount
    }.Where(text => text.Length > 0));
    public string RecipeSummary => string.IsNullOrEmpty(TargetBuff) ? TargetName : $"{TargetName} · {TargetBuff}";
    [ObservableProperty] public partial string MapHeading { get; set; } = "All requirements";
    [ObservableProperty] public partial string PlanSummary { get; set; } = "";

    partial void OnSearchChanged(string value) { IsBrowserExpanded = true; Filter(); }
    partial void OnPinnedOnlyChanged(bool value) { IsBrowserExpanded = true; Filter(); }
    partial void OnSelectedEffectChanged(RecipePlannerEffect? value) { if (!_loadingCatalog) IsBrowserExpanded = true; Filter(); }
    partial void OnQuantityChanged(double value)
    {
        double whole = double.IsFinite(value) ? Math.Clamp(Math.Truncate(value), 1, 99999) : 1;
        if (value != whole) { Quantity = whole; return; }
        if (_target is not null) _ = RebuildAsync();
    }

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
            foreach (var food in catalog.Food?.Foods ?? [])
                if (food.Id.Length <= 16) _buffs[food.Id] = (food, FoodIngestorBonus.Calculate(food));
            string? selection = SelectedEffect?.Id;
            Effects.Clear();
            Effects.Add(new("", "All Ingestor effects"));
            foreach (var effect in _buffs.Values.Select(b => b.Food.Effect).Distinct().OrderBy(EffectName))
                Effects.Add(new(effect, EffectName(effect)));
            SelectedEffect = Effects.FirstOrDefault(e => e.Id == selection) ?? Effects[0];
            await CatalogService.WarmCacheAsync(catalog.Recipes.Select(r => r.Result.Id)
                .Concat(new[] { "COOKER", "BUILD_REFINER1", "BUILD_REFINER2", "BUILD_REFINER3" }));
            if (version != _loadVersion) return;
            _loadingCatalog = false;
            Filter();
            Status = catalog.Recipes.Count == 0 ? "No recipes available. Update game data to load them." : "Choose a recipe, or search for the Ingestor effect you want.";
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
        if (!_buffs.TryGetValue(id, out var value)) return "";
        double amount = Math.Truncate(value.Bonus * (value.Food.Percentage ? 100f : 1f));
        double seconds = Math.Max(0, (_catalog?.Food?.BaseDuration ?? 0) * value.Food.CookingValue);
        return $"{(amount >= 0 ? "+" : "")}{amount:N0}{(value.Food.Percentage ? "%" : "")} {EffectName(value.Food.Effect)} · {TimeSpan.FromSeconds(seconds):m\\:ss} per item";
    }

    private void Filter()
    {
        if (_catalog is null || _loadingCatalog) return;
        string effect = SelectedEffect?.Id ?? "";
        var shown = _catalog.Recipes.GroupBy(r => r.Result.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.FirstOrDefault(r => _pinned.Contains(r.Id)) ?? g.First())
            .Where(r => !PinnedOnly || _pinned.Contains(r.Id))
            .Where(r => Name(r.Result.Id).Contains(Search.Trim(), StringComparison.CurrentCultureIgnoreCase))
            .Where(r => effect.Length == 0 || _buffs.TryGetValue(r.Result.Id, out var food) && food.Food.Effect == effect)
            .OrderByDescending(r => effect.Length > 0 && _buffs.TryGetValue(r.Result.Id, out var food)
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
        _target = recipe; _choices.Clear(); _path.Clear(); _path.Add(recipe.Result.Id);
        SelectedItem = null;
        IsBrowserExpanded = false;
        PlannerVisibility = Visibility.Visible;
        _ = RebuildAsync();
    }

    private async Task TogglePinAsync(string recipeId)
    {
        bool pinned = await Task.Run(() => PinnedRecipesService.Toggle(recipeId));
        if (pinned) _pinned.Add(recipeId); else _pinned.Remove(recipeId);
        foreach (var row in Results.Where(r => r.RecipeId == recipeId)) row.IsPinned = pinned;
        if (PinnedOnly && !pinned) Filter();
    }

    public void Stop() { ++_loadVersion; _planning?.Cancel(); }
    public Task ResumeAsync() => _planPending ? RebuildAsync() : Task.CompletedTask;
    public void RefreshOwned(JObject? liveCargo = null)
    {
        if (!ReferenceEquals(_inventoryOwner, SaveSessionManager.SessionIdentity))
        { _owned = null; _inventoryOwner = SaveSessionManager.SessionIdentity; }
        if (liveCargo is not null || _owned is null) _owned = RecipePlannerCatalogService.ReadOwned(liveCargo);
        var owned = _owned;
        foreach (var item in Gather) item.Inventory = SaveSessionManager.IsSaveLoaded
            ? $"{item.Required:N0} needed / {owned.GetValueOrDefault(item.Id):N0} in exosuit · {Math.Max(0, item.Required - owned.GetValueOrDefault(item.Id)):N0} to gather"
            : $"{item.Required:N0} needed · load a save to compare inventory";
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
