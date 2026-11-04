using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ArtifactX.Core.Companions;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>A choice in one of the Egg Sequencer page's filter boxes.</summary>
public sealed record EggFilterOption(string Label, Func<EggCatalystRowViewModel, bool> Matches)
{
    public override string ToString() => Label;
}

/// <summary>
/// The Egg Sequencer reference page: what every substance and product does in
/// each catalyst slot (Core EggSequencer, rules from the catalog's
/// EGGSEQUENCER row, values from ItemValues). Read-only; nothing is written to
/// the save.
/// </summary>
public sealed partial class EggSequencerViewModel : ObservableObject
{
    private List<EggCatalystRowViewModel> _all = [];

    public ObservableCollection<EggCatalystRowViewModel> Items { get; } = [];

    public IReadOnlyList<EggFilterOption> PersonalityOptions { get; } =
    [
        new("Any personality effect", _ => true),
        .. Enum.GetValues<EggTrait>().SelectMany(t => new[]
        {
            new EggFilterOption($"Raises {t}", r => r.Effect.Trait == t && r.Effect.TraitIncreases),
            new EggFilterOption($"Lowers {t}", r => r.Effect.Trait == t && !r.Effect.TraitIncreases),
        }),
    ];

    public IReadOnlyList<EggFilterOption> GrowthOptions { get; } =
    [
        new("Any size effect", _ => true),
        new("Makes it bigger", r => r.Effect.Grows),
        new("Makes it smaller", r => !r.Effect.Grows),
    ];

    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial EggFilterOption? Personality { get; set; }
    [ObservableProperty] public partial EggFilterOption? Growth { get; set; }
    [ObservableProperty] public partial bool HideNoEffect { get; set; } = true;
    [ObservableProperty] public partial string Message { get; set; } = "Loading the Egg Sequencer rules...";
    [ObservableProperty] public partial string FullDoseText { get; set; } = "";

    public EggSequencerViewModel()
    {
        Personality = PersonalityOptions[0];
        Growth = GrowthOptions[0];
    }

    public async Task LoadAsync()
    {
        var rules = await CatalogService.GetEggSequencerRulesAsync();
        var values = await CatalogService.GetItemValuesAsync();
        if (rules is null || values.Count == 0)
        {
            Message = "Read the game data again in Settings to see the Egg Sequencer rules.";
            return;
        }
        long fullDose = values.FirstOrDefault(v => string.Equals(v.Id, rules.FullDoseProductId, StringComparison.OrdinalIgnoreCase))?.Value ?? 0;
        string fullDoseName = values.FirstOrDefault(v => string.Equals(v.Id, rules.FullDoseProductId, StringComparison.OrdinalIgnoreCase))?.Name ?? "";
        int points = (int)Math.Round(EggSequencer.FullDoseTraitChange(rules) * 100);
        FullDoseText = $"A full dose is worth {fullDose:N0} units (one {fullDoseName}); raw substances count {rules.SubstanceModifier:0.#} times their value. "
            + $"A full dose moves a personality trait by {points} points.";

        _all = await Task.Run(() => values
            .Where(v => v.Kind is "Product" or "Substance")
            .Select(v => new EggCatalystRowViewModel(v.Id, v.Name,
                EggSequencer.Effect(v.Id, v.Kind == "Substance", v.Value, fullDose, rules)))
            .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList());
        await CatalogService.WarmCacheAsync(_all.Select(r => r.Id));
        foreach (var row in _all) row.Icon = CatalogService.TryGet(row.Id)?.Icon;
        Apply();
    }

    partial void OnSearchTextChanged(string value) => Apply();
    partial void OnPersonalityChanged(EggFilterOption? value) => Apply();
    partial void OnGrowthChanged(EggFilterOption? value) => Apply();
    partial void OnHideNoEffectChanged(bool value) => Apply();

    private void Apply()
    {
        if (_all.Count == 0) return;
        string text = SearchText.Trim();
        var rows = _all.Where(r => text.Length == 0 || r.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase))
            .Where(r => !HideNoEffect || r.HasEffect)
            .Where(r => Personality?.Matches(r) ?? true)
            .Where(r => Growth?.Matches(r) ?? true)
            .ToList();
        Items.Clear();
        foreach (var row in rows) Items.Add(row);
        Message = $"{rows.Count:N0} of {_all.Count:N0} items";
    }
}
