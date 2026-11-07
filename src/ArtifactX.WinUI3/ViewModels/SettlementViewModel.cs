using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using libMBIN.NMS.GameComponents;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;
using P = ArtifactX.Core.NmsModels.NmsSettlementPaths;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>A perk a feature slot can hold, coloured by whether it's good or bad.</summary>
public sealed record SettlementPerkOption(string Id, string Label, string Description, Brush Foreground)
{
    /// <summary>What screen readers say for the option.</summary>
    public override string ToString() => Label;
}

/// <summary>One settlement feature slot (a Perks / OEf entry) and its perk.</summary>
public sealed partial class SettlementPerkRowViewModel : ObservableObject
{
    private readonly Action<SettlementPerkRowViewModel> _picked;
    private readonly bool _loading;

    public SettlementPerkRowViewModel(int index, IReadOnlyList<SettlementPerkOption> options, int selected, Action<SettlementPerkRowViewModel> picked)
    {
        Index = index;
        Options = options;
        _picked = picked;
        _loading = true;
        SelectedIndex = selected;
        _loading = false;
    }

    public int Index { get; }
    public string Label => $"Feature {Index + 1}";
    public IReadOnlyList<SettlementPerkOption> Options { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Selected), nameof(Swatch), nameof(Tip))]
    public partial int SelectedIndex { get; set; }

    public SettlementPerkOption? Selected => SelectedIndex >= 0 && SelectedIndex < Options.Count ? Options[SelectedIndex] : null;

    /// <summary>A dot in the perk's colour beside the dropdown - a closed ComboBox
    /// doesn't reliably repaint itself in the selected item's colour.</summary>
    public Brush Swatch => Selected?.Foreground ?? SettlementViewModel.NeutralBrush;
    public string? Tip => Selected is { Id.Length: > 0 } s ? s.Description : null;

    partial void OnSelectedIndexChanged(int value)
    {
        if (!_loading && value >= 0) _picked(this);
    }
}

/// <summary>Up to six feature slots shown together as one card; cards wrap.</summary>
public sealed class SettlementPerkCardViewModel(List<SettlementPerkRowViewModel> rows)
{
    public List<SettlementPerkRowViewModel> Rows { get; } = rows;
}

/// <summary>
/// The selected settlement's details: name, owner, culture, population, the 8 stats and
/// its feature slots. One instance for the page's lifetime, reloaded in place; every
/// edit is staged straight into the save session.
/// </summary>
public sealed partial class SettlementViewModel : ObservableObject
{
    private const int PerksPerCard = 6;

    // Positive/negative perk colours are meaning (good/bad), not decoration, so they stay
    // fixed; neutral follows the theme.
    private static readonly SolidColorBrush NegativeBrush = new(Color.FromArgb(255, 0xE0, 0x4C, 0x3D));
    private static readonly SolidColorBrush PositiveBrush = new(Color.FromArgb(255, 0x3D, 0xC4, 0x77));
    public static Brush NeutralBrush => ThemeResources.Brush("ThemeTextDimBrush");

    private static readonly string[] StatLabels = { "Max Population", "Happiness", "Production", "Upkeep", "Sentinels", "Debt", "Alert", "Bug Attack" };

    private int _index = -1;
    private bool _loading;
    private List<SettlementPerkOption>? _perkOptions;

    public SettlementViewModel()
    {
        Population = new NumberFieldViewModel("Population", "Population", double.NaN,
            f => { if (_index >= 0) SaveSessionManager.StageEdit((int)f.Value, P.PopulationPath(_index)); });
        Stats = StatLabels.Select((label, i) => new NumberFieldViewModel(label, label, double.NaN,
            f => { if (_index >= 0) SaveArrays.StageEntry(P.StatsPath(_index), i, 8, _ => (int)f.Value); })).ToList();
    }

    /// <summary>A name was committed: the picker's labels change.</summary>
    public event EventHandler? NameChanged;

    public static IReadOnlyList<RaceChoice> Races { get; } = Enum.GetNames<GcAlienRace.AlienRaceEnum>().Select(r => new RaceChoice(r)).ToList();

    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string Owner { get; private set; } = "";

    /// <summary>Culture (pA_), CONFIRMED 2026-07-30: drives the in-game "X Planetary
    /// Settlement" subtitle - Traders->Gek, Warriors->Vy'keen, Explorers->Korvax.</summary>
    [ObservableProperty] public partial int RaceIndex { get; set; } = -1;

    public NumberFieldViewModel Population { get; }

    /// <summary>The 8-slot Stats array, in save order.</summary>
    public IReadOnlyList<NumberFieldViewModel> Stats { get; }

    public ObservableCollection<SettlementPerkCardViewModel> PerkCards { get; } = new();

    public void Clear()
    {
        _index = -1;
        _loading = true;
        Name = Owner = "";
        RaceIndex = -1;
        Population.Load(double.NaN);
        foreach (var stat in Stats) stat.Load(double.NaN);
        _loading = false;
        PerkCards.Clear();
    }

