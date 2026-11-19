using System.Text.Json;
using ArtifactX.Core.Universe;
using ArtifactX.Maui.Models;

namespace ArtifactX.Maui.Services;

/// <summary>
/// The player's recently opened systems, shown on Home, kept on the phone (AppStore) so a restart doesn't lose them.
/// Opening a system also makes its galaxy the one being viewed (GalaxyService).
/// </summary>
public static class RecentSystemsService
{
    private const string ItemsKey = "RecentSystems";
    private const int MaxItems = 10;

    // A plain list, not one pages bind to: Android can close the app's window and keep the process, and a closed page
    // still attached to a shared collection threw ObjectDisposedException when the list changed - opening a system from
    // Recent systems then failed (seen on the Pixel 2026-11-18). Pages copy it when they appear (HomeViewModel).
    private static List<RecentSystemSummary>? _items;

    /// <summary>Newest first, at most ten. Read on first use, after MauiProgram has set the store.</summary>
    public static IReadOnlyList<RecentSystemSummary> Items => _items ??= Load();


    public static void Record(PortalSystemReadout readout)
    {
        var summary = new RecentSystemSummary(
            readout.SystemName,
            $"{readout.CompactSummary} · {readout.GalaxyName}",
            readout.PortalCode,
            readout.GalaxyName,
            readout.GalaxyNumber);

        var items = _items ??= Load();
        items.RemoveAll(i => i.GalaxyNumber == summary.GalaxyNumber
            && string.Equals(i.PortalCode[1..], summary.PortalCode[1..], StringComparison.OrdinalIgnoreCase));
        items.Insert(0, summary);
        if (items.Count > MaxItems) items.RemoveRange(MaxItems, items.Count - MaxItems);
        AppStore.Current.Set(ItemsKey, JsonSerializer.Serialize(items));

        GalaxyService.Set(readout.GalaxyNumber);
    }

    private static List<RecentSystemSummary> Load()
    {
        try
        {
            if (AppStore.Current.Get(ItemsKey) is { Length: > 0 } json && JsonSerializer.Deserialize<List<RecentSystemSummary>>(json) is { } saved)
                return saved.Take(MaxItems).ToList();
        }
        catch (JsonException)
        {
            // An unreadable list (an older layout) starts empty rather than stopping the app.
        }
        return [];
    }

}
