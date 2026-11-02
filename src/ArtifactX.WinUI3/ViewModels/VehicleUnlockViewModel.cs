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
                    if (item is null) existing.Items.Add(new(option, rules, existing.Items.Count + 1));
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
                : "Requirements explain how to earn each option. Recorded rewards and progress are shown separately; meeting a requirement does not guarantee the game granted the part.";
            await RefreshAsync(LiveSaveWatcher.LatestSnapshot);
        }
        catch (Exception ex) { if (version == _loadVersion) Message = "Customization unlock information could not be read. Try updating game data."; System.Diagnostics.Debug.WriteLine(ex); }
    }

    public async Task RefreshAsync(PlanetSnapshot? snapshot = null)
    {
        int version = ++_refreshVersion;
        object? owner = SaveSessionManager.SessionIdentity;
        var stats = snapshot?.StatGroups ?? SaveSessionManager.GetValue(NmsPlayerStatsPaths.StatGroupsArrayPath) as JArray;
        // Only the small global group is read; planet history is not traversed.
        var global = stats?.FirstOrDefault(g => g[":rc"]?.Value<string>()?.TrimStart('^') == "GLOBAL_STATS")?["gUR"] as JArray;
        var values = global?.Select(s => (Id: s["b2n"]?.Value<string>()?.TrimStart('^') ?? "",
            Value: s[">MX"]?[">vs"]?.Value<double>() ?? s[">MX"]?["eoL"]?.Value<double>() ?? 0))
            .GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First().Value);
        bool loaded = SaveSessionManager.IsSaveLoaded;
        VehicleUnlockAccount? account = null;
        try { if (loaded) account = await _service.ReadAccountAsync(AccountSessionManager.ResolveAccountDataPath()); }
        catch (Exception ex) when (ex is System.IO.IOException or Newtonsoft.Json.JsonException or UnauthorizedAccessException)
        { System.Diagnostics.Debug.WriteLine(ex); }
        if (version != _refreshVersion || !ReferenceEquals(owner, SaveSessionManager.SessionIdentity)) return;
        foreach (var item in Groups.SelectMany(g => g.Items)) item.Refresh(account, values, loaded);
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
    private TitleUnlockRule[] _rules = [];
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string Requirement { get; set; } = "";
    [ObservableProperty] public partial BitmapImage? Icon { get; set; }
    [ObservableProperty] public partial string Status { get; set; } = "";
    [ObservableProperty] public partial string Progress { get; set; } = "";
    [ObservableProperty] public partial string Notice { get; set; } = "";
    [ObservableProperty] public partial Brush StatusBrush { get; set; } = ThemeResources.Brush("ThemeAccentBrush");
    [ObservableProperty] public partial Brush ProgressBrush { get; set; } = ThemeResources.Brush("ThemeTextBrush");

    internal VehicleUnlockItemViewModel(VehicleCustomisationOption option, TitleUnlockRule[] rules, int number)
        => UpdateDefinition(option, rules, number);

    internal void UpdateDefinition(VehicleCustomisationOption option, TitleUnlockRule[] rules, int number)
    {
        Option = option; _rules = rules;
        Name = option.Name.Length > 0 ? option.Name : $"Option {number}";
        Requirement = option.SpecialId.Length == 0 ? "Available by default."
            : rules.Length == 0 ? "Acquisition instructions are not available in the extracted title rules."
            : string.Join("\n\n", rules.Select(r => $"{r.Requirement}\nTitle: {r.Title.Replace("%NAME%", "").Trim(' ', ',')}"
                + (r.StatId.Length > 0 ? $"\nTarget: {r.StatValue:N0}" : "")));
    }

    internal void Refresh(VehicleUnlockAccount? account, System.Collections.Generic.Dictionary<string, double>? stats, bool loaded)
    {
        bool owned = Option.SpecialId.Length == 0 || account?.Rewards.Contains(Option.SpecialId) == true;
        Status = Option.SpecialId.Length == 0 ? "Available by default" : account is null
            ? loaded ? "Account ownership unavailable" : "Load a save to check ownership"
            : owned ? "Reward recorded" : "Reward not recorded";
        StatusBrush = ThemeResources.Brush(owned ? "ThemeDoneBrush" : "ThemeAccentBrush");
        Progress = loaded && stats is not null ? string.Join("\n", _rules.Where(r => r.StatId.Length > 0)
            .Select(r => $"{stats.GetValueOrDefault(r.StatId):N0} / {r.StatValue:N0} toward this title")) : "";
        ProgressBrush = ThemeResources.Brush(stats is not null && _rules.Length > 0
            && _rules.All(r => r.StatId.Length > 0 && stats.GetValueOrDefault(r.StatId) >= r.StatValue)
            ? "ThemeDoneBrush" : "ThemeTextBrush");
        Notice = !owned && account is not null && _rules.Any(r => account.Titles.Contains(r.TitleId))
            ? "Title earned, but the part reward is not recorded. Repeating the requirement or selecting the title may not grant the missing reward." : "";
    }
}
