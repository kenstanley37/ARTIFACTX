using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ArtifactX.WinUI3.Models;

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

    /// <summary>The game account this folder belongs to, from its folder name
    /// (st_&lt;SteamID64&gt;, gog_&lt;id&gt;); null for Xbox and browsed folders.</summary>
    public string? AccountId { get; init; }

    /// <summary>Debug builds only: found in the developer test folder
    /// (SaveFolderDetectionService.DevTestRoot), not a real install.</summary>
    public bool IsDevTest { get; init; }

    /// <summary>Who plays on this account: the Steam name (Steam's own
    /// loginusers.vdf), else the player name in the newest save. Set while
    /// indexing (SaveFolderIndexingService), before the card is shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string? playerName;

    /// <summary>Steam's cached avatar for the account, if any.</summary>
    public string? AvatarPath { get; set; }

    public bool HasAvatar => AvatarPath is not null;

    public bool HasNoAvatar => AvatarPath is null;

    private Microsoft.UI.Xaml.Media.ImageSource? _avatarImage;

    /// <summary>Created on first read (by the card's binding, on the UI thread);
    /// candidates themselves are built on a background thread.</summary>
    public Microsoft.UI.Xaml.Media.ImageSource? AvatarImage =>
        AvatarPath is null ? null : _avatarImage ??= new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(AvatarPath));

    /// <summary>When the game last saved to any slot here (UTC); orders the
    /// cards. Before the slots are read, the save files' own dates.</summary>
    public DateTime LastPlayedUtc => SlotGroups.Count > 0 ? SlotGroups.Max(g => g.LastWriteUtc) : SaveFilesLastWriteUtc();

    private DateTime SaveFilesLastWriteUtc()
    {
        try
        {
            return System.IO.Directory.GetFiles(FolderPath, "save*.hg")
                .Select(System.IO.File.GetLastWriteTimeUtc)
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();
        }
        catch (Exception)
        {
            return DateTime.MinValue;
        }
    }

    /// <summary>Drives this candidate's own Expander card, independently of
    /// every other candidate - unlike the old single-selection model, several
    /// platforms can be open at once so a user with saves on more than one
    /// storefront can see all of them.</summary>
    [ObservableProperty]
    private bool isExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubText))]
    private bool isLoadingSlots;

    [ObservableProperty]
    private bool hasLoadedSlots;

    [ObservableProperty]
    private bool isEmptyResult;

    /// <summary>True when this candidate's FolderPath matches
    /// SaveSessionManager.ActivePlatformFolder - kept in sync by
    /// SaveFolderSelectPage (see RefreshActivePlatformHighlight), not computed
    /// here, since this model has no reference back to that service. Drives
    /// the Expander's highlighted border/badge on SaveFolderSelectPage.</summary>
    [ObservableProperty]
    private bool isActivePlatform;

    /// <summary>User-given label, persisted separately (see
    /// SaveFolderSettingsService) - pure display metadata, never renames the
    /// actual folder on disk. Any card can be renamed, so several accounts on
    /// one platform can be told apart.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string? customName;

    public ObservableCollection<SaveSlotGroup> SlotGroups { get; } = new();

    public bool CanRemove => Source == SaveFolderSource.Manual;

    public string PlatformName => Source switch
    {
        SaveFolderSource.Steam => "Steam",
        SaveFolderSource.Gog => "GOG",
        SaveFolderSource.MicrosoftStore => "Microsoft Store / Xbox",
        _ => "Custom Folder"
    };

    /// <summary>"Steam · Neophyte": platform plus who plays on it; a nickname
    /// the player gave the card replaces the name part.</summary>
    public string DisplayName
    {
        get
        {
            string? who = !string.IsNullOrWhiteSpace(CustomName) ? CustomName : PlayerName;
            if (Source == SaveFolderSource.Manual) return who ?? PlatformName;
            if (string.IsNullOrWhiteSpace(who) && AccountId is { Length: >= 4 } id) who = $"account …{id[^4..]}";
            return string.IsNullOrWhiteSpace(who) ? PlatformName : $"{PlatformName} · {who}";
        }
    }

    public string SubText
    {
        get
        {
            if (Source == SaveFolderSource.MicrosoftStore)
                return "Xbox save containers can't be auto-verified — select to use anyway";
            if (!IsValidated) return "No save*.hg files found here";
            var parts = new System.Collections.Generic.List<string>();
            if (IsLoadingSlots) parts.Add("Reading saves…");
            else if (SlotGroups.Count > 0) parts.Add(SlotGroups.Count == 1 ? "1 save" : $"{SlotGroups.Count} saves");
            else parts.Add($"{DetectedSaveFileCount} save file(s) found");
            if (LastPlayedUtc > DateTime.MinValue) parts.Add($"last played {Ago(LastPlayedUtc)}");
            if (AccountId is { Length: >= 4 } id && Source != SaveFolderSource.Manual) parts.Add($"ID …{id[^4..]}");
            if (IsDevTest) parts.Add("developer test copy");
            return string.Join(" · ", parts);
        }
    }

    private static string Ago(DateTime utc)
    {
        var span = DateTime.UtcNow - utc;
        if (span.TotalMinutes < 2) return "just now";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} minutes ago";
        if (span.TotalDays < 1) return span.TotalHours < 2 ? "an hour ago" : $"{(int)span.TotalHours} hours ago";
        if (span.TotalDays < 2) return "yesterday";
        if (span.TotalDays < 60) return $"{(int)span.TotalDays} days ago";
        return utc.ToLocalTime().ToString("MMM d, yyyy", System.Globalization.CultureInfo.CurrentCulture);
    }

    /// <summary>Called once the slots are indexed, so SubText's counts and
    /// dates refresh on the card.</summary>
    public void NotifySlotsChanged() => OnPropertyChanged(nameof(SubText));

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