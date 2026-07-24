using NMS.Core;
using NMS.WinUI3.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NMS.WinUI3.Services;

/// <summary>
/// App-wide holder for the one save slot currently open for editing. Loading
/// parses one representative file (the pair's most recently modified one) once,
/// off the UI thread - after that, every page's reads and edits are pure
/// in-memory operations against the shared JSON tree, so switching pages never
/// touches disk. Committing writes both files in the slot and is likewise run
/// off the UI thread regardless of save size.
/// </summary>
public static class SaveSessionManager
{
    private static SaveEditSession? _session;
    private static string[] _targetHgPaths = Array.Empty<string>();
    private static SaveSlotGroup? _activeSlot;

    public static event EventHandler? ActiveSessionChanged;
    public static event EventHandler? PendingEditsChanged;

    public static bool IsSaveLoaded => _session is not null;
    public static bool HasUnsavedChanges => _session?.HasUnsavedChanges ?? false;
    public static SaveSlotGroup? ActiveSlot => _activeSlot;
    public static string? ActiveLabel => _activeSlot?.ActiveLabel;
    public static bool HasStagedEdit(params string[] path) => _session?.HasStagedEdit(path) ?? false;

    public static async Task LoadAsync(SaveSlotGroup slot)
    {
        DetachCurrent();
        if (_activeSlot is not null)
            _activeSlot.IsActive = false;

        var primary = slot.Files.OrderByDescending(f => f.LastModified).First();

        _session = await Task.Run(() => SaveEditSession.LoadAsync(primary.WorkingJsonPath));
        _targetHgPaths = slot.Files.Select(f => f.FullPath).ToArray();
        _activeSlot = slot;
        _activeSlot.IsActive = true;

        _session.EditsChanged += OnEditsChanged;
        ActiveSessionChanged?.Invoke(null, EventArgs.Empty);
    }

    public static void CloseSession()
    {
        DetachCurrent();
        if (_activeSlot is not null)
            _activeSlot.IsActive = false;

        _session = null;
        _targetHgPaths = Array.Empty<string>();
        _activeSlot = null;
        ActiveSessionChanged?.Invoke(null, EventArgs.Empty);
    }

    public static JToken? GetValue(params string[] path) => _session?.GetValue(path);
    public static long? GetLong(params string[] path) => _session?.GetLong(path);
    public static string? GetString(params string[] path) => _session?.GetString(path);
    public static bool? GetBool(params string[] path) => _session?.GetBool(path);

    public static void StageEdit(JToken? newValue, params string[] path) => _session?.StageEdit(newValue, path);
    public static void RevertEdit(params string[] path) => _session?.RevertEdit(path);
    public static void DiscardAllEdits() => _session?.DiscardAllEdits();

    public static async Task CommitAsync()
    {
        if (_session is null || _targetHgPaths.Length == 0) return;
        await Task.Run(() => _session.CommitAsync(_targetHgPaths));
    }

    private static void DetachCurrent()
    {
        if (_session is not null)
            _session.EditsChanged -= OnEditsChanged;
    }

    private static void OnEditsChanged(object? sender, EventArgs e) =>
        PendingEditsChanged?.Invoke(null, EventArgs.Empty);
}