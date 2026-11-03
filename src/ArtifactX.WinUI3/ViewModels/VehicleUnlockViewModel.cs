using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Unlocks;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Newtonsoft.Json.Linq;

namespace ArtifactX.WinUI3.ViewModels;

public sealed partial class VehicleUnlockViewModel : ObservableObject
{
    private readonly VehicleUnlockService _service = new();
    private VehicleUnlockEditor Editor => VehicleUnlockEditor.Current;
    public bool HasChanges => Editor.HasChanges;
    public void ResetChanges() { Editor.Reset(); _ = RefreshAsync(); }
    private int _loadVersion, _refreshVersion;
    public ObservableCollection<VehicleUnlockGroupViewModel> Groups { get; } = [];
    [ObservableProperty] public partial string Message { get; set; } = "Loading customization unlocks…";
    private static string Display(string text) => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text.ToLowerInvariant());

    public async Task LoadAsync(ExocraftType type)
    {
        int version = ++_loadVersion;
        try
        {
            var catalog = await _service.LoadAsync();
            if (version != _loadVersion) return;
            if (catalog is null) { Message = "Update game data to see customization unlock requirements."; return; }
            var options = catalog.Options.Where(o => o.Vehicle == (int)type).ToArray();
            // Keep useful default comparisons in categories with unlockable choices;
            // do not fill the guide with dozens of always-available decal repeats.
            var shown = options.GroupBy(o => o.Group).Where(g => g.Any(o => o.SpecialId.Length > 0)).ToArray();
            foreach (var group in shown)
            {
                var existing = Groups.FirstOrDefault(g => g.Name == Display(group.Key));
                if (existing is null) { existing = new(Display(group.Key)); Groups.Add(existing); }
                foreach (var option in group)
                {
                    var rules = catalog.Titles.Where(t => t.SpecialIds.Contains(option.SpecialId)).ToArray();
                    var item = existing.Items.FirstOrDefault(i => i.Option.OptionId == option.OptionId);
                    if (item is null) existing.Items.Add(new(option, rules, existing.Items.Count + 1, Edit));
                    else item.UpdateDefinition(option, rules, existing.Items.IndexOf(item) + 1);
                }
                foreach (var old in existing.Items.Where(i => !group.Any(o => o.OptionId == i.Option.OptionId)).ToArray()) existing.Items.Remove(old);
            }
            foreach (var old in Groups.Where(g => !shown.Any(s => Display(s.Key) == g.Name)).ToArray()) Groups.Remove(old);
            var ids = Groups.SelectMany(g => g.Items).Select(i => i.Option.SpecialId).Where(s => s.Length > 0).ToArray();
            await CatalogService.WarmCacheAsync(ids);
            if (version != _loadVersion) return;
            foreach (var item in Groups.SelectMany(g => g.Items)) item.Icon = CatalogService.TryGet(item.Option.SpecialId)?.Icon;
            Message = Groups.Count == 0 ? "No unlockable customization choices were found for this vehicle in the installed data."
                : "Changes affect this save and account-wide unlocks across all saves. Hover or focus a checkbox for details. Required progress is raised only when below the minimum.";
            await RefreshAsync(LiveSaveWatcher.LatestSnapshot);
        }
        catch (Exception ex) { if (version == _loadVersion) Message = "Customization unlock information could not be read. Try updating game data."; System.Diagnostics.Debug.WriteLine(ex); }
    }

    public async Task RefreshAsync(PlanetSnapshot? snapshot = null)
    {
        int version = ++_refreshVersion;
        object? owner = SaveSessionManager.SessionIdentity;
        var stats = SaveSessionManager.GetValue(NmsPlayerStatsPaths.StatGroupsArrayPath) as JArray ?? snapshot?.StatGroups;
        // Only the small global group is read; planet history is not traversed.
        var global = stats?.FirstOrDefault(g => g[":rc"]?.Value<string>()?.TrimStart('^') == "GLOBAL_STATS")?["gUR"] as JArray;
        if (stats is not null)
        {
            int index = Enumerable.Range(0, stats.Count).FirstOrDefault(i => stats[i][":rc"]?.Value<string>()?.TrimStart('^') == "GLOBAL_STATS", -1);
            if (index >= 0) global = SaveSessionManager.GetValue(NmsPlayerStatsPaths.GroupStatsArrayPath(index)) as JArray ?? global;
        }
        var values = global?.Select(s => (Id: s["b2n"]?.Value<string>()?.TrimStart('^') ?? "",
            Value: s[">MX"]?[">vs"]?.Value<double>() ?? s[">MX"]?["eoL"]?.Value<double>() ?? 0))
            .GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First().Value);
        bool loaded = SaveSessionManager.IsSaveLoaded;
        var specials = SaveSessionManager.GetValue(VehicleUnlockEditor.SpecialsPath) as JArray;
        var local = specials?.Select(t => CatalogService.NormalizeId(t.Value<string>() ?? "")).ToHashSet(StringComparer.Ordinal) ?? [];
        VehicleUnlockAccount? account = null;
        try { if (loaded) { await Editor.LoadAsync(); account = Editor.Account; } }
        catch (Exception ex) when (ex is System.IO.IOException or Newtonsoft.Json.JsonException or UnauthorizedAccessException)
        { System.Diagnostics.Debug.WriteLine(ex); }
        if (version != _refreshVersion || !ReferenceEquals(owner, SaveSessionManager.SessionIdentity)) return;
        foreach (var item in Groups.SelectMany(g => g.Items)) item.Refresh(account, values, loaded && specials is not null, local);
    }

    private void Edit(VehicleUnlockItemViewModel item, bool enabled)
    {
        try
        {
            if (!item.CanEdit) throw new InvalidOperationException("This option is protected or no editable save is loaded.");
            Editor.Set(item.Option.SpecialId, item.Rules, enabled);
        }
        catch (Exception ex) { Message = ex.Message; }
        _ = RefreshAsync();
    }

    public void Stop() { ++_loadVersion; ++_refreshVersion; }
}

