using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace NMS.Core;

/// <summary>
/// Holds one save file's parsed JSON tree plus a staged-edits dictionary. Reads
/// check staged edits first, falling back to the loaded tree - so every page
/// always sees "what this would look like if saved right now" without anything
/// touching disk until CommitAsync. Nothing here assumes a specific save
/// structure - paths are just JSON key sequences - so it works for any field
/// once you know its path, mapped or not.
/// </summary>
public sealed class SaveEditSession
{
    private const string PathSeparator = "\u0001"; // control char - never appears in real JSON keys

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

    public void RevertEdit(params string[] path)
    {
        if (_pendingEdits.Remove(Join(path)))
            EditsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DiscardAllEdits()
    {
        if (_pendingEdits.Count == 0) return;
        _pendingEdits.Clear();
        EditsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Applies every staged edit to the tree, reserializes, and writes both the
    /// decrypted working copy and every real .hg file in the slot (a slot is
    /// always a pair - manual + auto - so both need the same edited content).
    /// This is the one place real work happens - call it off the UI thread.
    /// </summary>
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

    private static JToken? Resolve(JToken root, string[] path)
    {
        JToken? current = root;
        foreach (var segment in path)
        {
            if (current is null) return null;
            current = current[segment];
        }
        return current;
    }

    private static void Apply(JObject root, string[] path, JToken? value)
    {
        JToken current = root;
        for (int i = 0; i < path.Length - 1; i++)
        {
            var next = current[path[i]];
            if (next is null)
            {
                var created = new JObject();
                ((JObject)current)[path[i]] = created;
                next = created;
            }
            current = next;
        }

        ((JObject)current)[path[^1]] = value;
    }
}