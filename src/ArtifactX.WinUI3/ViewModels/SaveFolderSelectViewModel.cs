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

    [RelayCommand]
    private async Task InitializeAsync()
    {
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
            // never touches disk again.
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
            bool keptOneOpen = false;
            foreach (var candidate in Candidates)
            {
                if (candidate.IsExpanded && !keptOneOpen) keptOneOpen = true;
                else candidate.IsExpanded = false;
            }

            if (Candidates.Count == 0)
                StatusMessage = "No save folders were detected automatically. Use \"Browse for folder\" to add yours.";
        }
        finally
        {
            IsBusy = false;
        }
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

            SaveSessionManager.SetActivePlatform(expanded.FolderPath);
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