public sealed class VehicleUnlockGroupViewModel(string name)
{
    public string Name { get; } = name;
    public ObservableCollection<VehicleUnlockItemViewModel> Items { get; } = [];
}

public sealed partial class VehicleUnlockItemViewModel : ObservableObject
{
    internal VehicleCustomisationOption Option { get; private set; } = null!;
    internal TitleUnlockRule[] Rules { get; private set; } = [];
    private readonly Action<VehicleUnlockItemViewModel, bool> _edit;
    private bool _syncing;
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string Requirement { get; set; } = "";
    [ObservableProperty] public partial bool IsChecked { get; set; }
    [ObservableProperty] public partial bool CanEdit { get; set; }
    [ObservableProperty] public partial string Details { get; set; } = "";
    [ObservableProperty] public partial BitmapImage? Icon { get; set; }
    [ObservableProperty] public partial string Status { get; set; } = "";
    [ObservableProperty] public partial string Progress { get; set; } = "";
    [ObservableProperty] public partial string Notice { get; set; } = "";
    [ObservableProperty] public partial Brush StatusBrush { get; set; } = ThemeResources.Brush("ThemeAccentBrush");
    [ObservableProperty] public partial Brush ProgressBrush { get; set; } = ThemeResources.Brush("ThemeTextBrush");

    internal VehicleUnlockItemViewModel(VehicleCustomisationOption option, TitleUnlockRule[] rules, int number,
        Action<VehicleUnlockItemViewModel, bool> edit)
    { _edit = edit; UpdateDefinition(option, rules, number); }

    partial void OnIsCheckedChanged(bool value) { if (!_syncing) _edit(this, value); }

    internal void UpdateDefinition(VehicleCustomisationOption option, TitleUnlockRule[] rules, int number)
    {
        Option = option; Rules = rules;
        Name = option.Name.Length > 0 ? option.Name : $"Option {number}";
        Requirement = option.SpecialId.Length == 0 ? "Available by default."
            : rules.Length == 0 ? "Acquisition instructions are not available in the extracted title rules."
            : string.Join("\n\n", rules.Select(r => $"{r.Requirement}\nTitle: {r.Title.Replace("%NAME%", "").Trim(' ', ',')}"
                + (r.StatId.Length > 0 ? $"\nTarget: {r.StatValue:N0}" : "")));
    }

    internal void Refresh(VehicleUnlockAccount? account, Dictionary<string, double>? stats, bool loaded, HashSet<string> local)
    {
        bool owned = Option.SpecialId.Length == 0 || account?.Rewards.Contains(Option.SpecialId) == true;
        bool inSave = local.Contains(Option.SpecialId);
        _syncing = true;
        IsChecked = owned || inSave;
        _syncing = false;
        CanEdit = loaded && Option.SpecialId.Length > 0 && account is not null;
        Status = Option.SpecialId.Length == 0 ? "Available by default" : account is null
            ? loaded ? "Account ownership unavailable" : "Load a save to check ownership"
            : owned ? "Reward recorded" : "Reward not recorded";
        StatusBrush = ThemeResources.Brush(owned ? "ThemeDoneBrush" : "ThemeAccentBrush");
        Progress = loaded && stats is not null ? string.Join("\n", Rules.Where(r => r.StatId.Length > 0)
            .Select(r => $"{stats.GetValueOrDefault(r.StatId):N0} / {r.StatValue:N0} toward this title")) : "";
        ProgressBrush = ThemeResources.Brush(stats is not null && Rules.Length > 0
            && Rules.All(r => r.StatId.Length > 0 && stats.GetValueOrDefault(r.StatId) >= r.StatValue)
            ? "ThemeDoneBrush" : "ThemeTextBrush");
        Notice = !owned && !inSave && account is not null && Rules.Any(r => account.Titles.Contains(r.TitleId))
            ? "Title earned, but the part reward is not recorded. Repeating the requirement or selecting the title may not grant the missing reward." : "";
        Details = string.Join("\n\n", new[] { Name, Requirement, Progress, Notice,
            owned && Option.SpecialId.Length > 0 ? "Account-wide reward. Turning it off removes this part from account and loaded-save unlock lists; earned titles and progress remain."
                : inSave ? "Unlocked in this save. Turning it off preserves progress counters."
                : "Enabling adds this part to the save and account, records its linked title and raises verified prerequisite counters only when necessary. Save changes from the title bar." }
            .Where(s => s.Length > 0));
    }
}
