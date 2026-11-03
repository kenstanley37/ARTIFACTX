using System;
using System.Collections.Generic;
using System.Linq;
using ArtifactX.Core.NmsModels;
using ArtifactX.Core.Unlocks;
using Newtonsoft.Json.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>Stages whole-array, loaded-save edits through the normal title bar
/// Save/Reset workflow. No direct save/account writes. Account edits are enlisted in the same Save/Reset workflow.</summary>
internal sealed class VehicleUnlockEditor
{
    private static VehicleUnlockEditor? _current;
    private readonly object? _owner = SaveSessionManager.SessionIdentity;
    private readonly VehicleUnlockAccountEdits _account = new();
    private readonly Dictionary<string, (string[] Path, JArray Original, JArray Applied, bool Staged, HashSet<string> Ids)> _changes = [];
    public static VehicleUnlockEditor Current => _current is not null && ReferenceEquals(_current._owner, SaveSessionManager.SessionIdentity)
        ? _current : _current = new();
    public System.Threading.Tasks.Task LoadAsync() => _account.LoadAsync();
    public VehicleUnlockAccount? Account => _account.Read();
    public bool HasChanges => _account.HasChanges || _changes.Values.Any(c => SaveSessionManager.HasStagedEditsUnder(c.Path));
    public void Reset()
    {
        foreach (var c in _changes.Values)
        {
            if (!SaveSessionManager.HasStagedEditsUnder(c.Path) || SaveSessionManager.GetValue(c.Path) is not JArray current) continue;
            string? field = c.Path.Last() == "gUR" ? "b2n" : null;
            string Key(JToken t) => (field is null ? t.Value<string>() : t[field]?.Value<string>())?.TrimStart('^') ?? "";
            var ids = c.Ids;
            var entries = ids.Select(id => new UnlockEntryChange(id, c.Original.FirstOrDefault(t => Key(t) == id), c.Applied.FirstOrDefault(t => Key(t) == id)))
                .Where(e => !JToken.DeepEquals(e.Original, e.Applied));
            var restored = VehicleUnlockEdits.RestoreEntries(current, entries, field);
            if (!c.Staged && JToken.DeepEquals(restored, c.Original)) SaveSessionManager.RevertEdit(c.Path);
            else SaveSessionManager.StageEdit(restored, c.Path);
        }
        _changes.Clear();
        _account.Discard();
    }

    internal static readonly string[] SpecialsPath = ["vLc", "6f=", "24<"];

    public void Set(string id, TitleUnlockRule[] rules, bool enabled)
    {
        if (!SaveSessionManager.IsSaveLoaded || SaveSessionManager.GetValue(SpecialsPath) is not JArray specials)
            throw new InvalidOperationException("Load a save with customization unlock data first.");
        if (!ReferenceEquals(_owner, SaveSessionManager.SessionIdentity)) throw new InvalidOperationException("Reload customization unlocks for the current save.");
        var accountEdits = _account.Plan(id, rules, enabled);
        var edits = new List<(JToken? Value, string[] Path)>();
        var updatedSpecials = VehicleUnlockEdits.Specials(specials, id, enabled);
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
            var raised = VehicleUnlockEdits.RaiseRequirements(stats, rules);
            if (!JToken.DeepEquals(stats, raised)) edits.Add((raised, NmsPlayerStatsPaths.GroupStatsArrayPath(index)));
        }
        if (!JToken.DeepEquals(specials, updatedSpecials)) edits.Add((updatedSpecials, SpecialsPath));
        if (!enabled && SaveSessionManager.GetValue("vLc", "6f=", "eZ<") is JArray products)
        {
            var updated = VehicleUnlockEdits.Specials(products, id, false);
            if (!JToken.DeepEquals(products, updated)) edits.Add((updated, ["vLc", "6f=", "eZ<"]));
        }
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
        _account.Stage(accountEdits);
        // Validation finishes before any edit is staged. One notice keeps the
        // title bar and surrounding editor from refreshing once per counter.
        SaveSessionManager.StageEdits(edits);
    }
}
