using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// The creatures a player pinned in the Creature Finder (owner, 2026-11-12: keep the one you're hunting in sight).
/// A pin is the creature's planet address and seed, kept in the profile settings (SettingsStore, user.db), so pins
/// survive restarts and travel with a backup.
/// </summary>
public static class CreaturePinService
{
    private const string Key = "CreatureFinderPins";
    private static HashSet<string>? _pins;

    /// <summary>A creature's pin key: its planet's packed address and its role seed.</summary>
    public static string KeyOf(ulong planetAddress, ulong creatureSeed) => $"{planetAddress:X}:{creatureSeed:X}";

    public static bool IsPinned(string key) => Pins.Contains(key);

    /// <summary>Pins or unpins; returns whether it is pinned now.</summary>
    public static bool Toggle(string key)
    {
        bool pinned = Pins.Add(key) || !Pins.Remove(key);
        SettingsStore.Set(SettingsStore.Scope.Profile, Key, Pins.Count == 0 ? null : string.Join(";", Pins));
        return pinned;
    }

    private static HashSet<string> Pins => _pins ??= new HashSet<string>(
        (SettingsStore.Get(SettingsStore.Scope.Profile, Key) ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries),
        StringComparer.OrdinalIgnoreCase);
}
