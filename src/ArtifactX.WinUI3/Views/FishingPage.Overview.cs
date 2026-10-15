using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// The info, You are here and planet-type boxes can be hidden so the fish list
/// and Where to catch get the room (2026-10-15); remembered across visits.
/// </summary>
public sealed partial class FishingPage
{
    private const string OverviewHiddenKey = "FishingOverviewHidden";

    private static bool OverviewHidden => SettingsStore.Get(SettingsStore.Scope.Profile, OverviewHiddenKey) == "1";

    private void OverviewToggle_Click(object sender, RoutedEventArgs e)
    {
        SettingsStore.Set(SettingsStore.Scope.Profile, OverviewHiddenKey, OverviewHidden ? null : "1");
        ApplyOverviewHidden();
    }

    private void ApplyOverviewHidden()
    {
        bool hidden = OverviewHidden;
        OverviewPanel.Visibility = hidden ? Visibility.Collapsed : Visibility.Visible;
        OverviewToggleTxt.Text = hidden ? "Show overview" : "Hide overview";
        OverviewToggleIcon.Glyph = hidden ? "" : "";
    }
}
