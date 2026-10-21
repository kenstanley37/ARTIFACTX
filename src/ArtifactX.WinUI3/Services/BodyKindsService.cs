using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Whether each body in a system is a planet or a moon, and which planet a moon orbits.
/// In order of trust (2026-10-17):
/// 1. what the game showed - a body's pop-up says MARK MOON or MARK PLANET (read by
///    game screen recording, kept in user.db table body_kinds, migration 15);
/// 2. the system generator, which places moons body by body: a large planet takes up to
///    two moons right after it (SystemBodies.Hosts) - exact on Amogat-Aizu XIV and
///    Muraya-Risti against the game's own Discoveries views;
/// 3. when the moon count the player recorded disagrees with the generator's, the old
///    guess (moons are the bodies right after planet 1).
/// </summary>
public static class BodyKindsService
{
    private static readonly object Lock = new();
    private static Dictionary<(string System, int Body), bool>? _captured;

    /// <summary>Raised (any thread) when a body's kind was read off the game screen.</summary>
    public static event Action<ulong>? Captured;

    private static string Key(ulong system) => system.ToString("X", CultureInfo.InvariantCulture);

    private static Dictionary<(string, int), bool> CapturedKinds
    {
        get
        {
            lock (Lock)
            {
                if (_captured is not null) return _captured;
                _captured = new();
                try
                {
                    using var connection = UserDatabase.Open();
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT system_address, planet_index, is_moon FROM body_kinds WHERE deleted = 0";
                    using var r = cmd.ExecuteReader();
                    while (r.Read()) _captured[(r.GetString(0), r.GetInt32(1))] = r.GetInt32(2) == 1;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[BodyKindsService] {ex.Message}");
                }
                return _captured;
            }
        }
    }

    public static bool IsMoon(ulong system, int body) => Host(system, body) > 0 || CapturedMoon(system, body) == true;

    /// <summary>The planet a moon orbits (1-based), 0 for a planet - or -1 for a moon the
    /// game showed as one whose host isn't known.</summary>
    public static int Host(ulong system, int body)
    {
        var captured = CapturedMoon(system, body);
        var bodies = UniverseInfo.Bodies(system);
        int? recordedMoons = SystemNotesService.Get(system)?.Moons;
        bool trustGenerator = recordedMoons is null || recordedMoons == bodies.MoonCount;
        int generated = trustGenerator && body >= 1 && body <= bodies.Hosts.Length ? bodies.Hosts[body - 1] : 0;
        if (captured == false) return 0;
        if (captured == true) return generated > 0 ? generated : -1;
        if (trustGenerator) return generated;
        // The recorded count disagrees with the generator: the old guess.
        return recordedMoons is int moons && body >= 2 && body <= 1 + moons ? 1 : 0;
    }

    private static bool? CapturedMoon(ulong system, int body)
    {
        lock (Lock) return CapturedKinds.TryGetValue((Key(system), body), out bool moon) ? moon : null;
    }

