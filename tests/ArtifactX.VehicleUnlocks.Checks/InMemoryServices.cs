using ArtifactX.Core.Unlocks;
using Newtonsoft.Json.Linq;

namespace ArtifactX.WinUI3.Services;

internal static class SaveSessionManager
{
    public static JObject Root = new();
    private static readonly Dictionary<string, (string[] Path, JToken? Value)> Pending = [];
    public static object SessionIdentity { get; } = new();
    public static bool IsSaveLoaded => true;
    public static bool IsExpeditionActive => false;
    public static JToken? GetValue(params string[] path)
    {
        if (Pending.TryGetValue(string.Join('/', path), out var pending)) return pending.Value;
        JToken? node = Root;
        foreach (string segment in path) node = node is JArray array ? array[int.Parse(segment)] : node?[segment];
        return node;
    }
    public static bool HasStagedEditsUnder(string[] path) => Pending.ContainsKey(string.Join('/', path));
    public static void RevertEdit(string[] path) => Pending.Remove(string.Join('/', path));
    public static void StageEdit(JToken? value, string[] path) => Pending[string.Join('/', path)] = (path, value?.DeepClone());
    public static void StageEdits(IEnumerable<(JToken? Value, string[] Path)> edits)
    { foreach (var edit in edits) StageEdit(edit.Value, edit.Path); }
    public static void Commit()
    {
        foreach (var edit in Pending.Values)
        {
            JToken node = Root;
            foreach (string segment in edit.Path.SkipLast(1)) node = node is JArray array ? array[int.Parse(segment)] : node[segment]!;
            if (node is JArray list) list[int.Parse(edit.Path.Last())] = edit.Value!;
            else node[edit.Path.Last()] = edit.Value;
        }
        Pending.Clear();
    }
}

internal sealed record RewardUnlockAccount(HashSet<string> Rewards, HashSet<string> Titles);
internal sealed class RewardUnlockAccountEdits
{
    private readonly Dictionary<string, TitleUnlockRule[]> _pending = [];
    public Task LoadAsync() => Task.CompletedTask;
    public bool HasChanges => _pending.Count > 0;
    public RewardUnlockAccount Read() => new(_pending.Keys.ToHashSet(), _pending.Values.SelectMany(r => r).Select(r => r.TitleId).ToHashSet());
    public (JToken? Value, string[] Path)[] Plan(string id, TitleUnlockRule[] rules, bool enabled) => [(new JArray(rules.Select(r => r.TitleId)), [id])];
    public void Stage((JToken? Value, string[] Path)[] edits)
    { foreach (var edit in edits) _pending[edit.Path[0]] = ((JArray)edit.Value!).Values<string>().Select(s => new TitleUnlockRule(s!, "", "", "", "", 0, 0, "", "", "", "None", 0, "", false, "", [], [])).ToArray(); }
    public void Discard() => _pending.Clear();
}
internal sealed class RewardUnlockSettingsEdits
{
    private string _xml = "<Data><Property name=\"UnlockedSpecials\" /></Data>";
    public Task LoadAsync() => Task.CompletedTask;
    public bool HasChanges => Read().Count > 0;
    public HashSet<string> Read() => UserSettingsSpecialEdits.Read(_xml);
    public string Plan(string id, bool enabled) => UserSettingsSpecialEdits.Set(_xml, id, enabled);
    public void Stage(string value) => _xml = value;
    public void Discard() => _xml = "<Data><Property name=\"UnlockedSpecials\" /></Data>";
}
