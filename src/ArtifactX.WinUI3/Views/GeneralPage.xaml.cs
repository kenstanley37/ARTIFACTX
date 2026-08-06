using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.System;

namespace ArtifactX.WinUI3.Views;

public sealed partial class GeneralPage : Page
{
    private bool _suppressChangeEvents;

    // Creative < Relaxed < Normal < Survival < Permadeath - used only to
    // estimate "Hardest Used Preset" (see LoadValues' doc comment on why
    // that field is estimated rather than read directly). "Custom" isn't on
    // this scale on purpose - RoundedDownPreset is what stands in for it.
    private static readonly Dictionary<string, int> PresetSeverity = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Creative"] = 0,
        ["Relaxed"] = 1,
        ["Normal"] = 2,
        ["Survival"] = 3,
        ["Permadeath"] = 4,
    };

    public GeneralPage()
    {
        InitializeComponent();

        SaveSessionManager.ActiveSessionChanged += OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged += OnSessionOrEditsChanged;
        UpdateCheckService.Changed += OnUpdateCheckChanged;
        Unloaded += Page_Unloaded;
        LoadValues();
        RefreshUpdateStatus();
    }

    private void OnSessionOrEditsChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(LoadValues);

    private void OnUpdateCheckChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(RefreshUpdateStatus);

    private void RefreshUpdateStatus()
    {
        VersionTxt.Text = $"ArtifactX v{AppVersionService.DisplayVersion}";

        var result = UpdateCheckService.LastResult;
        UpdateStatusTxt.Text = result?.Message ?? "";
        ReleasePageLink.Visibility = result?.Status == UpdateCheckStatus.UpdateAvailable
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void CheckForUpdatesBtn_Click(object sender, RoutedEventArgs e)
    {
        CheckForUpdatesBtn.IsEnabled = false;
        UpdateStatusTxt.Text = "Checking...";
        try
        {
            await UpdateCheckService.RefreshAsync();
        }
        finally
        {
            CheckForUpdatesBtn.IsEnabled = true;
        }
    }

    private async void ReleasePageLink_Click(object sender, RoutedEventArgs e)
    {
        string? url = UpdateCheckService.LastResult?.ReleaseUrl;
        if (url is null) return;
        await Launcher.LaunchUriAsync(new Uri(url));
    }

    /// <summary>Frame.Navigate creates a fresh Page instance on every visit
    /// (no NavigationCacheMode set anywhere in this app) - without this,
    /// the constructor's subscriptions above would never be released, so
    /// every past visit to this page leaves a dead instance permanently
    /// subscribed to these static events. Confirmed as the real cause of a
    /// reported slowdown (edits on unrelated pages taking multi-second
    /// delays after enough navigation) - each StageEdit anywhere in the app
    /// was re-running every accumulated dead page's full reload logic.</summary>
    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.ActiveSessionChanged -= OnSessionOrEditsChanged;
        SaveSessionManager.PendingEditsChanged -= OnSessionOrEditsChanged;
        UpdateCheckService.Changed -= OnUpdateCheckChanged;
    }

    private void LoadValues()
    {
        _suppressChangeEvents = true;

        if (!SaveSessionManager.IsSaveLoaded)
        {
            UnitsBox.Value = double.NaN;
            NanitesBox.Value = double.NaN;
            QuicksilverBox.Value = double.NaN;
            PageResetBtn.Visibility = Visibility.Collapsed;
            SaveNameTxt.Text = "";
            SummaryTxt.Text = "";
            PlayTimeTxt.Text = "";
            CurrentPresetTxt.Text = "";
            EasiestPresetTxt.Text = "";
            HardestPresetTxt.Text = "";
            _suppressChangeEvents = false;
            return;
        }

        // ToDisplayValue undoes the uint32 wrap the game itself writes for a
        // balance over ~2.1 billion - see its doc comment. No-op for the
        // vast majority of saves that never get that high.
        UnitsBox.Value = NmsPlayerStateData.ToDisplayValue(SaveSessionManager.GetLong(NmsPlayerStateData.UnitsPath) ?? 0);
        NanitesBox.Value = NmsPlayerStateData.ToDisplayValue(SaveSessionManager.GetLong(NmsPlayerStateData.NanitesPath) ?? 0);
        QuicksilverBox.Value = NmsPlayerStateData.ToDisplayValue(SaveSessionManager.GetLong(NmsPlayerStateData.QuicksilverPath) ?? 0);

        SaveNameTxt.Text = SaveSessionManager.GetString(NmsSaveHeader.SaveNamePath) is { Length: > 0 } name ? name : "Unnamed Save";
        SummaryTxt.Text = SaveSessionManager.GetString(NmsPlayerStateData.LocationDescriptionPath) ?? "Unknown";
        PlayTimeTxt.Text = FormatPlayTime(ReadTotalPlayTimeSeconds());

        string current = SaveSessionManager.GetString(NmsPlayerStateData.GameModePath) ?? "Unknown";
        string roundedDown = SaveSessionManager.GetString(NmsPlayerStateData.RoundedDownPresetPath) ?? current;
        string easiest = SaveSessionManager.GetString(NmsPlayerStateData.EasiestUsedPresetPath) ?? current;
        CurrentPresetTxt.Text = current;
        EasiestPresetTxt.Text = easiest;
        HardestPresetTxt.Text = EstimateHardestPreset(roundedDown, easiest);

        _suppressChangeEvents = false;
    }

    /// <summary>Years/days/hours/minutes, since a long-played save's total
    /// can genuinely span multiple days - a plain "Nh Nm" (fine for
    /// Save Selection's compact cards) undersells that scale here.</summary>
    private static string FormatPlayTime(int totalSeconds)
    {
        var t = TimeSpan.FromSeconds(totalSeconds);
        int years = t.Days / 365;
        int days = t.Days % 365;

        var parts = new List<string>();
        if (years > 0) parts.Add($"{years}y");
        if (years > 0 || days > 0) parts.Add($"{days}d");
        parts.Add($"{t.Hours}h");
        parts.Add($"{t.Minutes}m");
        return string.Join(" ", parts);
    }

    /// <summary>The save format never populates a dedicated "hardest used"
    /// field in any sample checked (Normal/Survival/Permadeath/Custom saves
    /// all omit it - see NmsPlayerStateData.EasiestUsedPresetPath's doc
    /// comment), so this estimates it as the more severe of
    /// RoundedDownPreset (the current setting, normalized off "Custom" onto
    /// the standard preset scale) and EasiestUsedPreset - a real lower bound
    /// on the true value, not necessarily exact (2026-08-06 user-approved
    /// tradeoff: could under-report if difficulty was ever set harder and
    /// later reverted, since that history wouldn't survive in either
    /// input).</summary>
    private static string EstimateHardestPreset(string roundedDownPreset, string easiestUsedPreset)
    {
        int roundedSeverity = PresetSeverity.GetValueOrDefault(roundedDownPreset, -1);
        int easiestSeverity = PresetSeverity.GetValueOrDefault(easiestUsedPreset, -1);
        return roundedSeverity >= easiestSeverity ? roundedDownPreset : easiestUsedPreset;
    }

    // --- GLOBAL_STATS access for the TIME stat - mirrors MilestonesPage's
    // identically-shaped ResolveGlobalStatGroupIndex/ResolveStatIndex, kept
    // page-local the same way that page's own copy is (no shared helper
    // exists yet for this by-id scan pattern). Unlike
    // SaveFolderIndexingService's copy of this same scan (used for Save
    // Selection's cards), this one goes through SaveSessionManager since a
    // slot IS loaded by the time General page is showing real values.

    private static int ResolveGlobalStatGroupIndex()
    {
        if (SaveSessionManager.GetValue(NmsPlayerStatsPaths.StatGroupsArrayPath) is not JArray groups)
            return -1;

        for (int i = 0; i < groups.Count; i++)
        {
            string groupId = (groups[i]?[":rc"]?.Value<string>() ?? "").TrimStart('^');
            if (string.Equals(groupId, "GLOBAL_STATS", StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static int ResolveStatIndex(int groupIndex, string statId)
    {
        if (groupIndex < 0) return -1;
        if (SaveSessionManager.GetValue(NmsPlayerStatsPaths.GroupStatsArrayPath(groupIndex)) is not JArray stats)
            return -1;

        for (int i = 0; i < stats.Count; i++)
        {
            string id = (stats[i]?["b2n"]?.Value<string>() ?? "").TrimStart('^');
            if (string.Equals(id, statId, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    /// <summary>See project_playtime_field_fix memory - F2P is NOT total
    /// play time despite the assumption baked into its name; GLOBAL_STATS'
    /// own "TIME" stat (a float, seconds) is the real lifetime total.</summary>
    private static int ReadTotalPlayTimeSeconds()
    {
        int groupIndex = ResolveGlobalStatGroupIndex();
        int statIndex = ResolveStatIndex(groupIndex, "TIME");
        if (statIndex < 0) return 0;

        double? value = SaveSessionManager.GetValue(NmsPlayerStatsPaths.StatFloatValuePath(groupIndex, statIndex))?.Value<double?>();
        return value.HasValue ? (int)Math.Round(value.Value) : 0;
    }

    // ToRawValue re-wraps a displayed balance back to the exact bit pattern
    // the game itself already uses once it's over ~2.1 billion, so staged
    // edits round-trip the same way the game's own save data already does -
    // see NmsPlayerStateData.ToDisplayValue's doc comment.
    private void StageUnits(double value)
    {
        if (_suppressChangeEvents || double.IsNaN(value)) return;
        SaveSessionManager.StageEdit(NmsPlayerStateData.ToRawValue((long)value), NmsPlayerStateData.UnitsPath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void StageNanites(double value)
    {
        if (_suppressChangeEvents || double.IsNaN(value)) return;
        SaveSessionManager.StageEdit(NmsPlayerStateData.ToRawValue((long)value), NmsPlayerStateData.NanitesPath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void StageQuicksilver(double value)
    {
        if (_suppressChangeEvents || double.IsNaN(value)) return;
        SaveSessionManager.StageEdit(NmsPlayerStateData.ToRawValue((long)value), NmsPlayerStateData.QuicksilverPath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void UnitsBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => StageUnits(args.NewValue);
    private void UnitsBox_LostFocus(object sender, RoutedEventArgs e) => StageUnits(UnitsBox.Value);

    private void NanitesBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => StageNanites(args.NewValue);
    private void NanitesBox_LostFocus(object sender, RoutedEventArgs e) => StageNanites(NanitesBox.Value);

    private void QuicksilverBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => StageQuicksilver(args.NewValue);
    private void QuicksilverBox_LostFocus(object sender, RoutedEventArgs e) => StageQuicksilver(QuicksilverBox.Value);

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.RevertEdit(NmsPlayerStateData.UnitsPath);
        SaveSessionManager.RevertEdit(NmsPlayerStateData.NanitesPath);
        SaveSessionManager.RevertEdit(NmsPlayerStateData.QuicksilverPath);
        LoadValues();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }
}
