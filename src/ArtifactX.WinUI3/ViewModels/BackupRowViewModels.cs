using System.Collections.Generic;

namespace ArtifactX.WinUI3.ViewModels;

/// <summary>One backup of the player's own data (user.db) on the Settings page.</summary>
public sealed record DataBackupRowViewModel(string Path, string Label, string ShortDate)
{
    public string RestoreName => $"Restore the backup from {ShortDate}";
    public string DeleteName => $"Delete the backup from {ShortDate}";
}

/// <summary>One account's game save backups on the Settings page: a line per slot.</summary>
public sealed record SaveBackupAccountViewModel(string Account, IReadOnlyList<string> Lines);
