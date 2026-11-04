using ArtifactX.Core.Companions;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One item card on the Egg Sequencer page.</summary>
public sealed partial class EggCatalystRowViewModel(string id, string name, EggCatalystEffect effect) : ObservableObject
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public EggCatalystEffect Effect { get; } = effect;
    public bool HasEffect => Effect.FullDoseCount > 0;

    [ObservableProperty] public partial ImageSource? Icon { get; set; }

    public string PersonalityText => (Effect.TraitIncreases ? "▲ Raises " : "▼ Lowers ") + Effect.Trait;
    public string GrowthText => Effect.Grows ? "▲ Bigger" : "▼ Smaller";
    public string DoseText => !HasEffect ? "No value, so no effect"
        : Effect.MaxDose < 1 ? $"Full slot is only a {Effect.MaxDose:P0} dose"
        : $"Full dose: {Effect.FullDoseCount:N0}";
    public string ChanceText => !HasEffect ? ""
        : Effect.MaxChangeChance < 1 ? $"Colour or anatomy change: {Effect.MaxChangeChance:P0} chance at most"
        : $"Sure to change colour or anatomy: {Effect.CertainChangeCount:N0}";

    public string AccessibleName => HasEffect
        ? $"{Name}. Neural Calibrator: {(Effect.TraitIncreases ? "raises" : "lowers")} {Effect.Trait}. Growth Hormone: {(Effect.Grows ? "bigger" : "smaller")}. {DoseText}. {ChanceText}."
        : $"{Name}. {DoseText}.";
}
