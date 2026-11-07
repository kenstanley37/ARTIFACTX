using ArtifactX.WinUI3.Models;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Keeps Save Selection up to date while ArtifactX is open (user request
/// 2026-10-13: a save started in game only showed after restarting the app).
/// Watches every account folder Save Selection found; when the game writes a
/// slot's files, only that slot is re-read (SaveFolderIndexingService.IndexSlotAsync)
/// and its card replaced in place - a new slot appears, a deleted one goes,
/// names and play times stay current.
///
/// The loaded slot's card is swapped for the re-read one too
/// (SaveSessionManager.AdoptSlot), so a slot that gains its second file while
/// open is written to both on the next save - the session itself (and any
/// unsaved edits) is untouched.
///
/// Writes arrive in bursts (the game writes both files of a slot, and its
/// manifest), so changes are gathered for a few seconds before reading; a file
/// caught mid-write is tried again once, then the old card is kept.
/// </summary>
public static class SaveFolderWatchService
{
    private static readonly object Lock = new();
    private static readonly List<(FileSystemWatcher Watcher, SaveFolderCandidate Candidate)> Watchers = new();
    private static readonly HashSet<(SaveFolderCandidate Candidate, int SlotId)> Pending = new();
    private static Timer? _debounce;
    private static DispatcherQueue? _dispatcher;

    private static readonly TimeSpan Quiet = TimeSpan.FromSeconds(3);

    /// <summary>Raised (UI thread) when the running game saves a different slot
    /// of the loaded account than the one ArtifactX has loaded - the player
    /// switched saves in game (MainWindow offers to follow).</summary>
    public static event Action<SaveSlotGroup>? GameSavedOtherSlot;

    /// <summary>Watches these account folders, replacing whatever was watched before.</summary>
    public static void Watch(IEnumerable<SaveFolderCandidate> candidates, DispatcherQueue dispatcher)
    {
        lock (Lock)
        {
            _dispatcher = dispatcher;
            foreach (var (w, _) in Watchers) Release(w);
            Watchers.Clear();
            Pending.Clear();
        }
        foreach (var candidate in candidates) Add(candidate);
    }

    /// <summary>Adds one account folder (e.g. one the player just browsed to).</summary>
    public static void Add(SaveFolderCandidate candidate)
    {
        if (!Directory.Exists(candidate.FolderPath)) return;
        lock (Lock)
        {
            if (Watchers.Any(x => string.Equals(x.Candidate.FolderPath, candidate.FolderPath, StringComparison.OrdinalIgnoreCase))) return;
            try
            {
                var watcher = new FileSystemWatcher(candidate.FolderPath, "save*.hg")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                };
                FileSystemEventHandler changed = (_, e) => OnChanged(candidate, e.FullPath);
                RenamedEventHandler renamed = (_, e) => { OnChanged(candidate, e.OldFullPath); OnChanged(candidate, e.FullPath); };
                watcher.Changed += changed;
                watcher.Created += changed;
                watcher.Deleted += changed;
                watcher.Renamed += renamed;
                watcher.EnableRaisingEvents = true;
                Watchers.Add((watcher, candidate));
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"[SaveFolderWatchService] Can't watch {candidate.FolderPath}: {ex.Message}");
            }
        }
    }

    private static void Release(FileSystemWatcher watcher)
    {
        watcher.EnableRaisingEvents = false;
        watcher.Dispose();
    }

    private static void OnChanged(SaveFolderCandidate candidate, string path)
    {
        if (!SaveFolderIndexingService.IsSaveFile(path)) return;
        lock (Lock)
        {
            Pending.Add((candidate, SaveFolderIndexingService.SlotIdFor(path)));
            _debounce ??= new Timer(_ => _ = FlushAsync());
            _debounce.Change(Quiet, Timeout.InfiniteTimeSpan);
        }
    }

    private static async Task FlushAsync()
    {
        List<(SaveFolderCandidate Candidate, int SlotId)> work;
        DispatcherQueue? dispatcher;
        lock (Lock)
        {
            work = Pending.ToList();
            Pending.Clear();
            dispatcher = _dispatcher;
        }
        if (dispatcher is null) return;

        foreach (var (candidate, slotId) in work)
        {
            SaveSlotGroup? group;
            try
            {
                group = await SaveFolderIndexingService.IndexSlotAsync(candidate, slotId);
                if (group is not null && group.Files.Any(f => !f.IsReadable))
                {
                    await Task.Delay(Quiet); // probably caught mid-write
                    group = await SaveFolderIndexingService.IndexSlotAsync(candidate, slotId);
                    if (group is not null && group.Files.Any(f => !f.IsReadable)) continue; // keep the old card
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SaveFolderWatchService] Couldn't re-read slot {slotId} of {candidate.FolderPath}: {ex.Message}");
                continue;
            }
            dispatcher.TryEnqueue(() => Apply(candidate, slotId, group));
        }
    }

    /// <summary>Puts the re-read slot on its account's card (UI thread).</summary>
    private static void Apply(SaveFolderCandidate candidate, int slotId, SaveSlotGroup? group)
    {
        var slots = candidate.SlotGroups;
        int at = -1;
        for (int i = 0; i < slots.Count; i++)
            if (slots[i].SlotId == slotId) { at = i; break; }
        var old = at >= 0 ? slots[at] : null;

        if (group is null)
        {
            // The loaded slot keeps its card even if its files vanish, so the
            // session still has somewhere to point.
            if (old is not null && !old.IsActive) slots.RemoveAt(at);
        }
        else
        {
            if (old is not null)
            {
                bool wasActive = old.IsActive;
                slots[at] = group;
                if (wasActive)
                {
                    SaveSessionManager.AdoptSlot(group);
                    _ = SaveSessionManager.NotifyGameSavedAsync(group);
                }
            }
            else
            {
                int insert = 0;
                while (insert < slots.Count && slots[insert].SlotId < slotId) insert++;
                slots.Insert(insert, group);
            }
        }

        candidate.IsEmptyResult = slots.Count == 0;
        candidate.NotifySlotsChanged();
        var latest = slots.OrderByDescending(g => g.LastWriteUtc).FirstOrDefault();
        foreach (var g in slots) g.IsLatest = ReferenceEquals(g, latest);

        // Only while the game runs: ArtifactX itself only writes the loaded
        // slot, and restoring a backup needs the game closed, so a write to
        // another slot then means the game is playing it.
        if (group is not null && ReferenceEquals(group, latest) && GameProcessMonitorService.IsGameRunning
            && SaveSessionManager.ActiveSlot is { } active && active.SlotId != slotId
            && string.Equals(Path.TrimEndingDirectorySeparator(candidate.FolderPath), SaveSessionManager.ActivePlatformFolder is { } f ? Path.TrimEndingDirectorySeparator(f) : null, StringComparison.OrdinalIgnoreCase))
            GameSavedOtherSlot?.Invoke(group);
    }
}
