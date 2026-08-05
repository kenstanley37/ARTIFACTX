using ArtifactX.Core;
using ArtifactX.WinUI3.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

public static class SaveSessionManager
{
    private static SaveEditSession? _session;
    private static string[] _targetHgPaths = Array.Empty<string>();
    private static SaveSlotGroup? _activeSlot;
    private static string? _activePlatformFolder;
    private static string? _activePlatformDisplayName;
    private static Dictionary<string, DateTime> _loadSnapshot = new();

    public static event EventHandler? ActiveSessionChanged;
    public static event EventHandler? PendingEditsChanged;

    /// <summary>Fires when ArtifactX.exe closes and the on-disk save files have
    /// changed since this session was loaded - meaning continuing to edit
    /// (and eventually save) risks overwriting real in-game progress.</summary>
    public static event EventHandler? ExternalChangeDetected;

    public static bool IsSaveLoaded => _session is not null;
    public static bool HasUnsavedChanges => _session?.HasUnsavedChanges ?? false;
    public static int PendingEditCount => _session?.PendingEditCount ?? 0;
    public static IEnumerable<string> DescribePendingEdits() => _session?.DescribePendingEdits() ?? Enumerable.Empty<string>();
    public static SaveSlotGroup? ActiveSlot => _activeSlot;
    public static string? ActiveLabel => _activeSlot?.ActiveLabel;

    /// <summary>The platform-account folder (e.g. "...\Steam\st_76561...") the
    /// user is currently working within - set either implicitly by loading a
    /// slot (LoadAsync below) or explicitly via SetActivePlatform when the
    /// user opens a platform card on SaveFolderSelectPage without picking a
    /// specific slot yet. Exists so account-wide pages (AccountDataPage, via
    /// AccountSessionManager.ResolveAccountDataPath) can work off just a
    /// platform choice - accountdata.hg lives once per platform folder, not
    /// per save slot, so it shouldn't require picking a slot first.</summary>
    public static string? ActivePlatformFolder => _activePlatformFolder;
    public static bool HasActivePlatform => _activePlatformFolder is not null;

    /// <summary>Human-readable form of ActivePlatformFolder (e.g. "Steam",
    /// "Custom Folder", or a user-given custom name) - kept alongside the raw
    /// path since nothing else derives a friendly label from a bare folder
    /// path. Used by AppTitleBar and AccountDataPage so the user can tell
    /// which platform account they're looking at even when no save slot is
    /// loaded (SaveSlotGroup.SourceDisplayName already carries the same
    /// value once a slot IS loaded, via LoadAsync below).</summary>
    public static string? ActivePlatformDisplayName => _activePlatformDisplayName;

