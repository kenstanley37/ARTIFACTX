using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Names and avatars for the Steam accounts on this PC, read from Steam's own
/// local files (never the network): config\loginusers.vdf maps each SteamID64
/// to its current Steam name, and config\avatarcache\&lt;id&gt;.png is the
/// avatar Steam last showed. Lets Save Selection say "Steam · Neophyte"
/// instead of five identical "Steam" cards. Anything missing just means no
/// name or picture - callers fall back to the save's own player name.
/// </summary>
public static partial class SteamAccountsService
{
    private static Dictionary<string, string>? _names;
    private static string? _steamRoot;

    [GeneratedRegex(@"""(\d{17})""\s*\{([^{}]*)\}")]
    private static partial Regex UserBlockPattern();

    [GeneratedRegex(@"""PersonaName""\s*""([^""]*)""")]
    private static partial Regex PersonaNamePattern();

    /// <summary>The Steam name for a SteamID64, or null.</summary>
    public static string? PersonaName(string steamId)
    {
        _names ??= LoadNames();
        return _names.TryGetValue(steamId, out var name) && name.Length > 0 ? name : null;
    }

    /// <summary>Path to the account's cached avatar picture, or null.</summary>
    public static string? AvatarPath(string steamId)
    {
        if (SteamRoot() is not string root) return null;
        string path = Path.Combine(root, "config", "avatarcache", steamId + ".png");
        return File.Exists(path) ? path : null;
    }

    /// <summary>No Man's Sky's Steam app id (store.steampowered.com/app/275850): Steam files the game's screenshots
    /// (F12 and photo mode) under userdata\&lt;account&gt;\760\remote\275850\screenshots - checked on the owner's PC,
    /// 2026-10-17.</summary>
    private const string NmsAppId = "275850";

    /// <summary>One No Man's Sky screenshot Steam keeps, with Steam's own small copy when it has one.</summary>
    public sealed record Screenshot(string Path, string ThumbnailPath, DateTime Taken);

    /// <summary>Every No Man's Sky screenshot of every Steam account on this PC, newest first. Reads folders
    /// only; call off the UI thread.</summary>
    public static List<Screenshot> NmsScreenshots()
    {
        var shots = new List<Screenshot>();
        try
        {
            if (SteamRoot() is not string root || !Directory.Exists(Path.Combine(root, "userdata"))) return shots;
            foreach (var account in Directory.EnumerateDirectories(Path.Combine(root, "userdata")))
            {
                string folder = Path.Combine(account, "760", "remote", NmsAppId, "screenshots");
                if (!Directory.Exists(folder)) continue;
                foreach (var file in new DirectoryInfo(folder).EnumerateFiles("*.jpg"))
                {
                    string thumb = Path.Combine(folder, "thumbnails", file.Name);
                    shots.Add(new Screenshot(file.FullName, File.Exists(thumb) ? thumb : file.FullName, file.LastWriteTime));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A folder Steam is writing to or we can't read: show what was found.
        }
        shots.Sort((a, b) => b.Taken.CompareTo(a.Taken));
        return shots;
    }

    private static Dictionary<string, string> LoadNames()
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            if (SteamRoot() is not string root) return names;
            string file = Path.Combine(root, "config", "loginusers.vdf");
            if (!File.Exists(file)) return names;

            foreach (Match user in UserBlockPattern().Matches(File.ReadAllText(file)))
            {
                var persona = PersonaNamePattern().Match(user.Groups[2].Value);
                if (persona.Success) names[user.Groups[1].Value] = persona.Groups[1].Value;
            }
        }
        catch (Exception)
        {
            // Unreadable or unexpected file: no names, cards fall back.
        }
        return names;
    }

    private static string? SteamRoot()
    {
        if (_steamRoot is not null) return _steamRoot.Length > 0 ? _steamRoot : null;
        try
        {
            string? path = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
            _steamRoot = path is { Length: > 0 } && Directory.Exists(path) ? path : "";
        }
        catch (Exception)
        {
            _steamRoot = "";
        }
        return _steamRoot.Length > 0 ? _steamRoot : null;
    }
}
