using System;
using ArtifactX.Core.Companions;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One item card on the Egg Sequencer page, worded for the selected slot.</summary>
public sealed partial class EggCatalystRowViewModel : ObservableObject
{
    /// <summary>The words the game shows for a trait above / below 0 (0x1407875f4).</summary>
    internal static readonly string[] PositiveKeys = ["UI_PET_HELPFUL_RATING", "UI_PET_AGGRESSION_RATING", "UI_PET_INDEPENDENCE_RATING"];
    internal static readonly string[] NegativeKeys = ["UI_PET_PLAYFUL_RATING", "UI_PET_GENTLENESS_RATING", "UI_PET_ATTACHMENT_RATING"];

    public EggCatalystRowViewModel(string id, string name, bool isSubstance, long value, EggCatalystEffect effect,
        EggCatalystSlot slot, int itemLimit, Func<string, string> text, long owned = 0)
    {
        Owned = owned;
        Id = id;
        Name = name;
        IsSubstance = isSubstance;
        Value = value;
        Effect = effect;
        Slot = slot;
        TraitWord = text((effect.TraitIncreases ? PositiveKeys : NegativeKeys)[(int)effect.Trait]);
        OppositeTraitWord = text((effect.TraitIncreases ? NegativeKeys : PositiveKeys)[(int)effect.Trait]);
        EffectText = slot switch
        {
            EggCatalystSlot.GrowthHormone => effect.Grows ? "▲ Bigger" : "▼ Smaller",
            EggCatalystSlot.GeneSplitter => "Remixes anatomy",
            EggCatalystSlot.DyeInjector => "Its own colours",
            _ => $"Toward {TraitWord}",
        };
        string what = slot is EggCatalystSlot.GeneSplitter or EggCatalystSlot.DyeInjector ? "Certain with" : "Full dose:";
        AmountText = !HasEffect ? "No value, so no effect"
            : Needed > itemLimit ? $"Never a full effect: a slot holds {itemLimit:N0}"
            : $"{what} {Needed:N0}";
    }

    public string Id { get; }
    public string Name { get; }
    public bool IsSubstance { get; }
    public long Value { get; }
    public EggCatalystEffect Effect { get; }
    public EggCatalystSlot Slot { get; }
    public bool HasEffect => Effect.FullDoseCount > 0;
    public string TraitWord { get; }
    public string OppositeTraitWord { get; }
    public string EffectText { get; }
    public string AmountText { get; }

    /// <summary>How many the loaded save carries across its inventories.</summary>
    public long Owned { get; }
    public bool HasOwned => Owned > 0;
    public string OwnedText => Owned > 0 ? $"You have {Owned:N0}" : "";
    public string OwnedSuffix => Owned > 0 ? $" · {OwnedText}" : "";
    public bool OwnsEnough => Owned >= Needed;

    /// <summary>How many give the slot's full effect, for sorting.</summary>
    public int Needed => Slot is EggCatalystSlot.GeneSplitter or EggCatalystSlot.DyeInjector ? Effect.CertainChangeCount : Effect.FullDoseCount;

    [ObservableProperty] public partial ImageSource? Icon { get; set; }

    public string AccessibleName => $"{Name}. {EffectText}. {AmountText}. {OwnedText}. Put in the slot.";
}