    public async Task LoadAsync(int index)
    {
        _index = index;
        if (_perkOptions is null) await LoadPerkOptionsAsync();
        if (index != _index) return;

        _loading = true;
        Name = Read<string>(P.NamePath(index)) ?? "";
        string username = Read<string>(P.OwnerUsernamePath(index)) ?? "";
        string platform = Read<string>(P.OwnerPlatformPath(index)) ?? "";
        Owner = username.Length == 0 ? "" : $"{username} ({platform})";
        string race = Read<string>(P.RacePath(index)) ?? "";
        RaceIndex = Races.ToList().FindIndex(r => string.Equals(r.Value, race, StringComparison.OrdinalIgnoreCase));
        Population.Load(Read<double?>(P.PopulationPath(index)) ?? 0);
        var stats = SaveSessionManager.GetValue(P.StatsPath(index)) as JArray;
        for (int i = 0; i < Stats.Count; i++)
            Stats[i].Load(stats?.Count > i ? stats[i].Value<double>() : double.NaN);
        _loading = false;

        ShowPerks();
    }

    partial void OnNameChanged(string value)
    {
        string name = value.Trim();
        if (_loading || _index < 0 || name.Length == 0 || name == (Read<string>(P.NamePath(_index)) ?? "")) return;
        SaveSessionManager.StageEdit(name, P.NamePath(_index));
        NameChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnRaceIndexChanged(int value)
    {
        if (_loading || _index < 0 || value < 0) return;
        if (Races[value].Value != (Read<string>(P.RacePath(_index)) ?? "")) SaveSessionManager.StageEdit(Races[value].Value, P.RacePath(_index));
    }

    // ------------------------------------------------------------ features (perks)

    private async Task LoadPerkOptionsAsync()
    {
        var catalog = await CatalogService.GetSettlementPerksAsync();
        _perkOptions = new List<SettlementPerkOption> { new("", "(None)", "", NeutralBrush) };
        _perkOptions.AddRange(catalog
            .OrderBy(kv => kv.Value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new SettlementPerkOption(kv.Key, $"{kv.Value.DisplayName} ({DisplayCategory(kv.Value.Category)})",
                kv.Value.Description, SignBrush(kv.Value.Category))));
    }

    /// <summary>
    /// One dropdown per Perks (OEf) slot: "(None)" plus every cataloged perk, matched by
    /// base id (no "^", no "#NNNNN" roll suffix). Picking a different perk writes a plain
    /// "^"+id with no suffix - confirmed 2026-07-30 that Proc-flagged perks work without
    /// one (the game makes its own flavour text). Rows go in cards of six: the array
    /// isn't capped (a real settlement was grown to 19 and loaded fine in-game).
    /// </summary>
    private void ShowPerks()
    {
        PerkCards.Clear();
        if (_perkOptions is null || SaveSessionManager.GetValue(P.PerksPath(_index)) is not JArray perks) return;

        var rows = new List<SettlementPerkRowViewModel>();
        for (int i = 0; i < perks.Count; i++)
        {
            string id = SettlementService.PerkId(perks[i]?.Value<string>() ?? "");
            int selected = Math.Max(0, _perkOptions.FindIndex(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase)));
            rows.Add(new SettlementPerkRowViewModel(i, _perkOptions, selected, OnPerkPicked));
        }
        foreach (var chunk in rows.Chunk(PerksPerCard))
            PerkCards.Add(new SettlementPerkCardViewModel(chunk.ToList()));
    }

    private void OnPerkPicked(SettlementPerkRowViewModel row)
    {
        var path = P.PerksPath(_index);
        string picked = row.Selected?.Id ?? "";
        if (string.Equals(picked, SettlementService.PerkId((SaveSessionManager.GetValue(path) as JArray)?.ElementAtOrDefault(row.Index)?.Value<string>() ?? ""),
                StringComparison.OrdinalIgnoreCase)) return;
        SaveArrays.StageEntry(path, row.Index, ((JArray)SaveSessionManager.GetValue(path)!).Count, _ => "^" + picked);
    }

    /// <summary>Appends ONE new empty ("^") slot. The array isn't capped at 8: real play
    /// grew a settlement to 10, and one grown to 19 with this button loaded fine in-game
    /// (CONFIRMED 2026-07-30).</summary>
    public void AddFeatureSlot()
    {
        if (_index < 0 || SaveSessionManager.GetValue(P.PerksPath(_index)) is not JArray current) return;
        SaveSessionManager.StageEdit(new JArray(current.Select(v => v.DeepClone())) { "^" }, P.PerksPath(_index));
        ShowPerks();
    }

    /// <summary>Red for a Negative-flagged perk, green for Positive, neutral otherwise -
    /// the category always starts with Negative/Positive when it has one.</summary>
    private static Brush SignBrush(string? category) => category switch
    {
        null or "" => NeutralBrush,
        _ when category.StartsWith("Negative", StringComparison.OrdinalIgnoreCase) => NegativeBrush,
        _ when category.StartsWith("Positive", StringComparison.OrdinalIgnoreCase) => PositiveBrush,
        _ => NeutralBrush,
    };

    /// <summary>Category flags minus "Proc" - those perks' names already say "(random name)".</summary>
    private static string DisplayCategory(string category) => string.Join(", ",
        category.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(flag => !flag.Equals("Proc", StringComparison.OrdinalIgnoreCase)));

    private static T? Read<T>(string[] path) => SaveSessionManager.GetValue(path) is JToken token ? token.Value<T>() : default;
}
