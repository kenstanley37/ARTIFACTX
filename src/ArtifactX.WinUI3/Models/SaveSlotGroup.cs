using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Models;

public sealed partial class SaveSlotGroup : ObservableObject
{
    public required int SlotId { get; init; }
    public required IReadOnlyList<SaveFileEntry> Files { get; init; }
    public required string SourceDisplayName { get; init; }

    [ObservableProperty]
    private bool isActive;

    public string SlotLabel => $"Slot {SlotId}";
    public string ActiveLabel => $"{SourceDisplayName} - {SlotLabel}";

    /// <summary>The file the game would actually load for this slot - same
    /// "most recently written" rule SaveSessionManager.LoadAsync uses. The
    /// other file(s), when present, are just Hello Games' own rollback
    /// backups, not independently meaningful save points.</summary>
    public SaveFileEntry PrimaryFile => Files.OrderByDescending(f => f.LastModified).First();

    public bool HasBackupCopy => Files.Count > 1;
}