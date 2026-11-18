namespace ArtifactX.Maui.Services;

/// <summary>The phone's settings store (MAUI Preferences), kept between runs.</summary>
public sealed class PreferencesStore : IKeyValueStore
{
    /// <summary>Null when missing or stored as another type (the galaxy was briefly stored as a number on test
    /// phones): the caller then uses its default.</summary>
    public string? Get(string key)
    {
        try { return Preferences.Default.ContainsKey(key) ? Preferences.Default.Get(key, "") : null; }
        catch (InvalidCastException) { return null; }
#if ANDROID
        catch (Java.Lang.ClassCastException) { return null; }
#endif
    }

    public void Set(string key, string value) => Preferences.Default.Set(key, value);
}
