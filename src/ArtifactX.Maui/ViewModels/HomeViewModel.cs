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

    public ObservableCollection<RecentSystemSummary> RecentSystems { get; } =
    [
        new("Ihweyf", "Vy'keen · Trading · 2 planets, 2 moons", "11FA0164523C", "Euclid"),
        new("Odyalutai-VII", "Korvax · Advanced Materials · 5 planets, 1 moon", "1042007F09E1", "Euclid"),
    ];

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
    private static async Task OpenRecentSystemAsync(RecentSystemSummary? system)
    {
        if (system is null || Shell.Current is null) return;
        await Shell.Current.GoToAsync("//systems");
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
