using ArtifactX.WinUI3.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>Retains a storage editor across tab switches. Unrelated pending
/// edits and unchanged save notifications do not recreate its inventory.</summary>
public sealed class CargoInventoryViewModel
{
    private readonly string[] _path;
    private object? _session;
    private JToken? _snapshot;
    private int _version;

    public InventoryGridViewModel Inventory { get; }
    public string[] Path => _path;
    public bool HasChanges => SaveSessionManager.HasStagedEditsUnder(_path);

    public CargoInventoryViewModel(string[] path, int columns, int rows)
    {
        _path = path;
        Inventory = new(path, columns, rows);
    }

    public async Task<bool> RefreshAsync(Action? inventoryReady = null)
    {
        // Compare only the fields used by InventoryGridViewModel.Load;
        // renames and unrelated container metadata do not redraw the grid.
        var snapshot = new JArray(new[] { ":No", "hl?", "MMm", "B@N" }
            .Select(key => SaveSessionManager.GetValue(_path.Append(key).ToArray())?.DeepClone()));
        object? session = SaveSessionManager.SessionIdentity;
        if (ReferenceEquals(_session, session) && JToken.DeepEquals(_snapshot, snapshot)) return false;
        _session = session;
        _snapshot = snapshot;
        int version = ++_version;
        Inventory.Load();
        inventoryReady?.Invoke();
        await CatalogService.WarmCacheAsync(Inventory.Cells.Where(c => c.IsOccupied).Select(c => c.ItemId).ToArray());
        return version == _version && ReferenceEquals(session, SaveSessionManager.SessionIdentity);
    }

    public void Revert() => SaveSessionManager.RevertEditsUnder(_path);
}
