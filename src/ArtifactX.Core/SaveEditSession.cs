using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ArtifactX.Core;

public sealed class SaveEditSession
{
    private const string PathSeparator = "\u0001";

    private readonly Dictionary<string, JToken?> _pendingEdits = new();

    public string WorkingJsonPath { get; }
    public JObject Root { get; }

    public event EventHandler? EditsChanged;

    private SaveEditSession(string workingJsonPath, JObject root)
    {
        WorkingJsonPath = workingJsonPath;
        Root = root;
    }

    public static async Task<SaveEditSession> LoadAsync(string workingJsonPath)
    {
        string json = await File.ReadAllTextAsync(workingJsonPath);
        JObject root = JObject.Parse(json.TrimEnd('\0', ' ', '\r', '\n'));
        return new SaveEditSession(workingJsonPath, root);
    }

    public bool HasUnsavedChanges => _pendingEdits.Count > 0;

    public JToken? GetValue(params string[] path)
    {
        string key = Join(path);
        return _pendingEdits.TryGetValue(key, out var staged) ? staged : Resolve(Root, path);
    }

    public long? GetLong(params string[] path) => GetValue(path)?.Value<long>();
    public string? GetString(params string[] path) => GetValue(path)?.Value<string>();
    public bool? GetBool(params string[] path) => GetValue(path)?.Value<bool>();

    public void StageEdit(JToken? newValue, params string[] path)
    {
        _pendingEdits[Join(path)] = newValue;
        EditsChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool HasStagedEdit(string[] path) => _pendingEdits.ContainsKey(Join(path));

    /// <summary>True if this exact path, or anything nested under it, has a
    /// staged edit - used to drive a whole page/container's "changed" state
    /// regardless of which specific field within it was touched.</summary>
    public bool HasStagedEditsUnder(string[] pathPrefix)
    {
        string exact = Join(pathPrefix);
        string prefix = exact + PathSeparator;
        return _pendingEdits.Keys.Any(k => k == exact || k.StartsWith(prefix));
    }

    public void RevertEdit(params string[] path)
    {
        if (_pendingEdits.Remove(Join(path)))
            EditsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Reverts every staged edit at or under this path - e.g. an
    /// entire container's unlock state AND any per-item amount edits within
    /// it, in one call.</summary>
    public void RevertEditsUnder(string[] pathPrefix)
    {
        string exact = Join(pathPrefix);
        string prefix = exact + PathSeparator;
        var keysToRemove = _pendingEdits.Keys.Where(k => k == exact || k.StartsWith(prefix)).ToList();

        if (keysToRemove.Count == 0) return;
        foreach (var k in keysToRemove) _pendingEdits.Remove(k);
        EditsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DiscardAllEdits()
    {
        if (_pendingEdits.Count == 0) return;
        _pendingEdits.Clear();
        EditsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task CommitAsync(IEnumerable<string> targetHgFilePaths)
    {
        foreach (var (key, value) in _pendingEdits)
            Apply(Root, key.Split(PathSeparator), value);

        string editedJson = Root.ToString(Newtonsoft.Json.Formatting.None);
        await File.WriteAllTextAsync(WorkingJsonPath, editedJson);

        foreach (var targetPath in targetHgFilePaths)
            await SaveStreamProcessor.WriteSaveContainerAsync(editedJson, targetPath);

        _pendingEdits.Clear();
        EditsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string Join(string[] path) => string.Join(PathSeparator, path);

    /// <summary>Navigates a path, treating a numeric segment as an array
    /// index when the current node is a JArray, or an object key otherwise.</summary>
    private static JToken? Resolve(JToken root, string[] path)
    {
        JToken? current = root;
        foreach (var segment in path)
        {
            if (current is null) return null;

            current = current is JArray arr && int.TryParse(segment, out int idx)
                ? (idx >= 0 && idx < arr.Count ? arr[idx] : null)
                : current[segment];
        }
        return current;
    }

    private static void Apply(JObject root, string[] path, JToken? value)
    {
        JToken current = root;
        for (int i = 0; i < path.Length - 1; i++)
        {
            if (current is JArray arr && int.TryParse(path[i], out int idx))
            {
                current = arr[idx]; // must already exist - editing, not inserting
                continue;
            }

            var next = current[path[i]];
            if (next is null)
            {
                var created = new JObject();
                ((JObject)current)[path[i]] = created;
                next = created;
            }
            current = next;
        }

        string lastSegment = path[^1];
        if (current is JArray lastArr && int.TryParse(lastSegment, out int lastIdx))
            lastArr[lastIdx] = value;
        else
            ((JObject)current)[lastSegment] = value;
    }
}