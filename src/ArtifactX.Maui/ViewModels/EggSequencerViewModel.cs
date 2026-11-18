using ArtifactX.Core.Companions;
using ArtifactX.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArtifactX.Maui.ViewModels;

/// <summary>
/// The phone's Egg Sequencer, the desktop page's rules without a save: pick one of the four catalyst slots, see only the
/// items it takes (filtered by the effect wanted), tap one to put it in, and read the dose the game will show and what
/// it does in plain words. Rules and item values come from the rules file (GameRulesService); the wording is Core
/// EggSequencerText, shared with the desktop.
/// </summary>
public sealed partial class EggSequencerViewModel : ObservableObject
{
    /// <summary>Items listed before "Show more": enough to choose from without a long scroll on a phone.</summary>
    private const int PageSize = 30;

    private EggSequencerRules? _rules;
    private List<EggCatalyst> _items = [];
    private List<EggCatalystItemViewModel> _slotItems = [];
    private List<EggCatalystItemViewModel> _matches = [];
    private int _shown = PageSize;
    private bool _loaded;

    /// <summary>Raised after an item goes into a slot, so the page can scroll up to show the result.</summary>
    public event Action? ItemPlaced;

    public IReadOnlyList<EggSlotItemViewModel> Slots { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ListHeading))]
    public partial EggSlotItemViewModel? SelectedSlot { get; private set; }

    [ObservableProperty] public partial IReadOnlyList<EggCatalystFilter> Filters { get; private set; } = [];
    [ObservableProperty] public partial EggCatalystFilter? Filter { get; set; }
    [ObservableProperty] public partial string Search { get; set; } = "";
    /// <summary>Only items a slot can hold enough of for the full effect.</summary>
    [ObservableProperty] public partial bool OnlyFullEffect { get; set; }
    [ObservableProperty] public partial IReadOnlyList<EggCatalystItemViewModel> Items { get; private set; } = [];
    [ObservableProperty] public partial bool HasMore { get; private set; }
    [ObservableProperty] public partial string MoreLabel { get; private set; } = "";
    [ObservableProperty] public partial string Message { get; private set; } = "Loading the Egg Sequencer rules...";
    [ObservableProperty] public partial string DoseNote { get; private set; } = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotReady))]
    public partial bool IsReady { get; private set; }

    public bool IsNotReady => !IsReady;

    public string ListHeading => SelectedSlot is null ? "" : $"PUT IN THE {SelectedSlot.Name.ToUpperInvariant()}";

    public async Task LoadAsync()
    {
        if (_loaded) return;
        _loaded = true;
        var game = await GameRulesService.GetAsync();
        if (game?.EggSequencer is not { Ingredients: not null, Texts: not null, Species: not null } rules || game.ItemValues.Count == 0)
        {
            Message = "The Egg Sequencer needs the game rules, which this copy of the app doesn't have.";
            return;
        }
        _rules = rules;
        long fullDose = game.ItemValues.GetValueOrDefault(rules.FullDoseProductId)?.Value ?? 0;
        _items = await Task.Run(() => EggSequencer.Catalysts(rules,
            game.ItemValues.Select(v => (v.Key, game.ItemName(v.Key) ?? "", v.Value.IsSubstance, v.Value.Value))
                .Where(v => v.Item2.Length > 0), fullDose));
        DoseNote = EggSequencerText.DoseNote(rules, fullDose, game.ItemName(rules.FullDoseProductId));
        Slots = Enum.GetValues<EggCatalystSlot>().Select(s => new EggSlotItemViewModel(s, rules, fullDose)).ToArray();
        OnPropertyChanged(nameof(Slots));
        IsReady = true;
        // The Neural Calibrator first, as on the desktop: personality is what most players sequence for.
        SelectSlot(Slots[^1]);
    }

    [RelayCommand]
    private void SelectSlot(EggSlotItemViewModel? slot)
    {
        if (slot is null || _rules is null) return;
        foreach (var s in Slots) s.IsSelected = s == slot;
        SelectedSlot = slot;
        _slotItems = _items.Select(i => new EggCatalystItemViewModel(i, EggSequencerText.Label(i, slot.Slot, _rules))).ToList();
        Filters = EggSequencerText.Filters(slot.Slot, _slotItems.Select(i => i.Label));
        Filter = Filters[0];
        Apply();
    }

    [RelayCommand]
    private void Pick(EggCatalystItemViewModel? item)
    {
        if (item is null || SelectedSlot is null) return;
        SelectedSlot.SetItem(item.Item);
        ItemPlaced?.Invoke();
    }

    [RelayCommand]
    private void ShowMore()
    {
        _shown += PageSize;
        Show();
    }

    partial void OnFilterChanged(EggCatalystFilter? value) => Apply();
    partial void OnSearchChanged(string value) => Apply();
    partial void OnOnlyFullEffectChanged(bool value) => Apply();

    private void Apply()
    {
        if (SelectedSlot is null) return;
        string text = Search.Trim();
        _matches = _slotItems.Where(i => i.Label.HasEffect)
            .Where(i => Filter?.Matches(i.Item, i.Label) ?? true)
            .Where(i => text.Length == 0 || i.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase))
            .Where(i => !OnlyFullEffect || i.Label.ReachesFull)
            .OrderBy(i => i.Label.Needed)
            .ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        _shown = PageSize;
        Message = _matches.Count == 0 ? "No items match. Try another effect or clear the search."
            : $"{_matches.Count:N0} items work in the {SelectedSlot.Name}, fewest needed first. Tap one to put it in.";
        Show();
    }

    private void Show()
    {
        Items = _matches.Take(_shown).ToArray();
        int left = _matches.Count - Items.Count;
        HasMore = left > 0;
        MoreLabel = $"Show {Math.Min(left, PageSize):N0} more ({left:N0} left)";
    }
}
