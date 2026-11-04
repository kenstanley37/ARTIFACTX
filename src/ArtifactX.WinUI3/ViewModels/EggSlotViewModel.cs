using System;
using System.Collections.Generic;
using ArtifactX.Core.Companions;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// One catalyst slot of the Egg Sequencer simulator: the item put in it, how
/// many, the dose the game would show and, in plain words, what it does to the
/// new companion. Nothing here touches the save.
/// </summary>
public sealed partial class EggSlotViewModel : ObservableObject
{
    private readonly EggSequencerRules _rules;
    private readonly long _fullDose;
    private readonly Func<string, string> _text;
    private EggCreature? _egg;

    public EggSlotViewModel(EggCatalystSlot slot, EggSequencerRules rules, long fullDose, Func<string, string> text)
    {
        Slot = slot;
        _rules = rules;
        _fullDose = fullDose;
        _text = text;
        string code = slot switch
        {
            EggCatalystSlot.GrowthHormone => "S",
            EggCatalystSlot.GeneSplitter => "A",
            EggCatalystSlot.DyeInjector => "C",
            _ => "P",
        };
        Name = text($"UI_EGG_MODIFIER_INPUT_{code}");
        Subtitle = text($"UI_EGG_MODIFIER_INPUT_{code}_SUB");
        BaseStatusLabel = slot == EggCatalystSlot.GrowthHormone ? text("WEIGHT_HEIGHT") : text($"UI_EGG_MODIFIER_OUT_{code}");
        Refresh();
    }

    public EggCatalystSlot Slot { get; }
    public string Name { get; }
    public string Subtitle { get; }
    private string BaseStatusLabel { get; }

    [ObservableProperty] public partial EggCatalystRowViewModel? Item { get; private set; }
    [ObservableProperty] public partial double Count { get; set; }
    [ObservableProperty] public partial bool IsSelected { get; set; }
    [ObservableProperty] public partial string DoseText { get; private set; } = "";
    [ObservableProperty] public partial bool IsOverdosed { get; private set; }
    [ObservableProperty] public partial string StatusLabel { get; private set; } = "";
    [ObservableProperty] public partial string StatusText { get; private set; } = "";
    [ObservableProperty] public partial string Explanation { get; private set; } = "";

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
        int suggested = item is null ? 0 : Math.Max(1, EggSequencer.SuggestedCount(Slot, item.Effect, _rules));
        Count = item is { Owned: > 0 } ? Math.Min(suggested, item.Owned) : suggested;
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

    private EggSpecies? Species => _egg is null ? null : _rules.Species.GetValueOrDefault(_egg.Species);

    private string TraitWord(int trait, float value) =>
        _text((value > 0 ? EggCatalystRowViewModel.PositiveKeys : EggCatalystRowViewModel.NegativeKeys)[trait]);

    private void Refresh()
    {
        OnPropertyChanged(nameof(HasItem));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(ItemName));
        OnPropertyChanged(nameof(CountText));
        StatusLabel = BaseStatusLabel;
        if (Item is null)
        {
            DoseText = "Dose: -";
            IsOverdosed = false;
            StatusText = _text("UI_EGG_MODIFIER_OUT_INHERITED");
            Explanation = $"Empty, so the new companion keeps its parent's {BaseStatusLabel.ToLowerInvariant()}.";
            OnPropertyChanged(nameof(AccessibleName));
            return;
        }

