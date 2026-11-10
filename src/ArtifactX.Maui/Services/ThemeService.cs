using ArtifactX.Core.Theming;
using ArtifactX.Maui.Resources.Themes;

namespace ArtifactX.Maui.Services;

/// <summary>
/// Manages the active visual theme (Galaxy Map or Station Core), shared with the desktop app.
/// Swaps the active ResourceDictionary in Application.Current.Resources.MergedDictionaries.
/// </summary>
public static class ThemeService
{
    private const string ThemePreferenceKey = "ArtifactX.ThemeId";

    public static string CurrentThemeId { get; private set; } = ArtifactXThemes.GalaxyMap.Id;

    public static event EventHandler? ThemeChanged;

    public static void ApplySaved()
    {
        string saved = Preferences.Default.Get(ThemePreferenceKey, ArtifactXThemes.GalaxyMap.Id);
        Apply(saved, persist: false);
    }

    public static void ToggleTheme()
    {
        string next = CurrentThemeId == ArtifactXThemes.GalaxyMap.Id
            ? ArtifactXThemes.StationCore.Id
            : ArtifactXThemes.GalaxyMap.Id;
        Apply(next, persist: true);
    }

    public static void Apply(string themeId, bool persist = true)
    {
        if (Application.Current is null) return;

        var merged = Application.Current.Resources.MergedDictionaries;
        var existing = merged.FirstOrDefault(d => d is GalaxyMap or StationCore);
        if (existing is not null)
            merged.Remove(existing);

        ResourceDictionary nextDict = string.Equals(themeId, ArtifactXThemes.StationCore.Id, StringComparison.OrdinalIgnoreCase)
            ? new StationCore()
            : new GalaxyMap();

        merged.Add(nextDict);
        CurrentThemeId = nextDict is StationCore
            ? ArtifactXThemes.StationCore.Id
            : ArtifactXThemes.GalaxyMap.Id;

        if (persist)
            Preferences.Default.Set(ThemePreferenceKey, CurrentThemeId);

        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }
}
