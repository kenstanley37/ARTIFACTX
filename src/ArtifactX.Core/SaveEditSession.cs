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

    /// <summary>Number of distinct field paths with a staged edit - not the
    /// number of individual values changed within them, since an array
    /// field (e.g. Settlement Stats) is staged as a single whole-array
    /// entry regardless of how many of its elements actually differ.</summary>
    public int PendingEditCount => _pendingEdits.Count;

    /// <summary>Diagnostic only - readable "a/b/c" forms of every staged
    /// path plus its new value, for tracking down unexpected pending edits
    /// (e.g. a page's Load method accidentally staging something it only
    /// meant to display).</summary>
    public IEnumerable<string> DescribePendingEdits() =>
        _pendingEdits.Select(kv => $"{kv.Key.Replace(PathSeparator, "/")} = {kv.Value?.ToString() ?? "null"}");

    /// <summary>The value at a path as it will be once saved. A path inside a
    /// staged value (e.g. one field of a whole new Multi-Tool entry) reads from
    /// that staged value, and a container with staged edits inside it (e.g. the
    /// Multi-Tool list once a new entry is staged) comes back as a copy with
    /// them applied. That copy is skipped for the top two levels ("vLc/6f="),
    /// which are far too big to copy on every read; nothing reads those
    /// expecting staged values.</summary>
    public JToken? GetValue(params string[] path)
    {
        string key = Join(path);
        JToken? result;
        if (_pendingEdits.TryGetValue(key, out var staged))
            result = staged;
        else
        {
            result = Resolve(Root, path);
            for (int n = path.Length - 1; n > 0; n--)
            {
                if (_pendingEdits.TryGetValue(Join(path[..n]), out var ancestor))
                {
                    result = ancestor is null ? null : Resolve(ancestor, path[n..]);
                    break;
                }
            }
        }

        if (result is null || path.Length < 3 || _pendingEdits.Count == 0) return result;
        string prefix = key + PathSeparator;
        var inside = _pendingEdits.Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(kv => (Path: kv.Key[prefix.Length..].Split(PathSeparator), kv.Value))
            .OrderBy(e => e.Path.Length)
            .ToList();
        if (inside.Count == 0) return result;

        var copy = result.DeepClone();
        foreach (var (subPath, value) in inside)
            Apply(copy, subPath, value?.DeepClone());
        return copy;
    }

    public long? GetLong(params string[] path) => GetValue(path)?.Value<long>();
    public string? GetString(params string[] path) => GetValue(path)?.Value<string>();
    public bool? GetBool(params string[] path) => GetValue(path)?.Value<bool>();

    /// <summary>Stages a new value for a path. Staging a whole container
    /// replaces any edits staged inside it earlier, since the new value
    /// already decides everything under it.</summary>
    public void StageEdit(JToken? newValue, params string[] path)
    {
        string key = Join(path);
        string prefix = key + PathSeparator;
        foreach (var inside in _pendingEdits.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            _pendingEdits.Remove(inside);
        _pendingEdits[key] = newValue;
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
        // Containers before the edits inside them (a field edited on a newly
        // staged entry must land on that entry, not be overwritten by it).
        foreach (var (path, value) in _pendingEdits.Select(kv => (Path: kv.Key.Split(PathSeparator), kv.Value)).OrderBy(e => e.Path.Length).ToList())
            Apply(Root, path, value);

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

    private static void Apply(JToken root, string[] path, JToken? value)
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
        {
            // Index == Count appends - used when a page stages a brand-new
            // entry the game creates lazily (e.g. a ^SYSTEM_STATS group).
            if (lastIdx == lastArr.Count)
                lastArr.Add(value ?? JValue.CreateNull());
            else
                lastArr[lastIdx] = value;
        }
        else
            ((JObject)current)[lastSegment] = value;
    }
}