using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArtifactX.WinUI3.Models;
using ArtifactX.WinUI3.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.ViewModels;

public partial class SaveFolderSelectViewModel : ObservableObject
{
    public ObservableCollection<SaveFolderCandidate> Candidates { get; } = new();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? statusMessage;

    // Survives across page navigations even though a brand-new ViewModel is
    // constructed on every visit (Frame.Navigate, no NavigationCacheMode
    // anywhere in this app - same pattern documented throughout this
    // project's history). SaveFolderIndexingService's own doc comment
    // already says candidates should be "decrypted and indexed exactly
    // once... per app session" - that intent just wasn't actually enforced
    // here, so every single nav back to Save Selection was silently
    // redoing the full detect+decrypt+index pass against every candidate's
    // save files (2026-08-07 user report). Invalidated only when it can
    // actually go stale - NMS closing, since that's the only time save
    // files change out from under the app - via the static hookup below.
    private static List<SaveFolderCandidate>? _cachedCandidates;

    static SaveFolderSelectViewModel()
    {
        GameProcessMonitorService.RunningStateChanged += (_, isRunning) =>
        {
            if (!isRunning) _cachedCandidates = null;
        };
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (_cachedCandidates is not null)
        {
            Candidates.Clear();
            foreach (var candidate in _cachedCandidates)
            {
                candidate.PropertyChanged += OnCandidatePropertyChanged;
                Candidates.Add(candidate);
            }
            return;
        }

        IsBusy = true;
        StatusMessage = null;

        try
        {
            var detected = await Task.Run(SaveFolderDetectionService.DetectCandidates);
            var customCandidates = PruneAndLoadCustomFolders();

            var allCandidates = detected
                .Concat(customCandidates)
                .DistinctBy(c => c.FolderPath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Every candidate gets decrypted and indexed exactly once, here, in
            // parallel, before anything is shown. Expanding a card afterward
            // never touches disk again - and, as of this cache, neither does
            // navigating away from and back to this page within the same
            // NMS session.
            await Task.WhenAll(allCandidates.Select(LoadSlotGroupsAsync));

            // Null (never persisted - first launch ever) defaults every card open,
            // so a brand-new user immediately sees their saves instead of a wall
            // of collapsed headers. Once the user has expanded/collapsed anything,
            // GetExpandedFolders() returns the real (possibly empty) set instead
            // of null, and that's respected exactly as left.
            var expandedPaths = SaveFolderSettingsService.GetExpandedFolders();

            Candidates.Clear();
            foreach (var candidate in allCandidates)
            {
                candidate.IsExpanded = expandedPaths is null
                    || expandedPaths.Contains(candidate.FolderPath, StringComparer.OrdinalIgnoreCase);
                candidate.CustomName = SaveFolderSettingsService.GetCustomFolderName(candidate.FolderPath);
                candidate.PropertyChanged += OnCandidatePropertyChanged;
                Candidates.Add(candidate);
            }

            // Accordion behavior (see OnCandidatePropertyChanged) only kicks in on
            // a user expanding a NEW card from here on - normalize any startup
            // state that predates that (settings persisted before this became
            // single-selection, or first-launch-ever defaulting every card open)
            // down to just the first expanded one, rather than showing several
            // open until the user happens to click something.
            SaveFolderCandidate? keptOpen = null;
            foreach (var candidate in Candidates)
            {
                if (candidate.IsExpanded && keptOpen is null) keptOpen = candidate;
                else candidate.IsExpanded = false;
            }

            // The candidate above was set IsExpanded=true via direct assignment
            // BEFORE its PropertyChanged subscription existed (both happen in the
            // loop two levels up), so OnCandidatePropertyChanged's SetActivePlatform
            // call never ran for it - without this, a card restored as already-open
            // from a prior session LOOKS active (highlighted, expanded) but
            // SaveSessionManager doesn't actually know about it until the user
            // manually toggles something (bug report 2026-08-05: "I have to click
            // Custom Folder and then back to Steam to get it to show").
            if (keptOpen is not null)
                SaveSessionManager.SetActivePlatform(keptOpen.FolderPath, keptOpen.DisplayName);

            if (Candidates.Count == 0)
                StatusMessage = "No save folders were detected automatically. Use \"Browse for folder\" to add yours.";

            _cachedCandidates = allCandidates;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Unsubscribes this instance's handler from every candidate it
    /// currently tracks - required because candidates now potentially
    /// outlive this ViewModel (see _cachedCandidates above). Without this,
    /// every past page visit would leave its OnCandidatePropertyChanged
    /// still attached to the shared, cached candidate objects, the same
    /// class of leak documented in project_static_event_leak_fix - each
    /// stale handler keeps that whole old ViewModel/Page instance alive and
    /// fires redundantly on every future IsExpanded change. Called from the
    /// View's Unloaded handler.</summary>
    public void DetachCandidateHandlers()
    {
        foreach (var candidate in Candidates)
            candidate.PropertyChanged -= OnCandidatePropertyChanged;
    }

    /// <summary>
    /// Drops any manually-added folder that's been deleted, or no longer contains
    /// save*.hg files, and quietly persists the pruned list — the user removed it
    /// themselves, either directly or by emptying the folder, so there's nothing
    /// to surface here.
    /// </summary>
    private static List<SaveFolderCandidate> PruneAndLoadCustomFolders()
    {
        var storedPaths = SaveFolderSettingsService.GetCustomFolders();
        var survivingPaths = new List<string>();
        var candidates = new List<SaveFolderCandidate>();

        foreach (var path in storedPaths)
        {
            if (!Directory.Exists(path)) continue;
            if (SaveFolderDetectionService.CountSaveFiles(path) == 0) continue;

            survivingPaths.Add(path);
            candidates.Add(SaveFolderDetectionService.BuildCandidate(path, SaveFolderSource.Manual));
        }

        if (survivingPaths.Count != storedPaths.Count)
            SaveFolderSettingsService.SetCustomFolders(survivingPaths);

        return candidates;
    }

    /// <summary>Called by the View after the folder picker returns a path — the one
    /// place indexing legitimately happens outside startup, since a brand-new
    /// folder has never been scanned.</summary>
    public async Task<bool> TryAddCustomFolderAsync(string folderPath)
    {
        int count = SaveFolderDetectionService.CountSaveFiles(folderPath);
        if (count == 0)
        {
            StatusMessage = "That folder doesn't contain any No Man's Sky save files (save*.hg).";
            return false;
        }

        var candidate = new SaveFolderCandidate
        {
            FolderPath = folderPath,
            Source = SaveFolderSource.Manual,
            DetectedSaveFileCount = count,
            IsValidated = true,
        };

        if (!Candidates.Contains(candidate))
        {
            candidate.PropertyChanged += OnCandidatePropertyChanged;
            Candidates.Add(candidate);

            // Keep the session cache in sync, or a newly-browsed folder
            // would vanish the next time this page loads from cache instead
            // of re-scanning the filesystem.
            (_cachedCandidates ??= new()).Add(candidate);
        }

        var stored = Candidates.First(c => c.Equals(candidate));
        stored.CustomName = SaveFolderSettingsService.GetCustomFolderName(folderPath);

        SaveFolderSettingsService.AddCustomFolder(folderPath);
        StatusMessage = null;

        // Opens immediately so its slots are visible right away - set AFTER
        // subscribing to PropertyChanged (above) so OnCandidatePropertyChanged's
        // accordion logic collapses any other currently-open card the normal way.
        stored.IsExpanded = true;

        if (!stored.HasLoadedSlots)
            await LoadSlotGroupsAsync(stored);

        PersistExpandedFolders();
        return true;
    }

    [RelayCommand]
    private void RemoveCandidate(SaveFolderCandidate candidate)
    {
        if (!candidate.CanRemove) return;

        candidate.PropertyChanged -= OnCandidatePropertyChanged;
        Candidates.Remove(candidate);
        _cachedCandidates?.Remove(candidate);
        SaveFolderSettingsService.RemoveCustomFolder(candidate.FolderPath);
        PersistExpandedFolders();
    }

    /// <summary>Pure display metadata - renames the card, never the folder on
    /// disk. An empty/whitespace name clears back to the default label.</summary>
    public void RenameCandidate(SaveFolderCandidate candidate, string? newName)
    {
        candidate.CustomName = newName;
        SaveFolderSettingsService.SetCustomFolderName(candidate.FolderPath, newName);
    }

    private void OnCandidatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SaveFolderCandidate.IsExpanded)) return;

        // Accordion behavior: opening one platform card closes every other -
        // only one is realistically "the one you're working in" at a time
        // (user feedback 2026-08-04). Also treats "opened" as "selected" for
        // account-wide pages (Account Data) that only need a platform chosen,
        // not a specific save slot loaded - see SaveSessionManager.SetActivePlatform.
        // Guarded on IsExpanded actually being true so collapsing a card
        // (setting it false, including the ones this loop itself collapses)
        // doesn't recurse back into this branch.
        if (sender is SaveFolderCandidate expanded && expanded.IsExpanded)
        {
            foreach (var other in Candidates)
            {
                if (!ReferenceEquals(other, expanded) && other.IsExpanded)
                    other.IsExpanded = false;
            }

            SaveSessionManager.SetActivePlatform(expanded.FolderPath, expanded.DisplayName);
        }

        PersistExpandedFolders();
    }

    private void PersistExpandedFolders()
    {
        var expanded = Candidates.Where(c => c.IsExpanded).Select(c => c.FolderPath).ToList();
        SaveFolderSettingsService.SetExpandedFolders(expanded);
    }

    private static async Task LoadSlotGroupsAsync(SaveFolderCandidate candidate)
    {
        candidate.IsLoadingSlots = true;

        try
        {
            var groups = await SaveFolderIndexingService.IndexAsync(candidate);

            candidate.SlotGroups.Clear();
            foreach (var group in groups)
                candidate.SlotGroups.Add(group);

            candidate.IsEmptyResult = candidate.SlotGroups.Count == 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SaveFolderSelectViewModel] Failed to index {candidate.FolderPath}: {ex.Message}");
            candidate.IsEmptyResult = true;
        }
        finally
        {
            candidate.IsLoadingSlots = false;
            candidate.HasLoadedSlots = true;
        }
    }
}