        var o = EggSequencer.Outcome(Slot, Item.IsSubstance, Item.Value, (int)Count, _fullDose, _rules);
        // The game rounds the dose down ("Dose: 0%" for one Ferrite Dust, "102%" for 102.5).
        IsOverdosed = o.Overdose > 0;
        DoseText = IsOverdosed ? $"Overdosed! ({Math.Floor((1 + o.Overdose) * 100):0}%)" : $"Dose: {Math.Floor(o.Dose * 100):0}%";
        var e = Item.Effect;
        var species = Species;
        bool locked = species is not null && Slot switch
        {
            EggCatalystSlot.GrowthHormone => !species.CanChangeGrowth,
            EggCatalystSlot.GeneSplitter => !species.CanChangeAnatomy,
            EggCatalystSlot.DyeInjector => !species.CanChangeColour,
            _ => !species.CanChangeTraits,
        };
        if (locked)
        {
            StatusText = _text("UI_EGG_MODIFIER_OUT_INHERITED");
            Explanation = $"{_egg!.Name} can't have its {BaseStatusLabel.ToLowerInvariant()} changed, so the {Name} does nothing for this egg.";
        }
        else if (o.Dose <= 0)
        {
            StatusText = _text("UI_EGG_MODIFIER_OUT_INHERITED");
            Explanation = $"{o.Count:N0} {Item.Name} is too little to make a difference. Add more.";
        }
        else switch (Slot)
        {
            case EggCatalystSlot.GrowthHormone:
                StatusText = _text(e.Grows ? "UI_EGG_MODIFIER_OUT_INCREASING" : "UI_EGG_MODIFIER_OUT_DECREASING");
                // No size numbers: the egg's stored size (unY) is not on the species' MinScale..MaxScale
                // scale (Mecha-Mouse 1.0 against 1.4-2.5, Slot 8 2026-11-04), so a before/after would be a guess.
                Explanation = $"The new companion will be {(e.Grows ? "bigger" : "smaller")}: up to {o.SizeShare:P0} of the size range its species can have.";
                Explanation += More(e.FullDoseCount, o.Count, "a full dose");
                break;
            case EggCatalystSlot.GeneSplitter:
                StatusText = _text("UI_EGG_MODIFIER_OUT_UNSTABLE");
                Explanation = (o.ChangeChance >= 1 ? "Its anatomy will be remixed: new body parts and accessories."
                        : $"{o.ChangeChance:P0} chance its anatomy is remixed (new body parts and accessories).")
                    + (o.BodyTypeChance > 0 ? $" Overdosed, so there is also a {o.BodyTypeChance:P0} chance its whole body type changes." : "")
                    + More(e.CertainChangeCount, o.Count, "a certain change");
                break;
            case EggCatalystSlot.DyeInjector:
                StatusText = _text("UI_EGG_MODIFIER_OUT_UNSTABLE");
                Explanation = (o.ChangeChance >= 1 ? "Its colours will change." : $"{o.ChangeChance:P0} chance its colours change.")
                    + $" {Item.Name} always gives the same colours; use a different item for different colours."
                    + More(e.CertainChangeCount, o.Count, "a certain change");
                break;
            default:
                string toward = Item.TraitWord, away = Item.OppositeTraitWord;
                if (_egg is not null)
                {
                    // The game names the trait by the sign of the result, or of the current value at 0.
                    int t = (int)e.Trait;
                    float before = _egg.Traits[t], after = EggSequencer.TraitAfter(before, e, o);
                    float shown = Math.Abs(after) < 1.5e-5f ? before : after;
                    StatusLabel = TraitWord(t, shown);
                    bool flipped = Math.Abs(after) >= 1.5e-5f && Math.Sign(after) != Math.Sign(before);
                    StatusText = _text(flipped || Math.Abs(after) > Math.Abs(before) ? "UI_EGG_MODIFIER_OUT_INCREASING" : "UI_EGG_MODIFIER_OUT_DECREASING");
                    Explanation = $"{_egg.Name}: {TraitWord(t, before)} {Math.Abs(before) * 100:0}% becomes {TraitWord(t, shown)} {Math.Abs(after) * 100:0}% "
                        + $"(moves {o.TraitChange:0} points toward {toward}).";
                }
                else
                {
                    StatusLabel = toward;
                    StatusText = _text("UI_EGG_MODIFIER_OUT_INCREASING");
                    Explanation = $"Its personality moves {o.TraitChange:0} points (out of 100) toward {toward}, away from {away}. "
                        + $"If the egg leans toward {away}, the game shows this as {away} decreasing. Choose your egg above for exact numbers.";
                }
                Explanation += More(e.FullDoseCount, o.Count, "a full dose");
                break;
        }
        OnPropertyChanged(nameof(AccessibleName));
    }

    private string More(int needed, int count, string what) =>
        needed > _rules.ItemLimit ? $" Even a full slot ({_rules.ItemLimit:N0}) is not {what}; a more valuable item works better."
        : needed > count ? $" {needed:N0} would make {what}." : "";
}
