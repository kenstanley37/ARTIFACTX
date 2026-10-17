using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>What the player recorded about one planet. Empty string = not recorded.</summary>
public sealed class PlanetDetails
{
    public string Weather { get; set; } = "";
    public string Sentinels { get; set; } = "";
    public string Flora { get; set; } = "";
    public string Fauna { get; set; } = "";

    /// <summary>Kinds of fauna on the planet ("0 / 6 Discovered" - 6); 0 = none; null = not known.</summary>
    public int? FaunaTotal { get; set; }

    /// <summary>Resources found there, by name as the game shows them ("Paraffinum").</summary>
    public List<string> Resources { get; set; } = new();

    /// <summary>Free-form notes, one per line item.</summary>
    public List<string> Notes { get; set; } = new();

    public bool IsEmpty => Weather == "" && Sentinels == "" && Flora == "" && Fauna == ""
        && Resources.Count == 0 && Notes.Count == 0;

    public PlanetDetails Copy() => new()
    {
        Weather = Weather, Sentinels = Sentinels, Flora = Flora, Fauna = Fauna, FaunaTotal = FaunaTotal,
        Resources = Resources.ToList(), Notes = Notes.ToList(),
    };
}

/// <summary>
/// Per-planet details the player records from the game's discovery screen -
/// weather, sentinels, flora, fauna, resources and notes - which the save never
/// stores (user request 2026-10-13: the system section is about the system, the
/// planets get their own details). Kept in user.db (tables planet_details and
/// planet_notes, UserDatabase step 8), keyed like planet_names: system address
/// (hex) + planet index. Held in memory too, since the finder looks them up for
/// every planet on each refresh. Removing is a soft delete, like every user.db table.
/// </summary>
public static class PlanetDetailsService
{
    private static readonly object Lock = new();
    private static Dictionary<string, PlanetDetails>? _details;

    /// <summary>Raised (on any thread) when details arrive from outside the Planets page -
    /// read off a game screen (PlanetPageReader) - so an open page can show them.</summary>
    public static event Action<ulong, int>? Captured;

    private static string SystemKey(ulong systemAddress) => systemAddress.ToString("X", CultureInfo.InvariantCulture);
    private static string Key(string system, int planet) => $"{system}|{planet}";

