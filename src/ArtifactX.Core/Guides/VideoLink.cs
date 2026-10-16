using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace ArtifactX.Core.Guides;

/// <summary>
/// Checks a creator video link for the Guides page. Only YouTube, Twitch and
/// TikTok are accepted (agreed 2026-10-11: those platforms already moderate
/// what's posted). The host is checked exactly - never by substring, so
/// "youtube.com.evil.example" or "notyoutube.com" are refused - and the link
/// is rewritten to one plain https form so the same video isn't saved twice.
/// </summary>
public static partial class VideoLink
{
    public const string YouTube = "YouTube";
    public const string Twitch = "Twitch";
    public const string TikTok = "TikTok";

    [GeneratedRegex(@"^[A-Za-z0-9_-]{11}$")]
    private static partial Regex YouTubeId();

    [GeneratedRegex(@"^[A-Za-z0-9_-]{1,100}$")]
    private static partial Regex Slug();

    [GeneratedRegex(@"^\d{1,25}$")]
    private static partial Regex Digits();

    [GeneratedRegex(@"^@[A-Za-z0-9_.]{1,40}$")]
    private static partial Regex TikTokUser();

    /// <summary>Platform and cleaned-up link, or an error a player can act on.</summary>
    public sealed record Result(bool Ok, string Platform, string Url, string Error);

    public static Result Parse(string? input)
    {
        string text = (input ?? "").Trim();
        if (text.Length == 0) return Fail("Paste a link to a YouTube, Twitch or TikTok video.");
        if (!text.Contains("://", StringComparison.Ordinal)) text = "https://" + text;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return Fail("That isn't a web link.");
        if (!string.IsNullOrEmpty(uri.UserInfo)) return Fail("That link isn't allowed.");

        string host = uri.Host.ToLowerInvariant();
        foreach (var prefix in new[] { "www.", "m.", "music." })
            if (host.StartsWith(prefix, StringComparison.Ordinal)) { host = host[prefix.Length..]; break; }
        string[] parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        switch (host)
        {
            case "youtube.com":
            {
                string? id = parts.Length >= 2 && parts[0] is "shorts" or "live" or "embed" ? parts[1]
                    : parts.Length == 1 && parts[0] == "watch" ? QueryValue(uri, "v")
                    : null;
                return id is not null && YouTubeId().IsMatch(id)
                    ? Ok(YouTube, $"https://www.youtube.com/watch?v={id}")
                    : Fail("That YouTube link doesn't point to a video.");
            }
            case "youtu.be":
                return parts.Length >= 1 && YouTubeId().IsMatch(parts[0])
                    ? Ok(YouTube, $"https://www.youtube.com/watch?v={parts[0]}")
                    : Fail("That YouTube link doesn't point to a video.");

            case "twitch.tv":
                if (parts.Length == 2 && parts[0] == "videos" && Digits().IsMatch(parts[1]))
                    return Ok(Twitch, $"https://www.twitch.tv/videos/{parts[1]}");
                if (parts.Length == 3 && parts[1] == "clip" && Slug().IsMatch(parts[0]) && Slug().IsMatch(parts[2]))
                    return Ok(Twitch, $"https://www.twitch.tv/{parts[0]}/clip/{parts[2]}");
                return Fail("Use a Twitch video or clip link (live channels change, so they can't be saved).");
            case "clips.twitch.tv":
                return parts.Length == 1 && Slug().IsMatch(parts[0])
                    ? Ok(Twitch, $"https://clips.twitch.tv/{parts[0]}")
                    : Fail("That Twitch clip link doesn't look right.");

            case "tiktok.com":
                if (parts.Length == 3 && TikTokUser().IsMatch(parts[0]) && parts[1] == "video" && Digits().IsMatch(parts[2]))
                    return Ok(TikTok, $"https://www.tiktok.com/{parts[0]}/video/{parts[2]}");
                if (parts.Length == 2 && parts[0] == "t" && Slug().IsMatch(parts[1]))
                    return Ok(TikTok, $"https://www.tiktok.com/t/{parts[1]}");
                return Fail("That TikTok link doesn't point to a video.");
            case "vm.tiktok.com":
            case "vt.tiktok.com":
                return parts.Length == 1 && Slug().IsMatch(parts[0])
                    ? Ok(TikTok, $"https://{host}/{parts[0]}")
                    : Fail("That TikTok link doesn't look right.");

            default:
                return Fail("Only YouTube, Twitch and TikTok videos can be added.");
        }
    }

    private static string? QueryValue(Uri uri, string key) =>
        uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2 && p[0] == key)
            .Select(p => Uri.UnescapeDataString(p[1]))
            .FirstOrDefault();

    private static Result Ok(string platform, string url) => new(true, platform, url, "");
    private static Result Fail(string error) => new(false, "", "", error);
}