    /// <summary>A body's pop-up ("Zionehou Omega / Discovered by: ... / MARK MOON") on any
    /// Discoveries screen: stores whether it's a moon. Returns a note for the log, or null.</summary>
    public static string? TryRead(IReadOnlyList<string> lines)
    {
        var list = lines.ToList();
        int mark = list.FindIndex(l => Regex.IsMatch(l, @"MARK\s*(MOON|PLANET)", RegexOptions.IgnoreCase)
            || Regex.IsMatch(l, @"Pin\s+(moon|planet)\s+location", RegexOptions.IgnoreCase));
        bool moon;
        string? type = null;
        if (mark >= 0)
            moon = Regex.IsMatch(list[mark], @"MOON", RegexOptions.IgnoreCase)
                || (mark + 1 < list.Count && Regex.IsMatch(list[mark + 1], @"Pin\s+moon", RegexOptions.IgnoreCase));
        else
        {
            // The Discoveries list's pop-up names the body's type right under it:
            // "Contaminated Moon", "Verdant Planet" (2026-10-17).
            int by0 = list.FindIndex(l => l.Contains("Discovered by", StringComparison.OrdinalIgnoreCase));
            if (by0 < 0) return null;
            type = list.Skip(by0 + 1).Take(3).Select(l => l.Trim())
                .FirstOrDefault(l => Regex.IsMatch(l, @"^[\p{L} \[\]'-]{3,30}\s(Moon|Planet)$", RegexOptions.IgnoreCase)); // "[REDACTED] Planet" too
            if (type is null) return null;
            moon = type.EndsWith("Moon", StringComparison.OrdinalIgnoreCase);
            mark = by0 + 1;
        }
        // The pop-up's name: above its "Discovered by:" line, past stray icon reads.
        int by = list.FindLastIndex(mark, l => l.Contains("Discovered by", StringComparison.OrdinalIgnoreCase));
        if (by <= 0) return "body pop-up: no name";
        string? name = null;
        for (int i = by - 1; i >= 0 && i >= by - 3; i--)
        {
            string line = PlanetPageReader.Clean(list[i]);
            if (line.Count(char.IsLetter) >= 3) { name = line; break; }
        }
        if (name is null) return "body pop-up: no name";
        if (PlanetPageReader.MatchBody(name) is not (ulong system, int body, string matched))
        {
            // Its type and resources go with it, for when it's placed (Rigie IV's were lost, 2026-10-20).
            var heldResources = PlanetPageReader.PopupResources(list, mark);
            UnmatchedBodiesService.Hold(name, !string.IsNullOrEmpty(type) || heldResources.Count > 0
                ? new PlanetDetails { Descriptor = type ?? "", Resources = heldResources } : null, moon);
            return $"body pop-up \"{name}\": unknown name - held for you to place on Current System";
        }

        // The list pop-up also lists the body's resources: fill them in (the full page
        // adds weather, sentinels, flora and fauna).
        string resources = PlanetPageReader.ReadPopupResources(list, mark, system, body);
        if (!string.IsNullOrEmpty(type))
        {
            var existingDetails = PlanetDetailsService.Get(system, body);
            var details = existingDetails?.Copy() ?? new PlanetDetails();
            if (string.IsNullOrEmpty(details.Descriptor))
            {
                details.Descriptor = type;
                PlanetDetailsService.Save(system, body, details, source: "captured");
            }
        }

        bool generatorSays = UniverseInfo.Bodies(system).IsMoon(body);
        string check = generatorSays == moon ? "" : $" (the generator said {(generatorSays ? "moon" : "planet")})";
        lock (Lock)
        {
            if (CapturedKinds.TryGetValue((Key(system), body), out bool was) && was == moon)
                return $"body pop-up {matched}: already known as a {(moon ? "moon" : "planet")}{check}{resources}";
            CapturedKinds[(Key(system), body)] = moon;
        }
        try
        {
            using var connection = UserDatabase.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO body_kinds (system_address, planet_index, is_moon, updated_at, source, synced_at, deleted, added_by)
                VALUES ($s, $p, $moon, $now, 'captured', NULL, 0, $by)
                ON CONFLICT(system_address, planet_index) DO UPDATE SET is_moon = $moon, updated_at = $now, synced_at = NULL, deleted = 0";
            cmd.Parameters.AddWithValue("$s", Key(system));
            cmd.Parameters.AddWithValue("$p", body);
            cmd.Parameters.AddWithValue("$moon", moon ? 1 : 0);
            cmd.Parameters.AddWithValue("$now", UserDatabase.Now());
            cmd.Parameters.AddWithValue("$by", GameAccountsService.ActiveKey);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BodyKindsService] {ex.Message}");
        }
        Captured?.Invoke(system);
        return $"body pop-up {matched}: saved as a {(moon ? "moon" : "planet")}{check}{resources}";
    }

    /// <summary>Stores a body's kind as the game showed it (also from the Current System picker).</summary>
    public static void Store(ulong system, int body, bool moon)
    {
        lock (Lock) CapturedKinds[(Key(system), body)] = moon;
        try
        {
            using var connection = UserDatabase.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO body_kinds (system_address, planet_index, is_moon, updated_at, source, synced_at, deleted, added_by)
                VALUES ($s, $p, $moon, $now, 'captured', NULL, 0, $by)
                ON CONFLICT(system_address, planet_index) DO UPDATE SET is_moon = $moon, updated_at = $now, synced_at = NULL, deleted = 0";
            cmd.Parameters.AddWithValue("$s", Key(system));
            cmd.Parameters.AddWithValue("$p", body);
            cmd.Parameters.AddWithValue("$moon", moon ? 1 : 0);
            cmd.Parameters.AddWithValue("$now", UserDatabase.Now());
            cmd.Parameters.AddWithValue("$by", GameAccountsService.ActiveKey);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BodyKindsService] {ex.Message}");
        }
        Captured?.Invoke(system);
    }

    /// <summary>Drops the in-memory copy (after a user.db import).</summary>
    public static void Reload()
    {
        lock (Lock) _captured = null;
    }
}
