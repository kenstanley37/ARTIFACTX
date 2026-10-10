using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// User-pickable visual themes. Each theme is a ResourceDictionary under
/// Themes/ defining the same Theme* keys (colours, gradients, fonts, corner
/// radius); Themes/ThemeStyles.xaml builds every shared style from those keys
/// only. App.xaml merges Station Core first, and this service swaps that one
/// dictionary for the saved/picked theme. Already-built elements keep their
/// old values, so MainWindow re-creates the current page on ThemeChanged.
/// </summary>
public static class ThemeService
{
    public sealed record ThemeInfo(string Id, string DisplayName, string Source);

    public static readonly IReadOnlyList<ThemeInfo> Themes = new[]
    {
        new ThemeInfo("StationCore", "Station Core", "ms-appx:///Themes/StationCore.xaml"),
        new ThemeInfo("GalaxyMap", "Galaxy Map", "ms-appx:///Themes/GalaxyMap.xaml"),
    };

    public static ThemeInfo Current { get; private set; } = Themes[0];

    public static event EventHandler? ThemeChanged;

    /// <summary>Call once at startup, before any window is created.</summary>
    public static void ApplySaved()
    {
        var saved = Themes.FirstOrDefault(t => t.Id == AppSettingsService.ThemeId);
        if (saved is not null && saved != Current)
            Swap(saved);
    }

    public static void Apply(string themeId)
    {
        var theme = Themes.FirstOrDefault(t => t.Id == themeId);
        if (theme is null || theme == Current) return;

        Swap(theme);
        AppSettingsService.SetThemeId(theme.Id);
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    private static void Swap(ThemeInfo theme)
    {
        var merged = Application.Current.Resources.MergedDictionaries;
        int index = -1;
        for (int i = 0; i < merged.Count; i++)
        {
            if (Themes.Any(t => string.Equals(merged[i].Source?.OriginalString, t.Source, StringComparison.OrdinalIgnoreCase)))
            {
                index = i;
                break;
            }
        }
        if (index < 0) return; // App.xaml always merges one; nothing to swap otherwise

        merged[index] = new ResourceDictionary { Source = new Uri(theme.Source) };
        Current = theme;
    }
}
