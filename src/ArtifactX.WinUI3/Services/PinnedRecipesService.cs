using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Recipes the player has pinned on the Recipes page, kept in user.db
/// (table pinned_recipes, UserDatabase migration step 2). Unpinning is a soft
/// delete (deleted = 1), like every user.db table.
/// </summary>
public static class PinnedRecipesService
{
    private static readonly object Lock = new();
    private static HashSet<string>? _pinned;

    private static HashSet<string> Pinned
    {
        get
        {
            lock (Lock)
            {
                if (_pinned is not null) return _pinned;
                _pinned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    using var connection = UserDatabase.Open();
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT recipe_id FROM pinned_recipes WHERE deleted = 0 ORDER BY pinned_at";
                    using var r = cmd.ExecuteReader();
                    while (r.Read()) _pinned.Add(r.GetString(0));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PinnedRecipesService] Couldn't read pins: {ex.Message}");
                }
                return _pinned;
            }
        }
    }

    public static bool IsPinned(string recipeId) => Pinned.Contains(recipeId);

    public static IReadOnlyCollection<string> All
    {
        get { lock (Lock) return new List<string>(Pinned); }
    }

    /// <summary>Pins or unpins; returns the new state.</summary>
    public static bool Toggle(string recipeId)
    {
        bool pin;
        lock (Lock)
        {
            pin = !Pinned.Contains(recipeId);
            if (pin) Pinned.Add(recipeId);
            else Pinned.Remove(recipeId);
        }

        try
        {
            using var connection = UserDatabase.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO pinned_recipes (recipe_id, pinned_at, updated_at, source, synced_at, deleted)
                VALUES ($id, $now, $now, 'user', NULL, $deleted)
                ON CONFLICT(recipe_id) DO UPDATE SET
                    pinned_at = CASE WHEN $deleted = 0 THEN $now ELSE pinned_at END,
                    updated_at = $now, synced_at = NULL, deleted = $deleted";
            cmd.Parameters.AddWithValue("$id", recipeId);
            cmd.Parameters.AddWithValue("$now", UserDatabase.Now());
            cmd.Parameters.AddWithValue("$deleted", pin ? 0 : 1);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PinnedRecipesService] Couldn't save pin: {ex.Message}");
        }
        return pin;
    }

    /// <summary>Drops the in-memory copy (after a user.db import).</summary>
    public static void Reload()
    {
        lock (Lock) _pinned = null;
    }
}
