using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.IO;
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

    /// <summary>The file the game would actually load for this slot - the
    /// most recently written of NMS's own rotating pair (e.g. save.hg/
    /// save2.hg - it alternates which one it writes to on every in-game
    /// save, as its own corruption-safety measure). Re-stats both files live
    /// on disk on every access rather than trusting each SaveFileEntry's own
    /// LastModified, which is a one-time snapshot from
    /// SaveFolderIndexingService's startup scan ("indexed exactly once...
    /// expanding a card afterward never touches disk again") - without a
    /// live stat here, playing and saving in-game after ArtifactX has
    /// already indexed the folder silently picks the now-stale file back up
    /// on load, even though its JSON content gets freshly re-read (see
    /// SaveSessionManager.LoadAsync - real bug hit 2026-07-28: newly tamed
    /// pets vanished because the wrong rotating file was reloaded). The
    /// other file(s), when present, are just Hello Games' own rollback
    /// backups, not independently meaningful save points.</summary>
    public SaveFileEntry PrimaryFile => Files.OrderByDescending(f => File.GetLastWriteTimeUtc(f.FullPath)).First();

    public bool HasBackupCopy => Files.Count > 1;
}