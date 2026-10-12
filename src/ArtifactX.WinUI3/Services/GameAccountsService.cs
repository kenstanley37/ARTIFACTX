using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// The person behind the data. One profile per PC for now (created on first
/// use); every user.db row with profile_id '' belongs to it. Later this is the
/// player's ArtifactX account, with their game accounts linked to it - one
/// person, many logins. A profile picker only matters once a second profile
/// exists, so none is shown until then.
/// </summary>
public static class ProfileService
{
    private static string? _primaryId;

    /// <summary>This PC's primary profile, created on first use.</summary>
    public static string PrimaryProfileId
    {
        get
        {
            if (_primaryId is not null) return _primaryId;
            try
            {
                using var connection = UserDatabase.Open();
                using (var read = connection.CreateCommand())
                {
                    read.CommandText = "SELECT id FROM profiles WHERE deleted = 0 ORDER BY created_at LIMIT 1";
                    if (read.ExecuteScalar() is string existing) return _primaryId = existing;
                }
                string id = Guid.NewGuid().ToString("N");
                using var insert = connection.CreateCommand();
                insert.CommandText = "INSERT INTO profiles (id, name, created_at, updated_at) VALUES ($id, '', $now, $now)";
                insert.Parameters.AddWithValue("$id", id);
                insert.Parameters.AddWithValue("$now", UserDatabase.Now());
                insert.ExecuteNonQuery();
                return _primaryId = id;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProfileService] {ex.Message}");
                return "";
            }
        }
    }

    public static void Reload() => _primaryId = null;
}

/// <summary>
/// The game accounts Save Selection finds, linked to the profile. Keys are
/// stable across PCs and folder moves: "steam:&lt;SteamID64&gt;", "gog:&lt;id&gt;";
/// anything else (Xbox, a browsed folder) is "folder:&lt;hash of its path&gt;".
/// Holds each account's nickname (the card name the player chose) and the
/// name the game knows them by.
/// </summary>
public static class GameAccountsService
{
    private static readonly object Lock = new();
    private static Dictionary<string, string>? _nicknames;

    /// <summary>The account key for a save folder.</summary>
    public static string KeyFor(string accountFolder)
    {
        string leaf = Path.GetFileName(accountFolder.TrimEnd('\\', '/'));
        if (leaf.StartsWith("st_", StringComparison.OrdinalIgnoreCase) && leaf.Length > 3) return "steam:" + leaf[3..];
        if (leaf.StartsWith("gog_", StringComparison.OrdinalIgnoreCase) && leaf.Length > 4) return "gog:" + leaf[4..];
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(accountFolder.ToUpperInvariant()));
        return "folder:" + Convert.ToHexString(hash)[..8];
    }

    /// <summary>The account of the save folder in use, or "" when none.</summary>
    public static string ActiveKey =>
        SaveSessionManager.ActivePlatformFolder is string folder ? KeyFor(folder) : "";

    /// <summary>Records (or refreshes) an account seen in Save Selection.</summary>
    public static void Seen(string accountFolder, string platform, string? accountId, string? displayName)
    {
        try
        {
            using var connection = UserDatabase.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO game_accounts (id, profile_id, platform, account_id, display_name, nickname, first_seen_at, last_seen_at, updated_at)
                VALUES ($id, '', $platform, $account, $name, '', $now, $now, $now)
                ON CONFLICT(id) DO UPDATE SET
                    last_seen_at = $now,
                    display_name = CASE WHEN $name <> '' THEN $name ELSE display_name END";
            cmd.Parameters.AddWithValue("$id", KeyFor(accountFolder));
            cmd.Parameters.AddWithValue("$platform", platform);
            cmd.Parameters.AddWithValue("$account", accountId ?? "");
            cmd.Parameters.AddWithValue("$name", displayName ?? "");
            cmd.Parameters.AddWithValue("$now", UserDatabase.Now());
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameAccountsService] {ex.Message}");
        }
    }

    public static string? Nickname(string accountFolder)
    {
        lock (Lock)
        {
            _nicknames ??= LoadNicknames();
            return _nicknames.TryGetValue(KeyFor(accountFolder), out var name) && name.Length > 0 ? name : null;
        }
    }

    public static void SetNickname(string accountFolder, string? nickname)
    {
        string key = KeyFor(accountFolder);
        string value = nickname?.Trim() ?? "";
        lock (Lock)
        {
            _nicknames ??= LoadNicknames();
            _nicknames[key] = value;
        }
        try
        {
            using var connection = UserDatabase.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO game_accounts (id, profile_id, platform, nickname, first_seen_at, last_seen_at, updated_at, source)
                VALUES ($id, '', '', $nick, $now, $now, $now, 'user')
                ON CONFLICT(id) DO UPDATE SET nickname = $nick, updated_at = $now, synced_at = NULL";
            cmd.Parameters.AddWithValue("$id", key);
            cmd.Parameters.AddWithValue("$nick", value);
            cmd.Parameters.AddWithValue("$now", UserDatabase.Now());
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameAccountsService] {ex.Message}");
        }
    }

    /// <summary>The name to show for an account key: its nickname, else the
    /// name the game knows the player by, else null.</summary>
    public static string? Name(string key)
    {
        try
        {
            using var connection = UserDatabase.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT CASE WHEN nickname <> '' THEN nickname ELSE display_name END FROM game_accounts WHERE id = $id AND deleted = 0";
            cmd.Parameters.AddWithValue("$id", key);
            return cmd.ExecuteScalar() is string name && name.Length > 0 ? name : null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameAccountsService] {ex.Message}");
            return null;
        }
    }

    public static void Reload()
    {
        lock (Lock) _nicknames = null;
    }

    private static Dictionary<string, string> LoadNicknames()
    {
        LegacyDataImporter.RunOnce();
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var connection = UserDatabase.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, nickname FROM game_accounts WHERE deleted = 0";
            using var r = cmd.ExecuteReader();
            while (r.Read()) names[r.GetString(0)] = r.GetString(1);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameAccountsService] {ex.Message}");
        }
        return names;
    }
}
