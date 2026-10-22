using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using libMBIN.NMS.GameComponents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>
/// The Frigates page: the fleet as cards, and the selected frigate's name, role, crew race,
/// class rating, seeds, stats, expedition record and traits - each edit staged as it's made
/// (FrigateService). Role, race and rating lists come from libMBIN's own enums.
/// </summary>
public sealed partial class FrigatesViewModel : ObservableObject
{
    private Dictionary<string, (string DisplayName, string Description, string Category)>? _traitCatalog;
    private List<FrigateTraitChoice> _traitChoices = new();
    private bool _loading;

    public ObservableCollection<FrigateCardViewModel> Cards { get; } = new();

    public IReadOnlyList<string> Roles { get; } = Enum.GetNames<GcFrigateClass.FrigateClassEnum>();
    public IReadOnlyList<RaceChoice> Races { get; } = Enum.GetNames<GcAlienRace.AlienRaceEnum>().Select(r => new RaceChoice(r)).ToList();
    public IReadOnlyList<string> Ratings { get; } = Enum.GetNames<GcInventoryClass.InventoryClassEnum>();

    [ObservableProperty] public partial bool HasFreighter { get; private set; } = true;
    [ObservableProperty] public partial string FleetSummary { get; private set; } = "";
    [ObservableProperty] public partial int SelectedIndex { get; private set; } = -1;
    [ObservableProperty] public partial bool HasSelection { get; private set; }

    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string? Role { get; set; }
    [ObservableProperty] public partial RaceChoice? Race { get; set; }
    [ObservableProperty] public partial string? Rating { get; set; }
    [ObservableProperty] public partial IReadOnlyList<NumberFieldViewModel> Stats { get; private set; } = [];
    [ObservableProperty] public partial IReadOnlyList<NumberFieldViewModel> Record { get; private set; } = [];
    [ObservableProperty] public partial IReadOnlyList<FrigateTraitSlotViewModel> Traits { get; private set; } = [];
    [ObservableProperty] public partial bool HasEdits { get; private set; }

    public bool NoFreighter => !HasFreighter;
    partial void OnHasFreighterChanged(bool value) => OnPropertyChanged(nameof(NoFreighter));

    /// <summary>The fleet and the selected frigate, from the loaded save.</summary>
    public async Task LoadAsync()
    {
        HasFreighter = FrigateService.HasFreighter();
        _traitCatalog ??= await CatalogService.GetFrigateTraitsAsync();
        _traitChoices = BuildTraitChoices(_traitCatalog);
        RefreshCards();
        Select(Cards.Any(c => c.Index == SelectedIndex) ? SelectedIndex : Cards.FirstOrDefault()?.Index ?? -1);
    }

    /// <summary>The cards only - after an edit, so the details being typed in aren't rebuilt.</summary>
    public void RefreshCards()
    {
        var fleet = HasFreighter ? FrigateService.ReadAll() : [];
        Cards.Clear();
        foreach (var f in fleet) Cards.Add(new FrigateCardViewModel(f) { IsSelected = f.Index == SelectedIndex });
        FleetSummary = fleet.Count == 0 ? "" : $"{fleet.Count} frigate{(fleet.Count == 1 ? "" : "s")} · " + string.Join(" · ",
            fleet.GroupBy(f => FrigateCardViewModel.RoleName(f.Role)).OrderByDescending(g => g.Count()).Select(g => $"{g.Count()} {g.Key}"));
        if (SelectedIndex >= 0) HasEdits = FrigateService.HasEdits(SelectedIndex);
    }

