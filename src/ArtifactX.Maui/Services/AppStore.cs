namespace ArtifactX.Maui.Services;

/// <summary>Small settings kept on the phone (the galaxy being viewed, recent systems). Behind an interface so the
/// services run in tests/ArtifactX.PortalScanning.Checks without MAUI.</summary>
public interface IKeyValueStore
{
    string? Get(string key);
    void Set(string key, string value);
}

/// <summary>The store the app's services use: MauiProgram sets the phone's (PreferencesStore); until then, and in the
/// checks, values live in memory.</summary>
public static class AppStore
{
    public static IKeyValueStore Current { get; set; } = new MemoryStore();

    private sealed class MemoryStore : IKeyValueStore
    {
        private readonly Dictionary<string, string> _values = new();
        public string? Get(string key) => _values.GetValueOrDefault(key);
        public void Set(string key, string value) => _values[key] = value;
    }
}
