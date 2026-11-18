using System.Text.Json;
using ArtifactX.Core.Universe;
using ArtifactX.Maui.Models;

namespace ArtifactX.Maui.Services;

/// <summary>
/// The player's recently opened systems, shown on Home, and the galaxy they were last in - both kept on the phone
/// (Preferences) so a restart doesn't lose them and the address page starts in the player's galaxy instead of Euclid
/// every time (owner, 2026-11-18: players move between systems in one galaxy far more often than between galaxies).
/// </summary>
public static class RecentSystemsService
{
    private const string ItemsKey = "RecentSystems";
    private const string GalaxyKey = "LastGalaxy";
    private const int MaxItems = 10;

    // A plain list, not one pages bind to: Android can close the app's window and keep the process, and a closed page
    // still attached to a shared collection threw ObjectDisposedException when the list changed - opening a system from
    // Recent systems then failed (seen on the Pixel 2026-11-18). Pages copy it when they appear (HomeViewModel).
    private static readonly List<RecentSystemSummary> _items = Load();

    /// <summary>Newest first, at most ten.</summary>
    public static IReadOnlyList<RecentSystemSummary> Items => _items;

    /// <summary>The galaxy of the system opened last (1-256); Euclid before any.</summary>
    public static int LastGalaxy
    {
        get => Math.Clamp(Preferences.Default.Get(GalaxyKey, 1), 1, 256);
        private set => Preferences.Default.Set(GalaxyKey, value);
    }


    public static void Record(PortalSystemReadout readout)
    {
        var summary = new RecentSystemSummary(
            readout.SystemName,
            $"{readout.CompactSummary} · {readout.GalaxyName}",
            readout.PortalCode,
            readout.GalaxyName,
            readout.GalaxyNumber);

        _items.RemoveAll(i => i.GalaxyNumber == summary.GalaxyNumber
            && string.Equals(i.PortalCode[1..], summary.PortalCode[1..], StringComparison.OrdinalIgnoreCase));
        _items.Insert(0, summary);
        if (_items.Count > MaxItems) _items.RemoveRange(MaxItems, _items.Count - MaxItems);
        Save();

        LastGalaxy = readout.GalaxyNumber;
    }

    private static List<RecentSystemSummary> Load()
    {
        try
        {
            string json = Preferences.Default.Get(ItemsKey, "");
            if (json.Length > 0 && JsonSerializer.Deserialize<List<RecentSystemSummary>>(json) is { } saved)
                return saved.Take(MaxItems).ToList();
        }
        catch (JsonException)
        {
            // An unreadable list (an older layout) starts empty rather than stopping the app.
        }
        return [];
    }

    private static void Save() => Preferences.Default.Set(ItemsKey, JsonSerializer.Serialize(_items));
}
