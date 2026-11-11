using System;
using System.Collections.Generic;
using System.Linq;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Unlocks;
using Newtonsoft.Json.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>Grants (and takes back, before Save) expedition and title rewards: the save's
/// KnownSpecials, the account's reward lists and the user settings, together. Used by the
/// vehicle customisation unlocks and the Ship Designer's paint styles; named for vehicles until
/// 2026-11-11. Stages whole-array, loaded-save edits through the normal title bar
/// Save/Reset workflow. No direct save/account writes. Account edits are enlisted in the same Save/Reset workflow.</summary>
internal sealed class RewardUnlockEditor
{
    private static RewardUnlockEditor? _current;
    private readonly Dictionary<string, TitleUnlockRule[]> _pendingEnables = [];
    private readonly object? _owner = SaveSessionManager.SessionIdentity;
    private readonly RewardUnlockAccountEdits _account = new();
    private readonly RewardUnlockSettingsEdits _settings = new();
    private readonly Dictionary<string, (string[] Path, JArray Original, JArray Applied, bool Staged, HashSet<string> Ids)> _changes = [];
    public static RewardUnlockEditor Current => _current is not null && ReferenceEquals(_current._owner, SaveSessionManager.SessionIdentity)
        ? _current : _current = new();
    public async System.Threading.Tasks.Task LoadAsync()
    { await _account.LoadAsync(); await _settings.LoadAsync(); }
    public RewardUnlockAccount? Account
    {
        get
        {
            var account = _account.Read();
            if (account is null) return null;
            var rewards = new HashSet<string>(account.Rewards, StringComparer.Ordinal);
            rewards.UnionWith(_settings.Read());
            return new(rewards, account.Titles);
        }
    }
    public bool HasChanges => _account.HasChanges || _settings.HasChanges || _changes.Values.Any(c => SaveSessionManager.HasStagedEditsUnder(c.Path));
    public bool IsPendingEnable(string id)
    {
        // Once the slot write finishes, these are persisted unlocks, including
        // when a later companion write fails. Never offer relocking them.
        if (!SaveSessionManager.HasStagedEditsUnder(SpecialsPath)) _pendingEnables.Clear();
        return _pendingEnables.ContainsKey(id);
    }

    private IEnumerable<(string[] Path, JArray Value, bool Revert)> ResetPlan()
    {
        foreach (var c in _changes.Values)
        {
            if (!SaveSessionManager.HasStagedEditsUnder(c.Path) || SaveSessionManager.GetValue(c.Path) is not JArray current) continue;
            string? field = c.Path.Last() == "gUR" ? "b2n" : null;
            string Key(JToken t) => (field is null ? t.Value<string>() : t[field]?.Value<string>())?.TrimStart('^') ?? "";
            var ids = c.Ids;
            var entries = ids.Select(id => new UnlockEntryChange(id, c.Original.FirstOrDefault(t => Key(t) == id), c.Applied.FirstOrDefault(t => Key(t) == id)))
                .Where(e => !JToken.DeepEquals(e.Original, e.Applied));
            var restored = RewardUnlockEdits.RestoreEntries(current, entries, field);
            yield return (c.Path, restored, !c.Staged && JToken.DeepEquals(restored, c.Original));
        }
    }

    public void Reset()
    {
        foreach (var edit in ResetPlan().ToArray())
            if (edit.Revert) SaveSessionManager.RevertEdit(edit.Path);
            else SaveSessionManager.StageEdit(edit.Value, edit.Path);
        _pendingEnables.Clear();
        _changes.Clear();
        _settings.Discard();
        _account.Discard();
    }

    internal static readonly string[] SpecialsPath = ["vLc", "6f=", "24<"];

