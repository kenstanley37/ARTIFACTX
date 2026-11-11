using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArtifactX.Core;
using ArtifactX.Core.Unlocks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>Isolated, staged account edits enlisted in the loaded save's
/// Save/Reset operation. Does not commit edits belonging to the account editor.
/// Bgb/fyX and expedition records were inspected in real account data on
/// 2026-11-02; existing-title grant guards are native-tested on build 184271.</summary>
internal sealed class RewardUnlockAccountEdits : IStagedSaveCompanion
{
    private readonly object? _owner = SaveSessionManager.SessionIdentity;
    private SaveEditSession? _session;
    private string? _path, _originalJson;
    private Task? _loading;
    public bool HasChanges => _session?.HasUnsavedChanges ?? false;
    public int PendingCount => _session?.PendingEditCount ?? 0;
    public bool IsLoaded => _session is not null;
    public Task LoadAsync() => _loading ??= LoadCoreAsync();

    private async Task LoadCoreAsync()
    {
        _path = AccountSessionManager.ResolveAccountDataPath();
        if (_path is null) return;
        _session = await Task.Run(() => SaveEditSession.LoadAsync(_path));
        _originalJson = _session.Root.ToString(Formatting.None);
        if (ReferenceEquals(_owner, SaveSessionManager.SessionIdentity)) SaveSessionManager.Enlist(this);
    }

    public RewardUnlockAccount? Read()
    {
        if (_session is null) return null;
        System.Collections.Generic.HashSet<string> ReadIds(params string[] fields) => fields
            .SelectMany(f => _session.GetValue("B89", f) as JArray ?? [])
            .Select(t => CatalogService.NormalizeId(t.Value<string>() ?? "")).ToHashSet(StringComparer.Ordinal);
        return new(ReadIds("B1h", "fyX", "d4U"), ReadIds("Bgb"));
    }

    public (JToken? Value, string[] Path)[] Plan(string id, TitleUnlockRule[] rules, bool enabled)
    {
        if (_session is null) throw new InvalidOperationException("Account unlock data is unavailable.");
        var edits = new System.Collections.Generic.List<(JToken?, string[])>();
        // The native special grant updates UnlockedSpecials. Removing an option
        // also removes its direct product entries from the observed account lists.
        foreach (string field in enabled ? new[] { "fyX" } : new[] { "fyX", "B1h", "d4U" })
        {
            if (_session.GetValue("B89", field) is not JArray current)
                throw new InvalidOperationException("The account unlock list has an unexpected format.");
            var updated = RewardUnlockEdits.Specials(current, id, enabled);
            if (!JToken.DeepEquals(current, updated)) edits.Add((updated, ["B89", field]));
        }
        if (enabled && rules.Length > 0)
        {
            if (_session.GetValue("B89", "Bgb") is not JArray titles)
                throw new InvalidOperationException("The account title list has an unexpected format.");
            JArray updated = titles;
            foreach (var rule in rules) updated = RewardUnlockEdits.Specials(updated, rule.TitleId, true);
            if (!JToken.DeepEquals(titles, updated)) edits.Add((updated, ["B89", "Bgb"]));
        }
        return edits.ToArray();
    }

    public void Stage((JToken? Value, string[] Path)[] edits)
    {
        _session?.StageEdits(edits);
        SaveSessionManager.CompanionChanged();
    }

    public async Task ValidateAsync()
    {
        if (!HasChanges || _path is null) return;
        if (GameProcessMonitorService.IsGameRunning) throw new InvalidOperationException("Close No Man's Sky before saving account unlock changes.");
        if (AccountSessionManager.HasUnsavedChanges)
            throw new InvalidOperationException("Save or reset pending changes on the Account Data page before saving these unlocks.");
        string json = await File.ReadAllTextAsync(_path);
        if (JObject.Parse(json.TrimEnd('\0')).ToString(Formatting.None) != _originalJson)
            throw new InvalidOperationException("The account file changed outside this editor. Reset these unlock changes and reload the save before editing again.");
    }

    public async Task CommitAsync()
    {
        if (_session is null || _path is null || !HasChanges) return;
        await ValidateAsync();
        BackupService.BackupFiles([_path], BackupService.BuildAccountDataGroupKey(Path.GetDirectoryName(_path) ?? ""));
        await Task.Run(() => _session.CommitAsync([]));
        _originalJson = _session.Root.ToString(Formatting.None);
        // Refresh other account pages on next visit, rather than leaving a cached
        // pre-unlock account session able to overwrite this save later.
        AccountSessionManager.CloseSession();
        SaveSessionManager.CompanionChanged();
    }

    public void Discard()
    {
        _session?.DiscardAllEdits();
        SaveSessionManager.CompanionChanged();
    }
}
