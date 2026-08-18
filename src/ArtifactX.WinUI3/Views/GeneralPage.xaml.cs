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
            SaveNameBox.Text = "";
            SummaryBox.Text = "";
            PlayTimeTxt.Text = "";
            CurrentPresetBox.SelectedIndex = -1;
            EasiestPresetBox.SelectedIndex = -1;
            HardestPresetBox.SelectedIndex = -1;
            _suppressChangeEvents = false;
            return;
        }

        // ToDisplayValue undoes the uint32 wrap the game itself writes for a
        // balance over ~2.1 billion - see its doc comment. No-op for the
        // vast majority of saves that never get that high.
        UnitsBox.Value = NmsPlayerStateData.ToDisplayValue(SaveSessionManager.GetLong(NmsPlayerStateData.UnitsPath) ?? 0);
        NanitesBox.Value = NmsPlayerStateData.ToDisplayValue(SaveSessionManager.GetLong(NmsPlayerStateData.NanitesPath) ?? 0);
        QuicksilverBox.Value = NmsPlayerStateData.ToDisplayValue(SaveSessionManager.GetLong(NmsPlayerStateData.QuicksilverPath) ?? 0);

        SaveNameBox.Text = SaveSessionManager.GetString(NmsSaveHeader.SaveNamePath) is { Length: > 0 } name ? name : "Unnamed Save";
        SummaryBox.Text = SaveSessionManager.GetString(NmsPlayerStateData.LocationDescriptionPath) ?? "";
        PlayTimeTxt.Text = FormatPlayTime(ReadTotalPlayTimeSeconds());

        string current = SaveSessionManager.GetString(NmsPlayerStateData.GameModePath) ?? "Normal";
        string easiest = SaveSessionManager.GetString(NmsPlayerStateData.EasiestUsedPresetPath) ?? current;
        string hardest = SaveSessionManager.GetString(NmsPlayerStateData.HardestUsedPresetPath) ?? current;
        SelectComboBoxContent(CurrentPresetBox, current);
        SelectComboBoxContent(EasiestPresetBox, easiest);
        SelectComboBoxContent(HardestPresetBox, hardest);

        _suppressChangeEvents = false;
    }

    private static void SelectComboBoxContent(ComboBox box, string content)
    {
        foreach (var item in box.Items)
        {
            if (item is ComboBoxItem { Content: string text } && string.Equals(text, content, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedItem = item;
                return;
            }
        }
        box.SelectedIndex = -1;
    }

    private void SaveNameBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressChangeEvents || !SaveSessionManager.IsSaveLoaded) return;
        SaveSessionManager.StageEdit(SaveNameBox.Text, NmsSaveHeader.SaveNamePath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void SummaryBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressChangeEvents || !SaveSessionManager.IsSaveLoaded) return;
        SaveSessionManager.StageEdit(SummaryBox.Text, NmsPlayerStateData.LocationDescriptionPath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void CurrentPresetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressChangeEvents || !SaveSessionManager.IsSaveLoaded) return;
        if ((CurrentPresetBox.SelectedItem as ComboBoxItem)?.Content is not string value) return;
        SaveSessionManager.StageEdit(value, NmsPlayerStateData.GameModePath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void EasiestPresetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressChangeEvents || !SaveSessionManager.IsSaveLoaded) return;
        if ((EasiestPresetBox.SelectedItem as ComboBoxItem)?.Content is not string value) return;
        SaveSessionManager.StageEdit(value, NmsPlayerStateData.EasiestUsedPresetPath);
        PageResetBtn.Visibility = Visibility.Visible;
    }

    private void HardestPresetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressChangeEvents || !SaveSessionManager.IsSaveLoaded) return;
        if ((HardestPresetBox.SelectedItem as ComboBoxItem)?.Content is not string value) return;
        SaveSessionManager.StageEdit(value, NmsPlayerStateData.HardestUsedPresetPath);
        PageResetBtn.Visibility = Visibility.Visible;
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

    // 4294967295 (uint32.MaxValue) - the same real, confirmed wraparound
    // ceiling the NumberBoxes above already cap at (see that Maximum's own
    // doc comment), independently confirmed by a reference tool's own Max
    // button landing on the exact same number (2026-08-06). Setting .Value
    // is enough - the existing ValueChanged handlers above stage the edit,
    // same as if the user had typed it in.
    private void MaxUnitsBtn_Click(object sender, RoutedEventArgs e) => UnitsBox.Value = 4294967295;
    private void MaxNanitesBtn_Click(object sender, RoutedEventArgs e) => NanitesBox.Value = 4294967295;
    private void MaxQuicksilverBtn_Click(object sender, RoutedEventArgs e) => QuicksilverBox.Value = 4294967295;

    private void PageResetBtn_Click(object sender, RoutedEventArgs e)
    {
        SaveSessionManager.RevertEdit(NmsPlayerStateData.UnitsPath);
        SaveSessionManager.RevertEdit(NmsPlayerStateData.NanitesPath);
        SaveSessionManager.RevertEdit(NmsPlayerStateData.QuicksilverPath);
        SaveSessionManager.RevertEdit(NmsSaveHeader.SaveNamePath);
        SaveSessionManager.RevertEdit(NmsPlayerStateData.LocationDescriptionPath);
        SaveSessionManager.RevertEdit(NmsPlayerStateData.GameModePath);
        SaveSessionManager.RevertEdit(NmsPlayerStateData.EasiestUsedPresetPath);
        SaveSessionManager.RevertEdit(NmsPlayerStateData.HardestUsedPresetPath);
        LoadValues();
        PageResetBtn.Visibility = Visibility.Collapsed;
    }
}