    private static Dictionary<string, PlanetDetails> Details
    {
        get
        {
            lock (Lock)
            {
                if (_details is not null) return _details;
                _details = new(StringComparer.OrdinalIgnoreCase);
                try
                {
                    using var connection = UserDatabase.Open();
                    PlanetDetails For(string system, int planet)
                    {
                        string key = Key(system, planet);
                        if (!_details.TryGetValue(key, out var d)) _details[key] = d = new PlanetDetails();
                        return d;
                    }
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = "SELECT system_address, planet_index, weather, sentinels, flora, fauna, resources, fauna_total FROM planet_details WHERE deleted = 0";
                        using var r = cmd.ExecuteReader();
                        while (r.Read())
                        {
                            var d = For(r.GetString(0), r.GetInt32(1));
                            d.Weather = r.GetString(2);
                            d.Sentinels = r.GetString(3);
                            d.Flora = r.GetString(4);
                            d.Fauna = r.GetString(5);
                            d.Resources = SplitResources(r.GetString(6));
                            d.FaunaTotal = r.IsDBNull(7) ? null : r.GetInt32(7);
                        }
                    }
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = "SELECT system_address, planet_index, text FROM planet_notes WHERE deleted = 0 ORDER BY created_at, rowid";
                        using var r = cmd.ExecuteReader();
                        while (r.Read()) For(r.GetString(0), r.GetInt32(1)).Notes.Add(r.GetString(2));
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PlanetDetailsService] Couldn't read planet details: {ex.Message}");
                }
                return _details;
            }
        }
    }

    private static List<string> SplitResources(string text) =>
        text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    /// <summary>The planet's recorded details, or null when nothing is recorded.</summary>
    public static PlanetDetails? Get(ulong systemAddress, int planet)
    {
        lock (Lock) return Details.TryGetValue(Key(SystemKey(systemAddress), planet), out var d) && !d.IsEmpty ? d : null;
    }

    /// <summary>Every planet's recorded details (for building search filters).</summary>
    public static IReadOnlyList<PlanetDetails> All
    {
        get { lock (Lock) return Details.Values.Where(d => !d.IsEmpty).ToList(); }
    }

    /// <summary>Every planet's recorded details with the planet they belong to
    /// (Settlement materials: "where have I seen this resource?").</summary>
    public static List<(ulong System, int Planet, PlanetDetails Details)> AllWithPlanets
    {
        get
        {
            lock (Lock)
                return Details.Where(kv => !kv.Value.IsEmpty).Select(kv =>
                {
                    var parts = kv.Key.Split('|');
                    return (ulong.Parse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                            int.Parse(parts[1], CultureInfo.InvariantCulture), kv.Value);
                }).ToList();
        }
    }

    /// <summary>Replaces what's recorded for a planet.</summary>
    public static void Save(ulong systemAddress, int planet, PlanetDetails details, string source = "user")
    {
        string system = SystemKey(systemAddress);
        var saved = details.Copy();
        lock (Lock) Details[Key(system, planet)] = saved;

        try
        {
            using var connection = UserDatabase.Open();
            using var tx = connection.BeginTransaction();
            string now = UserDatabase.Now();

            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO planet_details (system_address, planet_index, weather, sentinels, flora, fauna, resources, fauna_total,
                                                updated_at, source, synced_at, deleted, added_by)
                    VALUES ($s, $p, $weather, $sentinels, $flora, $fauna, $resources, $faunaTotal, $now, $source, NULL, $deleted, $added_by)
                    ON CONFLICT(system_address, planet_index) DO UPDATE SET
                        weather = $weather, sentinels = $sentinels, flora = $flora, fauna = $fauna, resources = $resources,
                        fauna_total = $faunaTotal,
                        updated_at = $now, source = $source, synced_at = NULL, deleted = $deleted";
                cmd.Parameters.AddWithValue("$s", system);
                cmd.Parameters.AddWithValue("$p", planet);
                cmd.Parameters.AddWithValue("$weather", saved.Weather);
                cmd.Parameters.AddWithValue("$sentinels", saved.Sentinels);
                cmd.Parameters.AddWithValue("$flora", saved.Flora);
                cmd.Parameters.AddWithValue("$fauna", saved.Fauna);
                cmd.Parameters.AddWithValue("$resources", string.Join(", ", saved.Resources));
                cmd.Parameters.AddWithValue("$faunaTotal", (object?)saved.FaunaTotal ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$now", now);
                cmd.Parameters.AddWithValue("$source", source);
                cmd.Parameters.AddWithValue("$added_by", GameAccountsService.ActiveKey);
                bool noFields = saved.Weather == "" && saved.Sentinels == "" && saved.Flora == "" && saved.Fauna == "" && saved.Resources.Count == 0;
                cmd.Parameters.AddWithValue("$deleted", noFields ? 1 : 0);
                cmd.ExecuteNonQuery();
            }

            // Notes: keep rows whose text is still there (in order, duplicates
            // allowed), soft-delete the rest, insert the new ones.
            var existing = new List<(string Id, string Text)>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT id, text FROM planet_notes WHERE system_address = $s AND planet_index = $p AND deleted = 0 ORDER BY created_at, rowid";
                cmd.Parameters.AddWithValue("$s", system);
                cmd.Parameters.AddWithValue("$p", planet);
                using var r = cmd.ExecuteReader();
                while (r.Read()) existing.Add((r.GetString(0), r.GetString(1)));
            }
            var unmatched = existing.ToList();
            var toInsert = new List<string>();
            foreach (var text in saved.Notes)
            {
                int match = unmatched.FindIndex(n => n.Text == text);
                if (match >= 0) unmatched.RemoveAt(match);
                else toInsert.Add(text);
            }
            foreach (var (id, _) in unmatched)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "UPDATE planet_notes SET deleted = 1, updated_at = $now, synced_at = NULL WHERE id = $id";
                cmd.Parameters.AddWithValue("$id", id);
                cmd.Parameters.AddWithValue("$now", now);
                cmd.ExecuteNonQuery();
            }
            foreach (var text in toInsert)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"INSERT INTO planet_notes (id, system_address, planet_index, text, created_at, updated_at, source, synced_at, deleted, added_by)
                                    VALUES ($id, $s, $p, $text, $now, $now, $source, NULL, 0, $added_by)";
                cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
                cmd.Parameters.AddWithValue("$s", system);
                cmd.Parameters.AddWithValue("$p", planet);
                cmd.Parameters.AddWithValue("$text", text);
                cmd.Parameters.AddWithValue("$now", now);
                cmd.Parameters.AddWithValue("$source", source);
                cmd.Parameters.AddWithValue("$added_by", GameAccountsService.ActiveKey);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlanetDetailsService] Couldn't save planet details: {ex.Message}");
        }
        if (source == "captured") Captured?.Invoke(systemAddress, planet);
    }

    /// <summary>Drops the in-memory copy (after a user.db import).</summary>
    public static void Reload()
    {
        lock (Lock) _details = null;
    }
}