    public void Set(string id, TitleUnlockRule[] rules, bool enabled)
    {
        if (!enabled)
        {
            if (!IsPendingEnable(id)) throw new InvalidOperationException("Saved unlocks cannot be turned off here.");
            var remaining = _pendingEnables.Where(e => e.Key != id).ToArray();
            // Validate the replay against the reset projection before changing
            // staged state. Shared counters are recalculated for remaining picks.
            var reset = ResetPlan().ToArray();
            foreach (var counter in reset.Where(e => e.Path.Last() == "gUR"))
                _ = RewardUnlockEdits.RaiseRequirements(counter.Value, remaining.SelectMany(e => e.Value));
            foreach (var entry in remaining)
            {
                _ = _account.Plan(entry.Key, entry.Value, true);
                _ = _settings.Plan(entry.Key, true);
            }
            Reset();
            foreach (var entry in remaining) Set(entry.Key, entry.Value, true);
            return;
        }
        IsPendingEnable(id);
        if (!_pendingEnables.ContainsKey(id) &&
            (Account?.Rewards.Contains(id) == true || (SaveSessionManager.GetValue(SpecialsPath) as JArray)?.Any(t => t.Value<string>()?.TrimStart('^') == id) == true))
            throw new InvalidOperationException("This part is already unlocked.");
        if (!SaveSessionManager.IsSaveLoaded || SaveSessionManager.GetValue(SpecialsPath) is not JArray specials)
            throw new InvalidOperationException("Load a save with customization unlock data first.");
        if (!ReferenceEquals(_owner, SaveSessionManager.SessionIdentity)) throw new InvalidOperationException("Reload customization unlocks for the current save.");
        var accountEdits = _account.Plan(id, rules, enabled);
        var settingsEdit = _settings.Plan(id, enabled);
        var edits = new List<(JToken? Value, string[] Path)>();
        var updatedSpecials = RewardUnlockEdits.Specials(specials, id, enabled);
        if (enabled && rules.Length > 0)
        {
            if (SaveSessionManager.IsExpeditionActive && rules.Any(r => r.BlockedInSeasons.Length > 0))
                throw new InvalidOperationException("This reward has expedition restrictions. Edit it from the main save context.");
            if (SaveSessionManager.GetValue(NmsPlayerStatsPaths.StatGroupsArrayPath) is not JArray groups)
                throw new InvalidOperationException("The save's progress counters are unavailable.");
            int index = Enumerable.Range(0, groups.Count)
                .FirstOrDefault(i => groups[i][":rc"]?.Value<string>()?.TrimStart('^') == "GLOBAL_STATS", -1);
            if (index < 0 || SaveSessionManager.GetValue(NmsPlayerStatsPaths.GroupStatsArrayPath(index)) is not JArray stats)
                throw new InvalidOperationException("The save's global progress counters are unavailable.");
            var raised = RewardUnlockEdits.RaiseRequirements(stats, rules);
            if (!JToken.DeepEquals(stats, raised)) edits.Add((raised, NmsPlayerStatsPaths.GroupStatsArrayPath(index)));
        }
        if (!JToken.DeepEquals(specials, updatedSpecials)) edits.Add((updatedSpecials, SpecialsPath));
        foreach (var edit in edits)
        {
            string key = string.Join("/", edit.Path);
            var current = (JArray)SaveSessionManager.GetValue(edit.Path)!;
            if (!_changes.TryGetValue(key, out var previous) || !SaveSessionManager.HasStagedEditsUnder(edit.Path))
                previous = (edit.Path, (JArray)current.DeepClone(), current, SaveSessionManager.HasStagedEditsUnder(edit.Path), []);
            string? field = edit.Path.Last() == "gUR" ? "b2n" : null;
            string Key(JToken t) => (field is null ? t.Value<string>() : t[field]?.Value<string>())?.TrimStart('^') ?? "";
            var next = (JArray)edit.Value!;
            foreach (string changed in current.Concat(next).Select(Key).Distinct().Where(k =>
                !JToken.DeepEquals(current.FirstOrDefault(t => Key(t) == k), next.FirstOrDefault(t => Key(t) == k))))
                previous.Ids.Add(changed);
            _changes[key] = (edit.Path, previous.Original, (JArray)next.DeepClone(), previous.Staged, previous.Ids);
        }
        _settings.Stage(settingsEdit);
        _account.Stage(accountEdits);
        // Validation finishes before any edit is staged. One notice keeps the
        // title bar and surrounding editor from refreshing once per counter.
        SaveSessionManager.StageEdits(edits);
        _pendingEnables[id] = rules;
    }
}
