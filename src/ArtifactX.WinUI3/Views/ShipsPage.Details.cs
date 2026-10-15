using ArtifactX.Almanac.Starship;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Views;

/// <summary>
/// The Ship Details card sits full width above the ship's tech (2026-10-15)
/// and can be closed to its band, which then reads "SHIP DETAILS · Starship 1
/// · Fighter" - open/closed is remembered across visits and app restarts.
/// </summary>
public sealed partial class ShipsPage
{
    private const string DetailsCollapsedKey = "StarshipsDetailsCollapsed";

    private static bool DetailsCollapsed => SettingsStore.Get(SettingsStore.Scope.Profile, DetailsCollapsedKey) == "1";

    private void ShipDetailsToggle_Click(object sender, RoutedEventArgs e)
    {
        SettingsStore.Set(SettingsStore.Scope.Profile, DetailsCollapsedKey, DetailsCollapsed ? null : "1");
        ApplyDetailsCollapsed();
    }

    /// <summary>Open: every field. Closed: the band alone, naming the ship.</summary>
    private void ApplyDetailsCollapsed()
    {
        bool collapsed = DetailsCollapsed;
        ShipDetailsBody.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        ShipDetailsToggleIcon.Glyph = collapsed ? "" : ""; // chevron down / up
        ToolTipService.SetToolTip(ShipDetailsToggle, collapsed ? "Show the details" : "Hide the details");
        string name = NameEditBox.Text?.Trim() ?? "";
        string type = (TypeComboBox.SelectedItem as StarshipTypeInfo)?.DisplayName ?? "";
        ShipDetailsHeaderTxt.Text = collapsed && name.Length > 0
            ? $"SHIP DETAILS  ·  {name.ToUpperInvariant()}{(type.Length > 0 ? $"  ·  {type.ToUpperInvariant()}" : "")}"
            : "SHIP DETAILS";
    }
}
