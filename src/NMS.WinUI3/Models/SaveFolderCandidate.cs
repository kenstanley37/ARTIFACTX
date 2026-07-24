using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace NMS.WinUI3.Models;

public enum SaveFolderSource
{
    Steam,
    Gog,
    MicrosoftStore,
    Manual
}

public sealed partial class SaveFolderCandidate : ObservableObject, IEquatable<SaveFolderCandidate>
{
    public required string FolderPath { get; init; }
    public required SaveFolderSource Source { get; init; }
    public int DetectedSaveFileCount { get; init; }
    public bool IsValidated { get; init; }

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private bool isLoadingSlots;

    [ObservableProperty]
    private bool hasLoadedSlots;

    [ObservableProperty]
    private bool isEmptyResult;

    public ObservableCollection<SaveSlotGroup> SlotGroups { get; } = new();

    public bool CanRemove => Source == SaveFolderSource.Manual;

    public string DisplayName => Source switch
    {
        SaveFolderSource.Steam => "Steam",
        SaveFolderSource.Gog => "GOG",
        SaveFolderSource.MicrosoftStore => "Microsoft Store / Xbox",
        _ => "Custom Folder"
    };

    public string SubText => Source == SaveFolderSource.MicrosoftStore
        ? "Xbox save containers can't be auto-verified — select to use anyway"
        : IsValidated
            ? $"{DetectedSaveFileCount} save file(s) found"
            : "No save*.hg files found here";

    /// <summary>
    /// Stable, collision-proof folder name for this candidate's decrypted working
    /// copies (e.g. "Steam_4F2A9B1C"). Deterministic from Source + FolderPath, so
    /// re-detecting the same install always lands in the same working folder.
    /// </summary>
    public string WorkingFolderKey
    {
        get
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(FolderPath.ToUpperInvariant()));
            return $"{Source}_{Convert.ToHexString(hash)[..8]}";
        }
    }

    public bool Equals(SaveFolderCandidate? other) =>
        other is not null && string.Equals(FolderPath, other.FolderPath, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as SaveFolderCandidate);
    public override int GetHashCode() => FolderPath.ToUpperInvariant().GetHashCode();
}