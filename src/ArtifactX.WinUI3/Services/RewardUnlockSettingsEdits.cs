using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ArtifactX.Core.Unlocks;

namespace ArtifactX.WinUI3.Services;

/// <summary>Stages the installed game's settings unlock mirror alongside the
/// slot/account edits. Never writes until title-bar Save; validates external
/// changes and backs up the full settings file before committing.</summary>
internal sealed class RewardUnlockSettingsEdits : IStagedSaveCompanion
{
    private readonly object? _owner = SaveSessionManager.SessionIdentity;
    private string? _path, _original, _pending;
    private Task? _loading;
    private bool _bom;
    private HashSet<string> _ids = [];
    public bool HasChanges => _pending is not null && _pending != _original;
    public int PendingCount => HasChanges ? 1 : 0;
    public Task LoadAsync() => _loading ??= LoadCoreAsync();
    private async Task LoadCoreAsync()
    {
        string? root = await Task.Run(() => GameDataService.Game?.RootFolder);
        if (root is null) return;
        _path = Path.Combine(root, "Binaries", "SETTINGS", "GCUSERSETTINGSDATA.MXML");
        if (!File.Exists(_path)) return;
        byte[] bytes = await File.ReadAllBytesAsync(_path);
        _bom = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf;
        _original = System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        _ids = await Task.Run(() => UserSettingsSpecialEdits.Read(_original));
        if (ReferenceEquals(_owner, SaveSessionManager.SessionIdentity)) SaveSessionManager.Enlist(this);
    }
    public HashSet<string> Read() => new(_ids, StringComparer.Ordinal);
    public string Plan(string id, bool enabled) => UserSettingsSpecialEdits.Set(_pending ?? _original
        ?? throw new InvalidOperationException("Locate the game installation and its user settings before editing customization unlocks."), id, enabled);
    public void Stage(string value)
    { _pending = value; _ids = UserSettingsSpecialEdits.Read(value); }
    public async Task ValidateAsync()
    {
        if (!HasChanges || _path is null) return;
        if (GameProcessMonitorService.IsGameRunning) throw new InvalidOperationException("Close No Man's Sky before saving game settings unlocks.");
        if (await File.ReadAllTextAsync(_path) != _original)
            throw new InvalidOperationException("Game settings changed outside this editor. Reset unlock changes and reload the save before editing again.");
    }
    public async Task CommitAsync()
    {
        if (!HasChanges || _path is null) return;
        await ValidateAsync();
        BackupService.BackupFiles([_path], BackupService.BuildAccountDataGroupKey(Path.GetDirectoryName(_path)!));
        await File.WriteAllTextAsync(_path, _pending!, new System.Text.UTF8Encoding(_bom));
        _original = _pending;
        _pending = null;
        SaveSessionManager.CompanionChanged();
    }
    public void Discard()
    { _pending = null; _ids = _original is null ? [] : UserSettingsSpecialEdits.Read(_original); }
}
