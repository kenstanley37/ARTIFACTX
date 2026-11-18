using ArtifactX.Core.Recipes;
using ArtifactX.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArtifactX.Maui.ViewModels;

/// <summary>Another recipe for the same thing, in a step's "Make it with" choice.</summary>
public sealed record RecipeAlternative(string RecipeId, string Label)
{
    public override string ToString() => Label;
}

/// <summary>A raw ingredient to gather, with the total the whole recipe needs.</summary>
public sealed record RecipeGatherItem(string Name, string AmountText)
{
    public string AccessibleName => $"{AmountText} {Name}";
}

/// <summary>One step of the route: the machine, what goes in and what comes out, and how many times to run it.</summary>
public sealed partial class RecipeStepViewModel : ObservableObject
{
    private readonly Action<string, string> _choose;

    public RecipeStepViewModel(int number, string resultId, string title, string station, string formula, string runs,
        IReadOnlyList<RecipeAlternative> alternatives, string chosenId, Action<string, string> choose)
    {
        _choose = choose;
        ResultId = resultId;
        Title = $"Step {number} · {title}";
        Station = station;
        Formula = formula;
        Runs = runs;
        Alternatives = alternatives;
        SelectedAlternative = alternatives.FirstOrDefault(a => a.RecipeId == chosenId);
    }

    public string ResultId { get; }
    public string Title { get; }
    public string Station { get; }
    public string Formula { get; }
    public string Runs { get; }
    public bool HasRuns => Runs.Length > 0;
    public IReadOnlyList<RecipeAlternative> Alternatives { get; }
    public bool HasAlternatives => Alternatives.Count > 1;
    public string AlternativesTitle => $"{Alternatives.Count} ways to make it";

    [ObservableProperty] public partial RecipeAlternative? SelectedAlternative { get; set; }

    partial void OnSelectedAlternativeChanged(RecipeAlternative? oldValue, RecipeAlternative? newValue)
    {
        // Not on the first set (the constructor), only when the player picks another recipe.
        if (oldValue is not null && newValue is not null && oldValue != newValue) _choose(ResultId, newValue.RecipeId);
    }
}

/// <summary>
/// Everything one of a recipe needs (as the desktop's recipe pages: always one, since players want the recipe, not
/// batch sizes): the raw ingredients to gather, then each step in the order to do it (Core RecipePlanner). A step that
/// can be made more than one way has a "Make it with" choice, and the route is worked out again with it.
/// </summary>
public sealed partial class RecipeDetailsViewModel : ObservableObject
{
    private readonly Dictionary<string, string> _choices = new(StringComparer.OrdinalIgnoreCase);
    private RecipeBook? _book;
    private RequirementRecipe? _target;
    private int _version;

    [ObservableProperty] public partial string Title { get; private set; } = "";
    [ObservableProperty] public partial string Station { get; private set; } = "";
    [ObservableProperty] public partial string Buff { get; private set; } = "";
    [ObservableProperty] public partial bool HasBuff { get; private set; }
    [ObservableProperty] public partial string Summary { get; private set; } = "";
    [ObservableProperty] public partial string Notice { get; private set; } = "";
    [ObservableProperty] public partial bool HasNotice { get; private set; }
    [ObservableProperty] public partial IReadOnlyList<RecipeGatherItem> Gather { get; private set; } = [];
    [ObservableProperty] public partial IReadOnlyList<RecipeStepViewModel> Steps { get; private set; } = [];

    public async Task LoadAsync(string? recipeId)
    {
        _book = await RecipeBook.GetAsync();
        if (_book is null || recipeId is null || !_book.ById.TryGetValue(recipeId, out var recipe))
        {
            Title = "Recipe not found";
            Notice = "Go back and choose the recipe again.";
            HasNotice = true;
            return;
        }
        if (_target?.Id != recipe.Id) _choices.Clear();
        _target = recipe;
        Title = _book.Name(recipe.ResultId);
        Station = _book.Station(recipe);
        Buff = _book.Buff(recipe.ResultId);
        HasBuff = Buff.Length > 0;
        await RebuildAsync();
    }

    [RelayCommand]
    private static async Task GoBackAsync()
    {
        if (Shell.Current is not null) await Shell.Current.GoToAsync("..");
    }

    private void Choose(string resultId, string recipeId)
    {
        _choices[resultId] = recipeId;
        _ = RebuildAsync();
    }

    private async Task RebuildAsync()
    {
        if (_book is not { } book || _target is not { } target) return;
        int version = ++_version;
        bool cooking = target.Kind == "Cooking";
        // Cooking routes may refine an ingredient on the way; refiner routes stay on the refiner (as the desktop).
        var route = cooking ? book.Recipes : book.Recipes.Where(r => r.Kind == "Refiner").ToArray();
        var choices = new Dictionary<string, string>(_choices, StringComparer.OrdinalIgnoreCase);
        RecipePlan plan;
        try
        {
            plan = await Task.Run(() => RecipePlanner.Build(target, route, 1, choices, CancellationToken.None,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
        }
        catch (InvalidOperationException ex)
        {
            // A chosen recipe that loops back on itself: say so and keep the last good route.
            Notice = ex.Message;
            HasNotice = true;
            return;
        }
        if (version != _version) return;

        string steps = cooking ? "cooking" : "refining";
        Summary = $"{plan.Steps.Count} {steps} {(plan.Steps.Count == 1 ? "step" : "steps")} · "
            + $"{plan.Gather.Count} raw {(plan.Gather.Count == 1 ? "ingredient" : "ingredients")}";
        Notice = plan.Notice;
        HasNotice = Notice.Length > 0;
        Gather = plan.Gather.OrderBy(g => book.Name(g.Id), StringComparer.CurrentCultureIgnoreCase)
            .Select(g => new RecipeGatherItem(book.Name(g.Id), $"{g.Amount:N0} ×")).ToArray();
        Steps = plan.Steps.Select((step, i) =>
        {
            var r = step.Recipe;
            long makes = step.Batches * Math.Max(1, r.ResultAmount);
            string runs = step.Batches > 1 ? $"Do this {step.Batches:N0} times (makes {makes:N0})." : "";
            var alternatives = route.Where(a => a.ResultId.Equals(r.ResultId, StringComparison.OrdinalIgnoreCase))
                .Select(a => new RecipeAlternative(a.Id, $"{string.Join(" + ", a.Ingredients.Select(x => book.Name(x.Id)))} ({book.Station(a)})"))
                .ToArray();
            return new RecipeStepViewModel(i + 1, r.ResultId, book.Name(r.ResultId), book.Station(r), book.Formula(r), runs,
                alternatives, r.Id, Choose);
        }).ToArray();
    }
}