    public static async Task LoadAsync(SaveSlotGroup slot)
    {
        DetachCurrent();
        if (_activeSlot is not null)
            _activeSlot.IsActive = false;

        var primary = slot.PrimaryFile;

        // Re-extract fresh from the real .hg file on disk rather than trusting
        // whatever working copy IndexAsync wrote during the app's initial folder
        // scan. Without this, re-loading the same slot later in the same app
        // session (e.g. after making a change in-game and re-saving) silently
        // shows stale data - nothing else ever re-decrypts that working JSON,
        // which is exactly why its timestamp never moved no matter how many
        // times the slot list was reopened.
        string freshJson = await SaveStreamProcessor.ExtractRawJsonAsync(primary.FullPath);
        await File.WriteAllTextAsync(primary.WorkingJsonPath, freshJson);

        _session = await Task.Run(() => SaveEditSession.LoadAsync(primary.WorkingJsonPath));
        _targetHgPaths = slot.Files.Select(f => f.FullPath).ToArray();
        _activeSlot = slot;
        _activeSlot.IsActive = true;
        _activePlatformFolder = Path.GetDirectoryName(primary.FullPath);
        _activePlatformDisplayName = slot.SourceDisplayName;

        TakeSnapshot();

        _session.EditsChanged += OnEditsChanged;
        ActiveSessionChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Marks a platform folder as the active one without loading any
    /// specific save slot - called when the user opens a platform card on
    /// SaveFolderSelectPage (see SaveFolderSelectPage.xaml's Expander.Expanding
    /// handler). Deliberately reuses ActiveSessionChanged rather than a new
    /// event: every page that reacts to it already handles a "no slot loaded"
    /// state gracefully (that's exactly what CloseSession also produces), so
    /// this needs no new wiring - MainWindow's nav-visibility check and
    /// AccountDataPage's own reload both already do the right thing.</summary>
    public static void SetActivePlatform(string folderPath, string displayName)
    {
        if (string.Equals(_activePlatformFolder, folderPath, StringComparison.OrdinalIgnoreCase)) return;

        _activePlatformFolder = folderPath;
        _activePlatformDisplayName = displayName;
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
        _loadSnapshot.Clear();
        ActiveSessionChanged?.Invoke(null, EventArgs.Empty);
    }

    public static JToken? GetValue(params string[] path) => _session?.GetValue(path);
    public static long? GetLong(params string[] path) => _session?.GetLong(path);
    public static string? GetString(params string[] path) => _session?.GetString(path);
    public static bool? GetBool(params string[] path) => _session?.GetBool(path);

    public static void StageEdit(JToken? newValue, params string[] path) => _session?.StageEdit(newValue, path);
    public static void RevertEdit(params string[] path) => _session?.RevertEdit(path);
    public static bool HasStagedEdit(params string[] path) => _session?.HasStagedEdit(path) ?? false;
    public static bool HasStagedEditsUnder(params string[] pathPrefix) => _session?.HasStagedEditsUnder(pathPrefix) ?? false;
    public static void RevertEditsUnder(params string[] pathPrefix) => _session?.RevertEditsUnder(pathPrefix);
    public static void DiscardAllEdits() => _session?.DiscardAllEdits();

    public static async Task CommitAsync()
    {
        if (_session is null || _targetHgPaths.Length == 0) return;

        // Backs up whatever's currently on disk before it gets overwritten -
        // every commit, not just the first, so the user always has a way
        // back to the state right before any given save (see BackupService).
        if (_activeSlot is not null && _activePlatformDisplayName is not null)
            BackupService.BackupFiles(_targetHgPaths, BackupService.BuildSlotGroupKey(_activePlatformDisplayName, _activeSlot.SlotId));

        await Task.Run(() => _session.CommitAsync(_targetHgPaths));
        TakeSnapshot();
    }

    /// <summary>Discards the current session and reloads fresh from disk -
    /// use after ExternalChangeDetected when the user chooses to reload
    /// rather than risk overwriting new in-game progress.</summary>
    public static async Task ReloadFromDiskAsync()
    {
        if (_activeSlot is null) return;
        var slot = _activeSlot;
        CloseSession();
        await LoadAsync(slot);
    }

    /// <summary>Called by the game-process monitor on the running->stopped
    /// transition. Compares each target file's current on-disk timestamp
    /// against what was recorded at load/commit time.</summary>
    public static void CheckForExternalChanges()
    {
        if (_session is null) return;

        foreach (var path in _targetHgPaths)
        {
            if (!File.Exists(path)) continue;
            var currentWriteTime = File.GetLastWriteTimeUtc(path);

            if (_loadSnapshot.TryGetValue(path, out var snapshotTime) && currentWriteTime != snapshotTime)
            {
                ExternalChangeDetected?.Invoke(null, EventArgs.Empty);
                return;
            }
        }
    }

    private static void TakeSnapshot()
    {
        _loadSnapshot = _targetHgPaths
            .Where(File.Exists)
            .ToDictionary(p => p, File.GetLastWriteTimeUtc);
    }

    private static void DetachCurrent()
    {
        if (_session is not null)
            _session.EditsChanged -= OnEditsChanged;
    }

    private static void OnEditsChanged(object? sender, EventArgs e) =>
        PendingEditsChanged?.Invoke(null, EventArgs.Empty);
}