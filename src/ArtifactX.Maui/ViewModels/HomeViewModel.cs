using ArtifactX.Core.Theming;
using ArtifactX.Maui.Models;
using ArtifactX.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ArtifactX.Maui.ViewModels;

public sealed partial class HomeViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string ActiveThemeLabel { get; set; } = GetThemeDisplayName();

    /// <summary>This page's own copy of Recent systems (RecentSystemsService), refreshed each time Home appears.</summary>
    public ObservableCollection<RecentSystemSummary> RecentSystems { get; } = [];

    public void RefreshRecentSystems()
    {
        if (RecentSystems.SequenceEqual(RecentSystemsService.Items)) return;
        RecentSystems.Clear();
        foreach (var system in RecentSystemsService.Items) RecentSystems.Add(system);
    }

    [RelayCommand]
    private static async Task OpenPortalGlyphsAsync()
    {
        if (Shell.Current is not null)
            await Shell.Current.GoToAsync("//systems");
    }

    [RelayCommand]
    private static async Task OpenRecipesAsync()
    {
        if (Shell.Current is not null)
            await Shell.Current.GoToAsync("//recipes");
    }

    [RelayCommand]
    private static async Task OpenEggSequencerAsync()
    {
        if (Shell.Current is not null)
            await Shell.Current.GoToAsync("//eggs");
    }

    [RelayCommand]
    private static async Task OpenItemValuesAsync()
    {
        if (Shell.Current is not null)
            await Shell.Current.GoToAsync("item-values");
    }

    [RelayCommand]
    private static async Task OpenRecentSystemAsync(RecentSystemSummary? system)
    {
        if (system is null || Shell.Current is null) return;
        await Shell.Current.GoToAsync($"system-details?code={Uri.EscapeDataString(system.PortalCode)}&galaxy={system.GalaxyNumber}");
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        ThemeService.ToggleTheme();
        ActiveThemeLabel = GetThemeDisplayName();
    }

    private static string GetThemeDisplayName() =>
        ThemeService.CurrentThemeId == ArtifactXThemes.StationCore.Id
            ? "Station Core"
            : "Galaxy Map";
}
