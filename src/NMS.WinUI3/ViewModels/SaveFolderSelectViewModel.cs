using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NMS.WinUI3.Models;
using NMS.WinUI3.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NMS.WinUI3.ViewModels;

public partial class SaveFolderSelectViewModel : ObservableObject
{
    public ObservableCollection<SaveFolderCandidate> Candidates { get; } = new();

    [ObservableProperty]
    private SaveFolderCandidate? selectedCandidate;

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
            // parallel, before anything is shown. Selecting a folder afterward
            // never touches disk again.
            await Task.WhenAll(allCandidates.Select(LoadSlotGroupsAsync));

            Candidates.Clear();
            foreach (var candidate in allCandidates)
                Candidates.Add(candidate);

            if (Candidates.Count == 0)
            {
                StatusMessage = "No save folders were detected automatically. Use \"Browse for folder\" to add yours.";
                return;
            }

            string? lastSelected = SaveFolderSettingsService.GetSelectedFolder();
            if (lastSelected is not null)
            {
                SelectedCandidate = Candidates.FirstOrDefault(c =>
                    string.Equals(c.FolderPath, lastSelected, StringComparison.OrdinalIgnoreCase));
            }
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
            IsValidated = true
        };

        if (!Candidates.Contains(candidate))
            Candidates.Add(candidate);

        var stored = Candidates.First(c => c.Equals(candidate));

        SaveFolderSettingsService.AddCustomFolder(folderPath);
        StatusMessage = null;

        if (!stored.HasLoadedSlots)
            await LoadSlotGroupsAsync(stored);

        SelectedCandidate = stored;
        return true;
    }

    [RelayCommand]
    private void RemoveCandidate(SaveFolderCandidate candidate)
    {
        if (!candidate.CanRemove) return;

        Candidates.Remove(candidate);
        SaveFolderSettingsService.RemoveCustomFolder(candidate.FolderPath);

        if (ReferenceEquals(SelectedCandidate, candidate))
            SelectedCandidate = Candidates.FirstOrDefault();
    }

    partial void OnSelectedCandidateChanged(SaveFolderCandidate? value)
    {
        // Pure UI state flip — every candidate was already indexed in InitializeAsync
        // (or TryAddCustomFolderAsync), so this never touches disk.
        foreach (var candidate in Candidates)
            candidate.IsSelected = candidate.Equals(value);

        if (value is not null)
            SaveFolderSettingsService.SetSelectedFolder(value.FolderPath);
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