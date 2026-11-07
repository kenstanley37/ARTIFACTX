using Microsoft.UI.Xaml;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace ArtifactX.WinUI3.Services;

/// <summary>Connects an initialized Cargo control to the existing loaded-slot
/// feed. No file watcher or reads of unopened inventories. Staged container
/// edits take precedence over the game's newer snapshot.</summary>
public sealed class CargoStorageFeed
{
    private readonly FrameworkElement _owner;
    private readonly Func<IEnumerable<string[]>> _paths;
    private readonly Action _refresh;
    private bool _attached;

    public CargoStorageFeed(FrameworkElement owner, Func<IEnumerable<string[]>> paths, Action refresh)
    {
        _owner = owner;
        _paths = paths;
        _refresh = refresh;
        owner.Loaded += (_, _) => Attach();
        owner.Unloaded += (_, _) => Detach();
    }

    private void Attach()
    {
        if (_attached) return;
        _attached = true;
        SaveSessionManager.ActiveSessionChanged += OnChanged;
        SaveSessionManager.PendingEditsChanged += OnChanged;
        SaveSessionManager.GameSavedLoadedSlot += OnGameSaved;
        _refresh();
    }

    private void Detach()
    {
        if (!_attached) return;
        _attached = false;
        SaveSessionManager.ActiveSessionChanged -= OnChanged;
        SaveSessionManager.PendingEditsChanged -= OnChanged;
        SaveSessionManager.GameSavedLoadedSlot -= OnGameSaved;
    }

    private void OnChanged(object? sender, EventArgs e) => _owner.DispatcherQueue.TryEnqueue(() =>
    {
        if (_attached) _refresh();
    });

    private void OnGameSaved(JObject disk)
    {
        object? session = SaveSessionManager.SessionIdentity;
        _owner.DispatcherQueue.TryEnqueue(() =>
        {
            if (!_attached || !ReferenceEquals(session, SaveSessionManager.SessionIdentity)
                || SaveSessionManager.IsMidExpedition(disk) != SaveSessionManager.IsExpeditionActive) return;
            foreach (var path in _paths()) SaveSessionManager.AdoptFromGame(disk, path);
            _refresh();
        });
    }
}
