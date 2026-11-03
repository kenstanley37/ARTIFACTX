using ArtifactX.Core;
using Newtonsoft.Json.Linq;

namespace ArtifactX.WinUI3.Services
{
    internal static class SaveSessionManager
    {
        public static event EventHandler? ActiveSessionChanged;
        public static event EventHandler? PendingEditsChanged;
        public static event Action<JObject>? GameSavedLoadedSlot;
        public static void NotifySession() => ActiveSessionChanged?.Invoke(null, EventArgs.Empty);
        public static void NotifyEdits() => PendingEditsChanged?.Invoke(null, EventArgs.Empty);
        public static void NotifyGame(JObject disk) => GameSavedLoadedSlot?.Invoke(disk);
        public static bool IsExpeditionActive => false;
        public static bool IsMidExpedition(JObject disk) => disk["expedition"]?.Value<bool>() ?? false;
        public static bool AdoptFromGame(JObject disk, params string[] path)
        {
            if (Session is null || HasStagedEditsUnder(path)) return false;
            JToken? source = disk;
            JToken? target = Session.Root;
            foreach (var key in path) source = source?[key];
            foreach (var key in path[..^1]) target = target?[key];
            if (source is null || target is null) return false;
            target[path[^1]] = source.DeepClone();
            return true;
        }
        public static SaveEditSession? Session { get; set; }
        public static object? SessionIdentity => Session;
        public static JToken? GetValue(params string[] path) => Session?.GetValue(path);
        public static string? GetString(params string[] path) => Session?.GetString(path);
        public static bool HasStagedEditsUnder(params string[] path) => Session?.HasStagedEditsUnder(path) ?? false;
        public static void StageEdit(JToken? value, params string[] path) => Session?.StageEdit(value, path);
        public static void RevertEditsUnder(params string[] path) => Session?.RevertEditsUnder(path);
    }

    internal static class CatalogService
    {
        public static TaskCompletionSource? WarmGate { get; set; }
        public static async Task WarmCacheAsync(IEnumerable<string?> ids)
        {
            _ = ids.ToArray();
            if (WarmGate is { } gate) await gate.Task;
        }
        public static Models.CatalogEntry? TryGet(string? id) => null;
        public static bool IsFishSpecies(string id) => false;
    }
}

namespace ArtifactX.WinUI3.Models
{
    public sealed class CatalogEntry
    {
        public string DisplayName { get; init; } = "";
        public string? CatalogGroup { get; init; }
        public string? TemplateType { get; init; }
    }
}
