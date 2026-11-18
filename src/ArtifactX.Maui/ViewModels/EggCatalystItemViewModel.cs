using ArtifactX.Core.Companions;

namespace ArtifactX.Maui.ViewModels;

/// <summary>An item in the phone Egg Sequencer's list, worded for the selected slot (Core EggSequencerText.Label).</summary>
public sealed class EggCatalystItemViewModel(EggCatalyst item, EggCatalystLabel label)
{
    public EggCatalyst Item { get; } = item;
    public EggCatalystLabel Label { get; } = label;
    public string Name => Item.Name;
    public string EffectText => Label.EffectText;
    public string AmountText => Label.AmountText;
    public string AccessibleName => $"{Name}. {EffectText}. {AmountText}. Put in the slot.";
}
