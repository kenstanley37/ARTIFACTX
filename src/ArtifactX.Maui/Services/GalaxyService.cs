using ArtifactX.Core.NmsModels;
using ArtifactX.Maui.Models;

namespace ArtifactX.Maui.Services;

/// <summary>
/// The galaxy the player is viewing - one for the whole app, kept on the phone (Preferences). Shown on every page
/// (GalaxyBar) as a constant reminder, so a player who changes galaxy in game remembers to change it here (owner,
/// 2026-11-18); the address page's Galaxy box and opening a system (RecentSystemsService) set it too.
/// </summary>
public static class GalaxyService
{
    private const string Key = "LastGalaxy";

    public static IReadOnlyList<PortalGalaxyOption> All { get; } = NmsGalaxyInfo.AllGalaxies
        .Select(g => new PortalGalaxyOption(g.Number, g.Name)).ToArray();

    /// <summary>1-256; Euclid before the player picks one.</summary>
    public static int Current => Math.Clamp(Preferences.Default.Get(Key, 1), 1, 256);

    public static PortalGalaxyOption CurrentOption => All[Current - 1];

    /// <summary>The galaxy changed. Subscribe while on screen only (Loaded/Unloaded or Appearing/Disappearing): Android
    /// can close the app's window and keep the process, and a closed page left listening throws when it redraws.</summary>
    public static event Action? Changed;

    public static void Set(int galaxy)
    {
        galaxy = Math.Clamp(galaxy, 1, 256);
        if (galaxy == Current) return;
        Preferences.Default.Set(Key, galaxy);
        Changed?.Invoke();
    }
}
