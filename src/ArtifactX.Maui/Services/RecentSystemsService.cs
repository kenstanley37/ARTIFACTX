using System.Collections.ObjectModel;
using ArtifactX.Core.Universe;
using ArtifactX.Maui.Models;

namespace ArtifactX.Maui.Services;

/// <summary>
/// Maintains the player's recently decoded portal systems across the Home and Systems tabs.
/// </summary>
public static class RecentSystemsService
{
    public static ObservableCollection<RecentSystemSummary> Items { get; } = [];

    public static void Record(PortalSystemReadout readout)
    {
        var summary = new RecentSystemSummary(
            readout.SystemName,
            $"{readout.CompactSummary} · {readout.GalaxyName}",
            readout.PortalCode,
            readout.GalaxyName,
            readout.GalaxyNumber);

        for (int i = Items.Count - 1; i >= 0; i--)
        {
            if (Items[i].GalaxyNumber == summary.GalaxyNumber &&
                string.Equals(Items[i].PortalCode[1..], summary.PortalCode[1..], StringComparison.OrdinalIgnoreCase))
            {
                Items.RemoveAt(i);
            }
        }

        Items.Insert(0, summary);
        while (Items.Count > 10)
            Items.RemoveAt(Items.Count - 1);
    }

}
