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