    public void Select(int index)
    {
        SelectedIndex = index;
        foreach (var card in Cards) card.IsSelected = card.Index == index;
        HasSelection = index >= 0;
        if (index < 0) return;

        _loading = true;
        Name = FrigateService.Text(NmsFrigatePaths.CustomNamePath(index));
        Role = FrigateService.Text(NmsFrigatePaths.FrigateClassPath(index));
        string race = FrigateService.Text(NmsFrigatePaths.RacePath(index));
        Race = Races.FirstOrDefault(r => r.Value == race);
        Rating = FrigateService.Text(NmsFrigatePaths.InventoryClassPath(index));
        _loading = false;

        var stats = FrigateService.ReadAll().FirstOrDefault(f => f.Index == index)?.Stats ?? [];
        Stats = FrigateService.StatNames.Select((name, i) => new NumberFieldViewModel(name, i.ToString(), i < stats.Length ? stats[i] : double.NaN,
            field => { FrigateService.SetStat(index, int.Parse(field.Key), field.Value); Edited(); })).ToList();

        (string Label, string[] Path)[] record =
        {
            ("Expeditions", NmsFrigatePaths.TotalNumberOfExpeditionsPath(index)),
            ("Successes", NmsFrigatePaths.TotalNumberOfSuccessfulEventsPath(index)),
            ("Failures", NmsFrigatePaths.TotalNumberOfFailedEventsPath(index)),
            ("Times Damaged", NmsFrigatePaths.NumberOfTimesDamagedPath(index)),
            ("Repairs Made", NmsFrigatePaths.RepairsMadePath(index)),
            ("Damage Taken", NmsFrigatePaths.DamageTakenPath(index)),
        };
        Record = record.Select(r => new NumberFieldViewModel(r.Label, r.Label, SaveSessionManager.GetValue(r.Path)?.Value<double>() ?? 0,
            field => { FrigateService.SetInt(r.Path, field.Value); Edited(); })).ToList();

        Traits = FrigateService.Traits(index).Select((id, slot) => new FrigateTraitSlotViewModel(slot, _traitChoices, id,
            s => { FrigateService.SetTrait(index, s.Slot, s.Selected?.Id ?? ""); Edited(); })).ToList();

        HasEdits = FrigateService.HasEdits(index);
    }

    /// <summary>The name box lost focus: staged when it changed.</summary>
    public void CommitName()
    {
        if (SelectedIndex >= 0 && FrigateService.SetText(NmsFrigatePaths.CustomNamePath(SelectedIndex), Name.Trim())) Edited();
    }

    partial void OnRoleChanged(string? value) => SetChoice(value, NmsFrigatePaths.FrigateClassPath);
    partial void OnRaceChanged(RaceChoice? value) => SetChoice(value?.Value, NmsFrigatePaths.RacePath);
    partial void OnRatingChanged(string? value) => SetChoice(value, NmsFrigatePaths.InventoryClassPath);

    private void SetChoice(string? value, Func<int, string[]> path)
    {
        if (_loading || SelectedIndex < 0 || value is null) return;
        if (FrigateService.SetText(path(SelectedIndex), value)) Edited();
    }

    public void Reset()
    {
        if (SelectedIndex < 0) return;
        FrigateService.RevertEdits(SelectedIndex);
        Select(SelectedIndex);
        RefreshCards();
    }

    /// <summary>A seed box staged a seed.</summary>
    public void Edited() => HasEdits = true;

    private static List<FrigateTraitChoice> BuildTraitChoices(Dictionary<string, (string DisplayName, string Description, string Category)> catalog)
    {
        var choices = new List<FrigateTraitChoice> { new("", "(None)", ThemeResources.Brush("ThemeTextDimBrush")) };
        choices.AddRange(catalog.OrderBy(kv => kv.Value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new FrigateTraitChoice(kv.Key, kv.Value.Description, TraitBrush(kv.Value.Category))));
        return choices;
    }

    // Good/bad trait colours carry meaning, so they stay fixed; neutral follows the theme.
    private static Brush TraitBrush(string? category) => category?.ToLowerInvariant() switch
    {
        "negative" => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xE0, 0x4C, 0x3D)),
        "positive" => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x3D, 0xC4, 0x77)),
        _ => ThemeResources.Brush("ThemeTextDimBrush"),
    };
}
