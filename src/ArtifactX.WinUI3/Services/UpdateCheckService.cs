using ArtifactX.Core;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

public enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    NoReleasesYet,
    Error,
}

public sealed class UpdateCheckResult
{
    public required UpdateCheckStatus Status { get; init; }
    public required string Message { get; init; }
    public string? ReleaseUrl { get; init; }
}

/// <summary>
/// Checks GitHub's public "latest release" API for a version newer than
/// AppVersionService.Current. Manual-download-only by design - never
/// auto-installs anything, since this app isn't code-signed yet (see
/// project_signpath_code_signing memory) and silently replacing an unsigned
/// exe would be its own risk on top of the one this app already carries.
///
/// RefreshAsync caches its result in LastResult and raises Changed, so
/// AppTitleBar's badge and GeneralPage's About section both reflect one
/// shared check instead of hitting the API independently.
/// </summary>
public static class UpdateCheckService
{
    // The release list, not /releases/latest: that one leaves out pre-releases,
    // so an alpha build would never see the next alpha (2026-10-15).
    private const string ReleasesApiUrl = "https://api.github.com/repos/kenstanley37/ARTIFACTX/releases?per_page=30";
    private const string ReleasesPageUrl = "https://github.com/kenstanley37/ARTIFACTX/releases";

    public static UpdateCheckResult? LastResult { get; private set; }
    public static event EventHandler? Changed;

    public static async Task RefreshAsync()
    {
        LastResult = await CheckAsync();
        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static async Task<UpdateCheckResult> CheckAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ArtifactX-UpdateCheck");

            using var response = await client.GetAsync(ReleasesApiUrl);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync();

            // JsonDocument (DOM parse), not JsonSerializer.Deserialize<T>, so
            // nothing here depends on reflection.
            using var doc = JsonDocument.Parse(json);
            var current = AppVersionService.Current;
            ReleaseVersion? newest = null;
            string releaseUrl = ReleasesPageUrl;
            foreach (var release in doc.RootElement.EnumerateArray())
            {
                if (release.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;
                var version = ReleaseVersion.TryParse(release.TryGetProperty("tag_name", out var tag) ? tag.GetString() : null);
                if (version is null) continue;
                // Someone on a full release isn't offered alphas and betas.
                if (version.IsPreRelease && !current.IsPreRelease) continue;
                if (newest is null || version > newest)
                {
                    newest = version;
                    releaseUrl = release.TryGetProperty("html_url", out var url) ? url.GetString() ?? ReleasesPageUrl : ReleasesPageUrl;
                }
            }

            if (newest is null)
                return new UpdateCheckResult { Status = UpdateCheckStatus.NoReleasesYet, Message = "No releases published yet." };
            if (newest > current)
            {
                return new UpdateCheckResult
                {
                    Status = UpdateCheckStatus.UpdateAvailable,
                    Message = $"Version {newest} is available (you have {AppVersionService.DisplayVersion}).",
                    ReleaseUrl = releaseUrl,
                };
            }
            return new UpdateCheckResult { Status = UpdateCheckStatus.UpToDate, Message = "You're up to date." };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateCheckService] Check failed: {ex}");
            return new UpdateCheckResult { Status = UpdateCheckStatus.Error, Message = "Couldn't check for updates (no internet connection?)." };
        }
    }
}
