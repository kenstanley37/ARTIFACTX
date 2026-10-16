using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ArtifactX.Core.Guides;

namespace ArtifactX.WinUI3.Services;

/// <summary>One saved creator video (Guides page). Url is the cleaned-up link
/// from VideoLink.Parse; ItemIds are catalog GameIds the video is about.</summary>
public sealed record Guide
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Url { get; init; } = "";
    public string Platform { get; init; } = "";
    public string Title { get; init; } = "";
    public string Creator { get; init; } = "";
    public string ThumbnailUrl { get; init; } = "";
    public string Topic { get; init; } = "";
    public List<string> ItemIds { get; init; } = new();
    public string Notes { get; init; } = "";
    public string CreatedAt { get; init; } = "";
    public string UpdatedAt { get; init; } = "";
}

/// <summary>Title, creator and thumbnail a platform reports for a video.</summary>
public sealed record VideoDetails(string Title, string Creator, string ThumbnailUrl);

/// <summary>
/// Creator video links kept in user.db (table guides, UserDatabase migration
/// step 5) - the player's own list until the ArtifactX API can share them.
/// Deleting is a soft delete like every user.db table. Details (title,
/// creator, thumbnail) come from the platform's public oEmbed endpoint for
/// YouTube and TikTok; Twitch has none without an API key, so those are typed.
/// </summary>
public static class GuidesService
{
    /// <summary>Topics, in the order the page offers them.</summary>
    public static readonly string[] Topics =
    {
        "Getting started", "Money and Units", "Crafting and refining", "Base building", "Settlements",
        "Starships", "Multi-Tools", "Freighters and frigates", "Exploration", "Fishing", "Expeditions", "Combat", "Other",
    };

    private static readonly object Lock = new();
    private static List<Guide>? _guides;

    public static IReadOnlyList<Guide> All
    {
        get { lock (Lock) return (_guides ??= Load()).ToList(); }
    }

    /// <summary>Saved guides tagged with this catalog item.</summary>
    public static IReadOnlyList<Guide> About(string itemId) =>
        All.Where(g => g.ItemIds.Contains(itemId, StringComparer.OrdinalIgnoreCase)).ToList();

    private static List<Guide> Load()
    {
        var guides = new List<Guide>();
        try
        {
            using var connection = UserDatabase.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"SELECT id, url, platform, title, creator, thumbnail_url, topic, items, notes, created_at, updated_at
                                FROM guides WHERE deleted = 0 ORDER BY created_at DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                guides.Add(new Guide
                {
                    Id = r.GetString(0), Url = r.GetString(1), Platform = r.GetString(2), Title = r.GetString(3),
                    Creator = r.GetString(4), ThumbnailUrl = r.GetString(5), Topic = r.GetString(6),
                    ItemIds = r.GetString(7).Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                    Notes = r.GetString(8), CreatedAt = r.GetString(9), UpdatedAt = r.GetString(10),
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GuidesService] Couldn't read guides: {ex.Message}");
        }
        return guides;
    }

    /// <summary>Adds or updates a guide (matched by Id).</summary>
    public static Guide Save(Guide guide)
    {
        string now = UserDatabase.Now();
        var saved = guide with
        {
            CreatedAt = guide.CreatedAt.Length > 0 ? guide.CreatedAt : now,
            UpdatedAt = now,
        };

        lock (Lock)
        {
            _guides ??= Load();
            _guides.RemoveAll(g => g.Id == saved.Id);
            _guides.Insert(0, saved);
        }

        try
        {
            using var connection = UserDatabase.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO guides (id, url, platform, title, creator, thumbnail_url, topic, items, notes,
                                    created_at, updated_at, source, synced_at, deleted)
                VALUES ($id, $url, $platform, $title, $creator, $thumb, $topic, $items, $notes,
                        $created, $now, 'user', NULL, 0)
                ON CONFLICT(id) DO UPDATE SET
                    url = $url, platform = $platform, title = $title, creator = $creator, thumbnail_url = $thumb,
                    topic = $topic, items = $items, notes = $notes, updated_at = $now, synced_at = NULL, deleted = 0";
            cmd.Parameters.AddWithValue("$id", saved.Id);
            cmd.Parameters.AddWithValue("$url", saved.Url);
            cmd.Parameters.AddWithValue("$platform", saved.Platform);
            cmd.Parameters.AddWithValue("$title", saved.Title);
            cmd.Parameters.AddWithValue("$creator", saved.Creator);
            cmd.Parameters.AddWithValue("$thumb", saved.ThumbnailUrl);
            cmd.Parameters.AddWithValue("$topic", saved.Topic);
            cmd.Parameters.AddWithValue("$items", string.Join(",", saved.ItemIds));
            cmd.Parameters.AddWithValue("$notes", saved.Notes);
            cmd.Parameters.AddWithValue("$created", saved.CreatedAt);
            cmd.Parameters.AddWithValue("$now", now);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GuidesService] Couldn't save guide: {ex.Message}");
        }
        return saved;
    }

    public static void Delete(string id)
    {
        lock (Lock) _guides?.RemoveAll(g => g.Id == id);
        try
        {
            using var connection = UserDatabase.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE guides SET deleted = 1, updated_at = $now, synced_at = NULL WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$now", UserDatabase.Now());
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GuidesService] Couldn't delete guide: {ex.Message}");
        }
    }

    /// <summary>Drops the in-memory copy (after a user.db import).</summary>
    public static void Reload()
    {
        lock (Lock) _guides = null;
    }

    /// <summary>Asks YouTube or TikTok for a video's title, creator and
    /// thumbnail (their public oEmbed endpoints - no account or key). Null for
    /// Twitch, on any failure, or when offline; the player can type them.</summary>
    public static async Task<VideoDetails?> FetchDetailsAsync(string platform, string url)
    {
        string? endpoint = platform switch
        {
            VideoLink.YouTube => $"https://www.youtube.com/oembed?format=json&url={Uri.EscapeDataString(url)}",
            VideoLink.TikTok => $"https://www.tiktok.com/oembed?url={Uri.EscapeDataString(url)}",
            _ => null,
        };
        if (endpoint is null) return null;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ArtifactX-Guides");
            string json = await client.GetStringAsync(endpoint);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string Text(string name) => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            string thumb = Text("thumbnail_url");
            // Only https images are shown.
            if (!Uri.TryCreate(thumb, UriKind.Absolute, out var thumbUri) || thumbUri.Scheme != Uri.UriSchemeHttps) thumb = "";
            return new VideoDetails(Text("title").Trim(), Text("author_name").Trim(), thumb);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GuidesService] Couldn't fetch details for {url}: {ex.Message}");
            return null;
        }
    }
}
