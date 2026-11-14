using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// The planets a player pinned in the Planet Finder (owner, 2026-11-14: the list follows you from system to system, so
/// keep the ones you mean to visit). A pin is the planet's system address and planet number, kept in the profile settings
/// (SettingsStore, user.db) like <see cref="CreaturePinService"/>, so pins survive restarts and travel with a backup.
/// </summary>
public static class PlanetPinService
{
    private const string Key = "PlanetFinderPins";
    private static HashSet<(ulong System, int Planet)>? _pins;

    public static bool IsPinned(ulong system, int planet) => Pins.Contains((system, planet));

    /// <summary>A copy of the pins, for reading off the UI thread.</summary>
    public static HashSet<(ulong System, int Planet)> Snapshot() => [.. Pins];

    /// <summary>Pins or unpins; returns whether it is pinned now.</summary>
    public static bool Toggle(ulong system, int planet)
    {
        bool pinned = Pins.Add((system, planet)) || !Pins.Remove((system, planet));
        SettingsStore.Set(SettingsStore.Scope.Profile, Key,
            Pins.Count == 0 ? null : string.Join(";", Pins.Select(p => $"{p.System:X}:{p.Planet}")));
        return pinned;
    }

    private static HashSet<(ulong System, int Planet)> Pins => _pins ??= Parse(SettingsStore.Get(SettingsStore.Scope.Profile, Key));

    private static HashSet<(ulong, int)> Parse(string? saved)
    {
        var pins = new HashSet<(ulong, int)>();
        foreach (var entry in (saved ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = entry.Split(':');
            if (parts.Length == 2 && ulong.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong system)
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int planet))
                pins.Add((system, planet));
        }
        return pins;
    }
}
