using ArtifactX.Core.Recipes;
using ArtifactX.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArtifactX.Maui.ViewModels;

/// <summary>A choice in the cooking list's Ingestor effect filter ("" = all).</summary>
public sealed record RecipeEffectOption(string Id, string Label)
{
    public override string ToString() => Label;
}

/// <summary>A recipe in the list: what it makes, from what, and on which machine.</summary>
public sealed record RecipeRowViewModel(string RecipeId, string Name, string Formula, string Station, string Buff, string WaysText)
{
    public bool HasBuff => Buff.Length > 0;
    public string StationLine => WaysText.Length > 0 ? $"{Station} · {WaysText}" : Station;
    public string AccessibleName => $"{Name}. {Formula}. {Station}. {Buff} {WaysText}. Open the recipe.";
}

/// <summary>
/// The phone's Recipes tab: cooking or refiner recipes from the rules file, one row per thing made (as the desktop's
/// recipe pages). The search matches what a recipe makes first, then recipes that use a matching ingredient ("what
/// can I make from Ferrite Dust"); cooking can be narrowed to one Nutrient Ingestor effect, strongest first. Tap a row
/// for everything it needs (RecipeDetailsViewModel).
/// </summary>
public sealed partial class RecipesViewModel : ObservableObject
{
    private RecipeBook? _book;
    private bool _loaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRefiner), nameof(SearchPlaceholder))]
    public partial bool IsCooking { get; set; } = true;

    public bool IsRefiner => !IsCooking;
    public string SearchPlaceholder => IsCooking ? "Search foods and ingredients" : "Search materials";

    [ObservableProperty] public partial string Search { get; set; } = "";
    [ObservableProperty] public partial IReadOnlyList<RecipeEffectOption> Effects { get; private set; } = [];
    [ObservableProperty] public partial RecipeEffectOption? SelectedEffect { get; set; }
    [ObservableProperty] public partial IReadOnlyList<RecipeRowViewModel> Results { get; private set; } = [];
    [ObservableProperty] public partial string Status { get; private set; } = "Loading recipes...";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotReady))]
    public partial bool IsReady { get; private set; }

    public bool IsNotReady => !IsReady;

    public async Task LoadAsync()
    {
        if (_loaded) return;
        _loaded = true;
        _book = await RecipeBook.GetAsync();
        if (_book is null)
        {
            Status = "Recipes need the game rules, which this copy of the app doesn't have.";
            return;
        }
        var effects = _book.Recipes.Where(r => r.Kind == "Cooking").Select(r => _book.Food(r.ResultId)?.Food.Effect)
            .OfType<string>().Distinct().Select(e => new RecipeEffectOption(e, FoodIngestorDisplay.EffectName(e)))
            .OrderBy(e => e.Label, StringComparer.CurrentCultureIgnoreCase).ToList();
        Effects = (RecipeEffectOption[])[new RecipeEffectOption("", "All Ingestor effects"), .. effects];
        SelectedEffect = Effects[0];
        IsReady = true;
        Filter();
    }

    [RelayCommand] private void ShowCooking() => IsCooking = true;

    [RelayCommand] private void ShowRefiner() => IsCooking = false;

    [RelayCommand]
    private static async Task OpenAsync(RecipeRowViewModel? row)
    {
        if (row is null || Shell.Current is null) return;
        await Shell.Current.GoToAsync($"recipe-details?id={Uri.EscapeDataString(row.RecipeId)}");
    }

    partial void OnIsCookingChanged(bool value) => Filter();
    partial void OnSearchChanged(string value) => Filter();
    partial void OnSelectedEffectChanged(RecipeEffectOption? value) => Filter();

    private void Filter()
    {
        if (_book is not { } book) return;
        string kind = IsCooking ? "Cooking" : "Refiner";
        string text = Search.Trim();
        string effect = IsCooking ? SelectedEffect?.Id ?? "" : "";
        var ofKind = book.Recipes.Where(r => r.Kind == kind)
            .Where(r => effect.Length == 0 || book.Food(r.ResultId)?.Food.Effect == effect).ToArray();
        var ways = ofKind.GroupBy(r => r.ResultId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        bool Has(string id) => book.Name(id).Contains(text, StringComparison.CurrentCultureIgnoreCase);

        // One row per thing made: the first recipe for it, or with a search for an ingredient, the first that uses it.
        var makes = ofKind.Where(r => text.Length == 0 || Has(r.ResultId))
            .GroupBy(r => r.ResultId, StringComparer.OrdinalIgnoreCase).Select(g => g.First());
        var uses = text.Length == 0 ? Array.Empty<RequirementRecipe>() : ofKind.Where(r => !Has(r.ResultId) && r.Ingredients.Any(i => Has(i.Id)))
            .GroupBy(r => r.ResultId, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToArray();

        IEnumerable<RequirementRecipe> Order(IEnumerable<RequirementRecipe> recipes) => effect.Length == 0
            ? recipes.OrderBy(r => book.Name(r.ResultId), StringComparer.CurrentCultureIgnoreCase)
            // The strongest effect first, then the longest-lasting (as the desktop).
            : recipes.OrderByDescending(r => book.Food(r.ResultId) is { } f ? f.Bonus * (f.Food.LowerIsBetter ? -1 : 1) : 0)
                .ThenByDescending(r => book.Food(r.ResultId)?.Food.CookingValue ?? 0)
                .ThenBy(r => book.Name(r.ResultId), StringComparer.CurrentCultureIgnoreCase);

        RecipeRowViewModel Row(RequirementRecipe r)
        {
            int count = ways.GetValueOrDefault(r.ResultId);
            return new(r.Id, book.Name(r.ResultId), book.Formula(r), book.Station(r), book.Buff(r.ResultId),
                count > 1 ? $"{count} ways to make it" : "");
        }
        var shownMakes = Order(makes).Select(Row).ToArray();
        var shownUses = Order(uses).Select(Row).ToArray();
        Results = (RecipeRowViewModel[])[.. shownMakes, .. shownUses];
        Status = Results.Count == 0 ? "No recipes match. Try another word."
            : text.Length > 0 && shownUses.Length > 0
                ? $"{shownMakes.Length:N0} make \"{text}\", then {shownUses.Length:N0} that use it."
                : $"{Results.Count:N0} recipes{(effect.Length > 0 ? ", strongest first" : "")}. Tap one to see what it needs.";
    }
}
