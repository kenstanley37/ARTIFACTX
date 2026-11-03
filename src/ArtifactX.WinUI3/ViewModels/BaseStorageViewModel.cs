using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels;

public sealed partial class BaseStorageViewModel : ObservableObject
{
    private object? _session;
    private string _loadedName = "";
    private readonly Dictionary<string, CargoInventoryViewModel> _editors = new();
    public ObservableCollection<StorageContainerEntry> Containers { get; } = new();

    [ObservableProperty] public partial StorageContainerEntry? SelectedContainer { get; set; }
    [ObservableProperty] public partial string Name { get; set; } = "";

    public CargoInventoryViewModel? Editor { get; private set; }
    public bool HasChanges => Containers.Any(c => SaveSessionManager.HasStagedEditsUnder(NmsInventoryContainer.BaseStorageContainerPath(c.Key)));

    public void RefreshContainers()
    {
        bool newSession = !ReferenceEquals(_session, SaveSessionManager.SessionIdentity);
        if (newSession)
        {
            _session = SaveSessionManager.SessionIdentity;
            _editors.Clear();
        }
        var entries = BaseStorageService.DiscoverContainers();
        string? selected = newSession ? null : SelectedContainer?.Key;
        // Preserve unchanged selector objects and the selection on ordinary edits.
        for (int i = 0; i < entries.Count; i++)
        {
            var existing = Containers.FirstOrDefault(c => c.Key == entries[i].Key && c.Name == entries[i].Name);
            if (existing is null) Containers.Insert(i, entries[i]);
            else if (Containers.IndexOf(existing) != i) Containers.Move(Containers.IndexOf(existing), i);
        }
        while (Containers.Count > entries.Count) Containers.RemoveAt(Containers.Count - 1);
        SelectedContainer = Containers.FirstOrDefault(c => c.Key == selected) ?? Containers.FirstOrDefault();
        UpdateSelection();
        if (newSession) Name = SelectedContainer?.Name ?? "";
    }

    partial void OnSelectedContainerChanged(StorageContainerEntry? value) => UpdateSelection();

    private void UpdateSelection()
    {
        foreach (var container in Containers) container.IsSelected = container.Key == SelectedContainer?.Key;
    }

    public async Task<bool> LoadSelectedAsync()
    {
        var selected = SelectedContainer;
        if (selected is null) { Editor = null; return true; }
        if (!_editors.TryGetValue(selected.Key, out var editor))
        {
            editor = new(NmsInventoryContainer.BaseStorageContainerPath(selected.Key), BaseStorageCapacity.Columns, BaseStorageCapacity.Rows);
            _editors.Add(selected.Key, editor);
        }
        bool selectionChanged = !ReferenceEquals(Editor, editor);
        Editor = editor;
        if (selectionChanged || Name == _loadedName) Name = selected.Name;
        _loadedName = selected.Name;
        bool changed = await editor.RefreshAsync();
        return ReferenceEquals(Editor, editor) && (selectionChanged || changed);
    }

    public void Rename()
    {
        if (SelectedContainer is not { } selected) return;
        string name = Name.Trim();
        if (name.Length == 0 || name == selected.Name) return;
        BaseStorageService.Rename(selected.Key, name);
        Name = name;
        RefreshContainers();
    }

    public void Revert()
    {
        foreach (var container in Containers)
            SaveSessionManager.RevertEditsUnder(NmsInventoryContainer.BaseStorageContainerPath(container.Key));
        RefreshContainers();
        Name = SelectedContainer?.Name ?? "";
    }
}
