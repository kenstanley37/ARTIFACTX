using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ArtifactX.Core.Companions;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>A choice in the Egg Sequencer page's effect filter.</summary>
public sealed record EggFilterOption(string Label, Func<EggCatalystRowViewModel, bool> Matches)
{
    public override string ToString() => Label;
}

/// <summary>
/// The Egg Sequencer page: a copy of the game's sequencer screen. Pick a
/// catalyst slot to list only the items it takes, filtered by the effect
/// wanted; put one in and the slot shows the game's dose and, in plain words,
/// what will happen (EggSlotViewModel). Rules from Core EggSequencer and the
/// catalog's EGGSEQUENCER row, values from ItemValues. Nothing touches the save.
/// </summary>
public sealed partial class EggSequencerViewModel : ObservableObject
{
    private EggSequencerRules? _rules;
    private List<(ItemValueInfo Item, EggCatalystEffect Effect)> _items = [];
    private List<EggCatalystRowViewModel> _slotRows = [];

    public ObservableCollection<EggSlotViewModel> Slots { get; } = [];
    public ObservableCollection<EggCatalystRowViewModel> Items { get; } = [];

    [ObservableProperty] public partial EggSlotViewModel? SelectedSlot { get; private set; }
    [ObservableProperty] public partial IReadOnlyList<EggFilterOption> FilterOptions { get; private set; } = [];
    [ObservableProperty] public partial EggFilterOption? Filter { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial string Message { get; private set; } = "Loading the Egg Sequencer rules...";
    [ObservableProperty] public partial string Title { get; private set; } = "";
    [ObservableProperty] public partial string CatalystsHeading { get; private set; } = "";
    [ObservableProperty] public partial string StatusHeading { get; private set; } = "";
    [ObservableProperty] public partial string DoseNote { get; private set; } = "";
    [ObservableProperty] public partial bool IsReady { get; private set; }

    public async Task LoadAsync()
    {
        var rules = await CatalogService.GetEggSequencerRulesAsync();
        var values = await CatalogService.GetItemValuesAsync();
        if (rules is null || values.Count == 0)
        {
            Message = "Read the game data again in Settings to use the Egg Sequencer.";
            return;
        }
        _rules = rules;
        var full = values.FirstOrDefault(v => string.Equals(v.Id, rules.FullDoseProductId, StringComparison.OrdinalIgnoreCase));
        long fullDose = full?.Value ?? 0;
        var ingredients = rules.Ingredients.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _items = await Task.Run(() => values
            .Where(v => v.Kind is "Product" or "Substance" && ingredients.Contains(v.Id))
            .Select(v => (v, EggSequencer.Effect(v.Id, v.Kind == "Substance", v.Value, fullDose, rules)))
            .ToList());
        await CatalogService.WarmCacheAsync(_items.Select(i => i.Item.Id));

        Title = Text("UI_EGG_MODIFIER_NAME_L");
        CatalystsHeading = Text("UI_EGG_MODIFIER_INPUT_HEADING");
        StatusHeading = Text("UI_EGG_MODIFIER_OUTPUT_HEADING");
        DoseNote = $"A full dose is {fullDose:N0} units of value, the worth of one {full?.Name ?? rules.FullDoseProductId}. "
            + $"Raw materials count {rules.SubstanceModifier:0.#} times their value. A slot holds up to {rules.ItemLimit:N0}.";
        Slots.Clear();
        foreach (var slot in Enum.GetValues<EggCatalystSlot>()) Slots.Add(new EggSlotViewModel(slot, rules, fullDose, Text));
        IsReady = true;
        Select(Slots[^1]);
    }

    /// <summary>Show the items for a slot.</summary>
    public void Select(EggSlotViewModel slot)
    {
        if (_rules is null) return;
        foreach (var s in Slots) s.IsSelected = s == slot;
        SelectedSlot = slot;
        _slotRows = _items.Select(i => new EggCatalystRowViewModel(i.Item.Id, i.Item.Name, i.Item.Kind == "Substance", i.Item.Value,
                i.Effect, slot.Slot, _rules.ItemLimit, Text) { Icon = CatalogService.TryGet(i.Item.Id)?.Icon })
            .ToList();
        FilterOptions = Options(slot.Slot);
        Filter = FilterOptions[0];
        Apply();
    }

    /// <summary>Put an item in the selected slot.</summary>
    public void Pick(EggCatalystRowViewModel row) => SelectedSlot?.SetItem(row);

    public void ClearSelectedSlot() => SelectedSlot?.SetItem(null);

    partial void OnFilterChanged(EggFilterOption? value) => Apply();
    partial void OnSearchTextChanged(string value) => Apply();

    private IReadOnlyList<EggFilterOption> Options(EggCatalystSlot slot)
    {
        var any = new EggFilterOption("Any effect", _ => true);
        return slot switch
        {
            EggCatalystSlot.GrowthHormone =>
                [any, new("Makes it bigger", r => r.Effect.Grows), new("Makes it smaller", r => !r.Effect.Grows)],
            EggCatalystSlot.NeuralCalibrator =>
                [any, .. _slotRows.Select(r => r.TraitWord).Distinct().Order()
                    .Select(word => new EggFilterOption($"Toward {word}", r => r.TraitWord == word))],
            _ => [any, new($"Certain with 10 or fewer", r => r.Needed <= 10)],
        };
    }

    private void Apply()
    {
        if (_slotRows.Count == 0) return;
        string text = SearchText.Trim();
        var rows = _slotRows.Where(r => r.HasEffect)
            .Where(r => Filter?.Matches(r) ?? true)
            .Where(r => text.Length == 0 || r.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(r => r.Needed)
            .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        Items.Clear();
        foreach (var row in rows) Items.Add(row);
        Message = $"{rows.Count:N0} items work in the {SelectedSlot?.Name}, fewest needed first. Click one to put it in.";
    }

    private string Text(string key) => _rules?.Texts.TryGetValue(key, out var t) == true && t.Length > 0 ? t : key;
}
