using ArtifactX.Core.Companions;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One item card on the Egg Sequencer page, worded for the selected slot (Core EggSequencerText.Label,
/// shared with the phone).</summary>
public sealed partial class EggCatalystRowViewModel : ObservableObject
{
    public EggCatalystRowViewModel(EggCatalyst item, EggCatalystSlot slot, EggSequencerRules rules, long owned = 0)
    {
        Item = item;
        Slot = slot;
        Owned = owned;
        Label = EggSequencerText.Label(item, slot, rules);
    }

    public EggCatalyst Item { get; }
    public EggCatalystLabel Label { get; }
    public string Id => Item.Id;
    public string Name => Item.Name;
    public bool IsSubstance => Item.IsSubstance;
    public long Value => Item.Value;
    public EggCatalystEffect Effect => Item.Effect;
    public EggCatalystSlot Slot { get; }
    public bool HasEffect => Label.HasEffect;
    public string TraitWord => Label.TraitWord;
    public string OppositeTraitWord => Label.OppositeTraitWord;
    public string EffectText => Label.EffectText;
    public string AmountText => Label.AmountText;

    /// <summary>How many the loaded save carries across its inventories.</summary>
    public long Owned { get; }
    public bool HasOwned => Owned > 0;
    /// <summary>A slot can hold enough of it for the full effect (100%).</summary>
    public bool ReachesFull => Label.ReachesFull;
    /// <summary>What you carry and, when it's short of a full dose, how many more you need.</summary>
    public string OwnedText => Owned <= 0 ? ""
        : ReachesFull && Owned < Needed ? $"You have {Owned:N0} · {Needed - Owned:N0} more needed" : $"You have {Owned:N0}";
    public string OwnedSuffix => Owned > 0 ? $" · {OwnedText}" : "";
    public bool OwnsEnough => Owned >= Needed;

    /// <summary>How many give the slot's full effect, for sorting.</summary>
    public int Needed => Label.Needed;

    [ObservableProperty] public partial ImageSource? Icon { get; set; }

    public string AccessibleName => $"{Name}. {EffectText}. {AmountText}. {OwnedText}. Put in the slot.";
}
