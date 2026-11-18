using System.Globalization;
using ArtifactX.Core.Companions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArtifactX.Maui.ViewModels;

/// <summary>
/// One catalyst slot on the phone's Egg Sequencer: the item in it, how many, the dose the game shows and in plain
/// words what it does (Core EggSequencerText.Describe, the desktop's wording). A phone has no save, so it is always
/// worded for any egg.
/// </summary>
public sealed partial class EggSlotItemViewModel : ObservableObject
{
    private readonly EggSequencerRules _rules;
    private readonly long _fullDose;

    public EggSlotItemViewModel(EggCatalystSlot slot, EggSequencerRules rules, long fullDose)
    {
        Slot = slot;
        _rules = rules;
        _fullDose = fullDose;
        Name = EggSequencerText.SlotName(slot, rules);
        Subtitle = EggSequencerText.SlotSubtitle(slot, rules);
        Refresh();
    }

    public EggCatalystSlot Slot { get; }
    public string Name { get; }
    public string Subtitle { get; }
    public int ItemLimit => _rules.ItemLimit;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasItem), nameof(ItemLine), nameof(AccessibleName))]
    public partial EggCatalyst? Item { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessibleName))]
    public partial bool IsSelected { get; set; }

    [ObservableProperty] public partial int Count { get; private set; }

    /// <summary>The amount box's text; typing a number sets <see cref="Count"/>.</summary>
    [ObservableProperty] public partial string CountEntry { get; set; } = "";

    [ObservableProperty] public partial string DoseText { get; private set; } = "";
    [ObservableProperty] public partial bool IsOverdosed { get; private set; }
    [ObservableProperty] public partial string StatusLabel { get; private set; } = "";
    [ObservableProperty] public partial string StatusText { get; private set; } = "";
    [ObservableProperty] public partial string Explanation { get; private set; } = "";

    public bool HasItem => Item is not null;

    /// <summary>The slot's line in the slot list: what is in it, or "Empty".</summary>
    public string ItemLine => Item is null ? "Empty" : $"{Count:N0} × {Item.Name}";

    public string StatusLine => $"{StatusLabel}: {StatusText}";

    public string AccessibleName => Item is null
        ? $"{Name}, empty. {Subtitle}.{(IsSelected ? " Selected." : "")}"
        : $"{Name}: {Count:N0} {Item.Name}. {DoseText}.{(IsSelected ? " Selected." : "")}";

    /// <summary>Put an item in at the amount its full effect needs (EggSequencerText.StartCount).</summary>
    public void SetItem(EggCatalyst? item)
    {
        Item = item;
        SetCount(item is null ? 0 : EggSequencerText.StartCount(Slot, item, _rules));
    }

    [RelayCommand] private void Increase() => SetCount(Count + 1);

    [RelayCommand] private void Decrease() => SetCount(Count - 1);

    [RelayCommand] private void Clear() => SetItem(null);

    partial void OnCountEntryChanged(string value)
    {
        // Only a whole number changes the amount; an empty box while typing leaves it.
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out int typed) && typed != Count)
            SetCount(typed);
    }

    private void SetCount(int value)
    {
        Count = Item is null ? 0 : Math.Clamp(value, 1, _rules.ItemLimit);
        string text = Item is null ? "" : Count.ToString(CultureInfo.CurrentCulture);
        if (CountEntry != text) CountEntry = text;
        Refresh();
    }

    private void Refresh()
    {
        var report = EggSequencerText.Describe(Slot, Item, Count, null, _fullDose, _rules);
        DoseText = report.DoseText;
        IsOverdosed = report.IsOverdosed;
        StatusLabel = report.StatusLabel;
        StatusText = report.StatusText;
        Explanation = report.Explanation;
        OnPropertyChanged(nameof(StatusLine));
        OnPropertyChanged(nameof(ItemLine));
        OnPropertyChanged(nameof(AccessibleName));
    }
}
