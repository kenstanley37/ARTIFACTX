using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ArtifactX.WinUI3.Services;

/// <summary>One player-recorded find (Finds page). PortalCode is the 12-digit
/// code including the planet digit; Galaxy is 1-based (a code alone doesn't
/// say which galaxy). ItemIds are catalog GameIds: items sold, or fish for a fishing spot.</summary>
public sealed record Find
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Type { get; init; } = "";
    public string Title { get; init; } = "";
    public string PortalCode { get; init; } = "";
    public int Galaxy { get; init; } = 1;
    public string Class { get; init; } = "";
    public string Subtype { get; init; } = "";
    public List<string> ItemIds { get; init; } = new();
    public string Location { get; init; } = "";
    public string Notes { get; init; } = "";
    public string CreatedAt { get; init; } = "";
    public string UpdatedAt { get; init; } = "";
}

/// <summary>
/// Finds stored in user.db (table finds, UserDatabase migration step 3). Kept
/// in memory and written through; deleting is a soft delete like every user.db
/// table, so it can sync later. Not tied to any save.
/// </summary>
public static class FindsService
{
    /// <summary>Find types, in the order the page offers them.</summary>
    public static readonly string[] Types =
    {
        "Starship", "Multi-Tool", "Freighter", "Sells items", "Fishing spot", "Freighter upgrades", "Point of interest", "Other",
    };

    public static bool HasClass(string type) => type is "Starship" or "Multi-Tool" or "Freighter";

    /// <summary>Types whose ItemIds hold picked catalog items: items sold, or fish caught.</summary>
    public static bool HasItems(string type) => type is "Sells items" or "Fishing spot";

    private static readonly object Lock = new();
    private static List<Find>? _finds;

    /// <summary>Raised whenever a find is added, modified, deleted, or reloaded.</summary>
    public static event Action? Changed;

    public static IReadOnlyList<Find> All
    {
        get { lock (Lock) return (_finds ??= Load()).ToList(); }
    }

    private static List<Find> Load()
    {
        var finds = new List<Find>();
        try
        {
            using var connection = UserDatabase.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"SELECT id, type, title, portal_code, galaxy, class, subtype, items, location, notes, created_at, updated_at
                                FROM finds WHERE deleted = 0 ORDER BY created_at DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                finds.Add(new Find
                {
                    Id = r.GetString(0), Type = r.GetString(1), Title = r.GetString(2), PortalCode = r.GetString(3),
                    Galaxy = r.GetInt32(4), Class = r.GetString(5), Subtype = r.GetString(6),
                    ItemIds = r.GetString(7).Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                    Location = r.GetString(8), Notes = r.GetString(9), CreatedAt = r.GetString(10), UpdatedAt = r.GetString(11),
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FindsService] Couldn't read finds: {ex.Message}");
        }
        return finds;
    }

    /// <summary>Adds or updates a find (matched by Id).</summary>
    public static Find Save(Find find)
    {
        string now = UserDatabase.Now();
        var saved = find with
        {
            CreatedAt = find.CreatedAt.Length > 0 ? find.CreatedAt : now,
            UpdatedAt = now,
        };

        lock (Lock)
        {
            _finds ??= Load();
            _finds.RemoveAll(f => f.Id == saved.Id);
            _finds.Insert(0, saved);
        }

        try
        {
            using var connection = UserDatabase.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO finds (id, type, title, portal_code, galaxy, class, subtype, items, location, notes,
                                   created_at, updated_at, source, synced_at, deleted, added_by)
                VALUES ($id, $type, $title, $code, $galaxy, $class, $subtype, $items, $location, $notes,
                        $created, $now, 'user', NULL, 0, $added_by)
                ON CONFLICT(id) DO UPDATE SET
                    type = $type, title = $title, portal_code = $code, galaxy = $galaxy, class = $class,
                    subtype = $subtype, items = $items, location = $location, notes = $notes,
                    updated_at = $now, synced_at = NULL, deleted = 0";
            cmd.Parameters.AddWithValue("$id", saved.Id);
            cmd.Parameters.AddWithValue("$type", saved.Type);
            cmd.Parameters.AddWithValue("$title", saved.Title);
            cmd.Parameters.AddWithValue("$code", saved.PortalCode);
            cmd.Parameters.AddWithValue("$galaxy", saved.Galaxy);
            cmd.Parameters.AddWithValue("$class", saved.Class);
            cmd.Parameters.AddWithValue("$subtype", saved.Subtype);
            cmd.Parameters.AddWithValue("$items", string.Join(",", saved.ItemIds));
            cmd.Parameters.AddWithValue("$location", saved.Location);
            cmd.Parameters.AddWithValue("$notes", saved.Notes);
            cmd.Parameters.AddWithValue("$created", saved.CreatedAt);
            cmd.Parameters.AddWithValue("$now", now);
            cmd.Parameters.AddWithValue("$added_by", GameAccountsService.ActiveKey);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FindsService] Couldn't save find: {ex.Message}");
        }

        Changed?.Invoke();
        return saved;
    }

    public static void Delete(string id)
    {
        lock (Lock) _finds?.RemoveAll(f => f.Id == id);
        try
        {
            using var connection = UserDatabase.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE finds SET deleted = 1, updated_at = $now, synced_at = NULL WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$now", UserDatabase.Now());
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FindsService] Couldn't delete find: {ex.Message}");
        }

        Changed?.Invoke();
    }

    /// <summary>Drops the in-memory copy (after a user.db import).</summary>
    public static void Reload()
    {
        lock (Lock) _finds = null;
        Changed?.Invoke();
    }
}
