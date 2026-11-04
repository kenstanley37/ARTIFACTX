using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ArtifactX.Core.Frigates;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>A stat to sort the foods by.</summary>
public sealed record FrigateStatOption(int Stat, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// The Frigates page's feeding panel: for the selected organic frigate, what
/// "Feed Vessel" would make of its traits and stats with each food (Core
/// OrganicFrigateFeeding). Foods whose ids share their first 8 characters have
/// the same effect and share a row. Read-only: nothing is fed or staged.
/// </summary>
public sealed partial class FrigateFeedingViewModel : ObservableObject
{
    private static FrigateFeedingRules? _rules;
    private static bool _loaded;
    private List<FrigateFoodRowViewModel> _all = [];
    private int _version;

    public ObservableCollection<FrigateFoodRowViewModel> Foods { get; } = [];

    /// <summary>The expedition and travel stats a food can move; the rest are 0 or 1 point.</summary>
    public IReadOnlyList<FrigateStatOption> StatOptions { get; } =
        FrigateService.StatNames.Take(7).Select((name, i) => new FrigateStatOption(i, name)).ToList();

    [ObservableProperty] public partial bool IsOrganic { get; private set; }
    [ObservableProperty] public partial string Message { get; private set; } = "";
    [ObservableProperty] public partial FrigateStatOption? SortStat { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial bool HideRefused { get; set; } = true;

    /// <summary>Show the outcomes for one frigate (null or not organic hides the panel).</summary>
    public async Task ShowAsync(FrigateSummary? frigate, IReadOnlyList<string> traits,
        IReadOnlyDictionary<string, (string DisplayName, string Description, string Category)> traitCatalog)
    {
        int version = ++_version;
        IsOrganic = frigate?.Role == "DeepSpaceCommon";
        Foods.Clear();
        _all = [];
        if (!IsOrganic || frigate is null) return;
        if (!_loaded) { _rules = await CatalogService.GetFrigateFeedingRulesAsync(); _loaded = true; }
        if (_rules is null || frigate.ResourceSeed == 0)
        {
            Message = "Read the game data again in Settings to see what each food does.";
            return;
        }

        if (version != _version) return;
        var rules = _rules;
        var primary = rules.Traits.FirstOrDefault(t => traits.FirstOrDefault() is string id && t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        SortStat ??= StatOptions.FirstOrDefault(o => o.Stat == primary?.Stat) ?? StatOptions[0];
        string TraitText(string id) => traitCatalog.TryGetValue(id, out var t) && t.Description.Length > 0 ? t.Description : id;

        var rows = await Task.Run(() => rules.Foods
            .GroupBy(OrganicFrigateFeeding.EffectKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var result = OrganicFrigateFeeding.Feed(group.First(), frigate.ResourceSeed, OrganicFrigateFeeding.OrganicClass, traits, rules);
                var names = group.Select(id => CatalogService.TryGet(id)?.DisplayName).OfType<string>().Distinct().ToList();
                return (group.First(), names, result);
            })
            .Where(g => g.names.Count > 0)
            .Select(g => new FrigateFoodRowViewModel(g.Item1, string.Join(", ", g.names), g.result,
                g.result.Traits.Select(TraitText).ToList(), FrigateService.StatNames))
            .ToList());
        if (version != _version) return;
        _all = rows;
        await CatalogService.WarmCacheAsync(_all.Select(r => r.IconId));
        if (version != _version) return;
        foreach (var row in _all) row.Icon = CatalogService.TryGet(row.IconId)?.Icon;
        Apply();
    }

    /// <summary>Stage one of the row's food in the Exosuit (ExosuitCargoService),
    /// so the player has it when they reach the Feed Vessel.</summary>
    public void AddToExosuit(FrigateFoodRowViewModel row)
    {
        string name = CatalogService.TryGet(row.IconId)?.DisplayName ?? row.Names;
        row.AddedText = ExosuitCargoService.AddOne(row.IconId) switch
        {
            ExosuitAddResult.Added => $"Added 1 {name} to your Exosuit. Save to keep it.",
            ExosuitAddResult.Full => "Your Exosuit cargo is full. Free a slot on the Exosuit page first.",
            ExosuitAddResult.NoSave => "Load a save to add items.",
            _ => $"{name} can't be added from here.",
        };
    }

    partial void OnSortStatChanged(FrigateStatOption? value) => Apply();
    partial void OnSearchTextChanged(string value) => Apply();
    partial void OnHideRefusedChanged(bool value) => Apply();

    private void Apply()
    {
        if (_all.Count == 0) return;
        int stat = SortStat?.Stat ?? 0;
        string text = SearchText.Trim();
        var rows = _all.Where(r => !HideRefused || !r.Result.Refused)
            .Where(r => text.Length == 0 || r.Names.Contains(text, StringComparison.CurrentCultureIgnoreCase))
            .OrderByDescending(r => r.Result.StatChange.ElementAtOrDefault(stat))
            .ThenByDescending(r => r.Result.StatChange.Sum())
            .ThenBy(r => r.Names, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        foreach (var row in rows) row.Focus(stat, SortStat?.Label ?? "");
        Foods.Clear();
        foreach (var row in rows) Foods.Add(row);
        int raising = rows.Count(r => r.Result.StatChange.ElementAtOrDefault(stat) > 0);
        Message = $"{raising:N0} of {rows.Count:N0} foods raise {SortStat?.Label}, biggest gain first";
    }
}

/// <summary>One food (or foods with the same effect) and what it does to the frigate.</summary>
public sealed partial class FrigateFoodRowViewModel(string iconId, string names, FrigateFeedResult result,
    IReadOnlyList<string> traits, IReadOnlyList<string> statNames) : ObservableObject
{
    public string IconId { get; } = iconId;
    public string Names { get; } = names;
    public FrigateFeedResult Result { get; } = result;
    public IReadOnlyList<string> Traits { get; } = traits;

    [ObservableProperty] public partial ImageSource? Icon { get; set; }
    [ObservableProperty] public partial string FocusText { get; private set; } = "";
    [ObservableProperty] public partial string AddedText { get; set; } = "";
    public string AddName => $"Add one {Names.Split(", ")[0]} to your Exosuit";
    [ObservableProperty] public partial Brush? FocusBrush { get; private set; }

    /// <summary>Show the change to the stat the list is sorted by.</summary>
    public void Focus(int stat, string label)
    {
        int d = Result.Refused ? 0 : Result.StatChange.ElementAtOrDefault(stat);
        FocusText = d == 0 ? $"0 {label}" : $"{(d > 0 ? "+" : "−")}{Math.Abs(d)} {label}";
        FocusBrush = d == 0 ? ThemeResources.Brush("ThemeTextDimBrush") : new FrigateStatChangeViewModel("", d > 0, stat).Brush;
    }

    public IReadOnlyList<FrigateStatChangeViewModel> Changes { get; } = result.Refused ? []
        : result.StatChange.Select((d, i) => (d, i)).Where(c => c.d != 0 && c.i < statNames.Count)
            .Select(c => new FrigateStatChangeViewModel($"{(c.d > 0 ? "+" : "−")}{Math.Abs(c.d)} {statNames[c.i]}", c.d > 0, c.i)).ToList();

    public string Summary => Result.Refused
        ? "The game refuses this food: it would not change the traits."
        : Changes.Count == 0 ? "New traits, no change to stats" : "";

    public string AccessibleName => Result.Refused ? $"{Names}. {Summary}"
        : $"{Names}. {string.Join(", ", Changes.Select(c => c.Text))}. New traits: {string.Join(", ", Traits)}.";
}

/// <summary>One stat change on a food row. Fuel Burn Rate is better when it falls.</summary>
public sealed record FrigateStatChangeViewModel(string Text, bool Rises, int Stat)
{
    private const int FuelBurnRate = 4;
    public bool IsGood => Stat == FuelBurnRate ? !Rises : Rises;
    public Brush Brush => IsGood
        ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x3D, 0xC4, 0x77))
        : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xE0, 0x4C, 0x3D));
}
