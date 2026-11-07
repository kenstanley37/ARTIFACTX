using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>A Ship Designer design: the builder's choices, by the game's own ids
/// (type, slot -> item, style option, colour group -> swatch index) and the seed
/// that names it. Ids, not looks, so a design still loads after a game update.</summary>
public sealed record ShipDesign(
    string Id, string Name, string Type, Dictionary<string, string> Parts,
    string? Style, Dictionary<string, int> Colours, string Seed, string CreatedAt);

/// <summary>A saved design and its picture (PNG), if one has been taken.</summary>
public sealed record SavedDesign(ShipDesign Design, byte[]? Thumbnail);

/// <summary>
/// Designs saved on the Ship Designer page, in user.db (table ship_designs,
/// UserDatabase migration steps 12-13). Deleting is a soft delete (deleted = 1),
/// like every user.db table.
/// </summary>
public static class ShipDesignsService
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static Task<List<SavedDesign>> LoadAllAsync() => Task.Run(() =>
    {
        var results = new List<SavedDesign>();
        try
        {
            using var connection = UserDatabase.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT payload, thumbnail FROM ship_designs WHERE deleted = 0 ORDER BY updated_at DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                try
                {
                    if (JsonSerializer.Deserialize<ShipDesign>(r.GetString(0), Json) is { } design)
                        results.Add(new SavedDesign(design, r.IsDBNull(1) ? null : (byte[])r.GetValue(1)));
                }
                catch (JsonException)
                {
                    // Skip one unreadable design rather than failing the whole list.
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ShipDesignsService] {ex.Message}");
        }
        return results;
    });

    /// <summary>Adds the design, or replaces the one with its id. A null thumbnail
    /// keeps the picture it has.</summary>
    public static Task SaveAsync(ShipDesign design, byte[]? thumbnail) => Task.Run(() =>
    {
        using var connection = UserDatabase.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO ship_designs (id, profile_id, name, ship_type, payload, created_at, updated_at, thumbnail)
            VALUES ($id, '', $name, $type, $payload, $created, $now, $thumb)
            ON CONFLICT(id) DO UPDATE SET name = $name, ship_type = $type, payload = $payload,
                thumbnail = COALESCE($thumb, thumbnail), updated_at = $now, synced_at = NULL, deleted = 0";
        cmd.Parameters.AddWithValue("$id", design.Id);
        cmd.Parameters.AddWithValue("$name", design.Name);
        cmd.Parameters.AddWithValue("$type", design.Type);
        cmd.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(design, Json));
        cmd.Parameters.AddWithValue("$created", design.CreatedAt);
        cmd.Parameters.AddWithValue("$now", UserDatabase.Now());
        cmd.Parameters.AddWithValue("$thumb", (object?)thumbnail ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    });

    /// <summary>Gives a design its picture (one saved before pictures existed).</summary>
    public static Task SetThumbnailAsync(string id, byte[] thumbnail) => Task.Run(() =>
    {
        using var connection = UserDatabase.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE ship_designs SET thumbnail = $thumb WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$thumb", thumbnail);
        cmd.ExecuteNonQuery();
    });

    public static Task DeleteAsync(string id) => Task.Run(() =>
    {
        using var connection = UserDatabase.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE ship_designs SET deleted = 1, updated_at = $now, synced_at = NULL WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$now", UserDatabase.Now());
        cmd.ExecuteNonQuery();
    });
}
