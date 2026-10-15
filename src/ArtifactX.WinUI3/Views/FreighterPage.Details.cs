using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// The details card can be closed to its band, like Starships' (2026-10-15 -
/// "taking up a lot of space"); closed, the band names your freighter. Open/closed is
/// remembered across visits and app restarts.
/// </summary>
public sealed partial class FreighterPage
{
    private const string DetailsCollapsedKey = "FreighterDetailsCollapsed";

    private static bool DetailsCollapsed => SettingsStore.Get(SettingsStore.Scope.Profile, DetailsCollapsedKey) == "1";

    private void DetailsToggle_Click(object sender, RoutedEventArgs e)
    {
        SettingsStore.Set(SettingsStore.Scope.Profile, DetailsCollapsedKey, DetailsCollapsed ? null : "1");
        ApplyDetailsCollapsed();
    }

    private void ApplyDetailsCollapsed()
    {
        bool collapsed = DetailsCollapsed;
        DetailsBody.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        DetailsToggleIcon.Glyph = collapsed ? "" : ""; // chevron down / up
        ToolTipService.SetToolTip(DetailsToggle, collapsed ? "Show the details" : "Hide the details");
        string name = NameEditBox.Text?.Trim() ?? "";
        DetailsHeaderTxt.Text = collapsed && name.Length > 0 ? $"FREIGHTER DETAILS  ·  {name.ToUpperInvariant()}" : "FREIGHTER DETAILS";
    }
}
