using System;
using ArtifactX.Core.Companions;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// One catalyst slot of the Egg Sequencer simulator: the item put in it, how
/// many, the dose the game would show and, in plain words, what it does to the
/// new companion (Core EggSequencerText.Describe, shared with the phone).
/// Nothing here touches the save.
/// </summary>
public sealed partial class EggSlotViewModel : ObservableObject
{
    private readonly EggSequencerRules _rules;
    private readonly long _fullDose;
    private EggCreature? _egg;

    public EggSlotViewModel(EggCatalystSlot slot, EggSequencerRules rules, long fullDose)
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

    [ObservableProperty] public partial EggCatalystRowViewModel? Item { get; private set; }
    [ObservableProperty] public partial double Count { get; set; }
    [ObservableProperty] public partial bool IsSelected { get; set; }
    [ObservableProperty] public partial string DoseText { get; private set; } = "";
    [ObservableProperty] public partial bool IsOverdosed { get; private set; }
    [ObservableProperty] public partial string StatusLabel { get; private set; } = "";
    [ObservableProperty] public partial string StatusText { get; private set; } = "";
    [ObservableProperty] public partial string Explanation { get; private set; } = "";
    /// <summary>What the slot does to the chosen egg, or null when it does nothing (empty,
    /// too little, or the species can't change here). Read by the "after" preview.</summary>
    [ObservableProperty] public partial EggSlotOutcome? Result { get; private set; }

    /// <summary>The egg's size after this slot (Growth Hormone only), or null.</summary>
    public float? SizeAfter { get; private set; }

    public bool HasItem => Item is not null;
    public bool IsEmpty => Item is null;
    public ImageSource? Icon => Item?.Icon;
    public string ItemName => Item?.Name ?? "";
    public string CountText => Item is null ? "" : $"{(int)Count:N0}";
    public string AccessibleName => Item is null ? $"{Name}, empty. {Subtitle}." : $"{Name}: {(int)Count} {Item.Name}. {DoseText}. {Explanation}";

    /// <summary>Put an item in the slot at the amount the game needs for its full effect.</summary>
    /// When the save carries some, no more than it has (the game only takes what you carry).
    public void SetItem(EggCatalystRowViewModel? item)
    {
        Item = item;
        Count = item is null ? 0 : EggSequencerText.StartCount(Slot, item.Item, _rules, item.Owned);
        Refresh();
    }

    partial void OnCountChanged(double value)
    {
        if (double.IsNaN(value) || value < 0) { Count = 0; return; }
        if (value > _rules.ItemLimit) { Count = _rules.ItemLimit; return; }
        Refresh();
    }

    partial void OnIsSelectedChanged(bool value) => OnPropertyChanged(nameof(AccessibleName));

    /// <summary>The egg being sequenced (from the save), for exact before/after values; null for none.</summary>
    public void SetEgg(EggCreature? egg)
    {
        _egg = egg;
        Refresh();
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(HasItem));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(ItemName));
        OnPropertyChanged(nameof(CountText));
        var report = EggSequencerText.Describe(Slot, Item?.Item, (int)Count, _egg, _fullDose, _rules,
            "Choose your egg above for exact numbers.");
        SizeAfter = report.SizeAfter;
        DoseText = report.DoseText;
        IsOverdosed = report.IsOverdosed;
        StatusLabel = report.StatusLabel;
        StatusText = report.StatusText;
        Explanation = report.Explanation;
        Result = report.Result;
        OnPropertyChanged(nameof(AccessibleName));
    }
}
