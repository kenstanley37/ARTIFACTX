using ArtifactX.Core.Recipes;
using ArtifactX.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArtifactX.Maui.ViewModels;

/// <summary>An item's base value line.</summary>
public sealed record ItemValueRowViewModel(string Name, string Value, string Detail)
{
    public string AccessibleName => $"{Name}, {Value}. {Detail}";
}

/// <summary>
/// The phone's Item values: every product's and substance's base value from the rules file, as the desktop's Item
/// Values page shows them (Core TradeEconomies for which economy deals in a trade good), searchable, highest first, and
/// filterable to one economy's trade goods.
/// </summary>
public sealed partial class ItemValuesViewModel : ObservableObject
{
    private const int MaxRows = 200;

    private List<(string Name, long Value, bool IsSubstance, string Category, bool Illegal)> _items = [];
    private bool _loaded;

    public IReadOnlyList<FinderChoice> Economies { get; } =
        (FinderChoice[])[new("Every item", ""), new("Trade goods only", "*"), .. TradeEconomies.All.Select(e => new FinderChoice($"{e.Economy} trade goods", e.Category))];

    [ObservableProperty] public partial FinderChoice? Economy { get; set; }
    [ObservableProperty] public partial string Search { get; set; } = "";
    [ObservableProperty] public partial IReadOnlyList<ItemValueRowViewModel> Rows { get; private set; } = [];
    [ObservableProperty] public partial string Status { get; private set; } = "Loading values...";

    public async Task LoadAsync()
    {
        if (_loaded) return;
        _loaded = true;
        Economy ??= Economies[0];
        if (await GameRulesService.GetAsync() is not { ItemValues.Count: > 0 } rules)
        {
            Status = "Item values need the game rules, which this copy of the app doesn't have.";
            return;
        }
        _items = await Task.Run(() => rules.ItemValues
            .Select(v => (Name: rules.ItemName(v.Key) ?? "", v.Value.Value, v.Value.IsSubstance, Category: v.Value.TradeCategory, v.Value.Illegal))
            .Where(v => v.Name.Length > 0).ToList());
        Apply();
    }

    [RelayCommand]
    private static async Task GoBackAsync()
    {
        if (Shell.Current is not null) await Shell.Current.GoToAsync("..");
    }

    partial void OnSearchChanged(string value) => Apply();
    partial void OnEconomyChanged(FinderChoice? value) => Apply();

    private void Apply()
    {
        if (_items.Count == 0) return;
        string text = Search.Trim();
        string economy = Economy?.Value ?? "";
        var shown = _items
            .Where(i => text.Length == 0 || i.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase))
            .Where(i => economy.Length == 0 || (economy == "*" ? TradeEconomies.EconomyFor(i.Category) is not null : i.Category == economy))
            .OrderByDescending(i => i.Value).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        Rows = shown.Take(MaxRows).Select(i => new ItemValueRowViewModel(i.Name, $"{i.Value:N0} u",
            TradeEconomies.Detail(i.IsSubstance, i.Category, i.Illegal))).ToArray();
        Status = shown.Count == 0 ? "No items match." : shown.Count > MaxRows
            ? $"The {MaxRows} most valuable of {shown.Count:N0} items. Search to narrow it."
            : $"{shown.Count:N0} items, most valuable first.";
    }
}
