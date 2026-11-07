using ArtifactX.Core;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

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
    public string? NewVersion { get; init; }
    public string? ReleaseNotes { get; init; }
}

/// <summary>
/// Checks GitHub Releases for a version newer than AppVersionService.Current,
/// and orchestrates downloading and applying updates via Velopack's UpdateManager.
///
/// Player data (user.db, templates, backups) resides strictly in %AppData%\ArtifactX,
/// completely isolated from %LocalAppData%\ArtifactX where Velopack manages app binaries.
/// </summary>
public static class UpdateCheckService
{
    private const string GithubRepoUrl = "https://github.com/kenstanley37/ARTIFACTX";
    private const string ReleasesApiUrl = "https://api.github.com/repos/kenstanley37/ARTIFACTX/releases?per_page=30";
    private const string ReleasesPageUrl = "https://github.com/kenstanley37/ARTIFACTX/releases";

    public static UpdateCheckResult? LastResult { get; private set; }
    public static event EventHandler? Changed;

    public static bool IsDownloading { get; private set; }
    public static int DownloadProgress { get; private set; }
    public static event Action<int>? DownloadProgressChanged;

    /// <summary>
    /// Whether the app is running as an installed Velopack package.
    /// Unpackaged development builds (Visual Studio / CLI) report false.
    /// </summary>
    public static bool IsInstalled
    {
        get
        {
            try
            {
                var mgr = new UpdateManager(new GithubSource(GithubRepoUrl, null, false));
                return mgr.IsInstalled;
            }
            catch
            {
                return false;
            }
        }
    }

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

            using var doc = JsonDocument.Parse(json);
            var current = AppVersionService.Current;
            ReleaseVersion? newest = null;
            string releaseUrl = ReleasesPageUrl;
            string? releaseNotes = null;

            foreach (var release in doc.RootElement.EnumerateArray())
            {
                if (release.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;
                var version = ReleaseVersion.TryParse(release.TryGetProperty("tag_name", out var tag) ? tag.GetString() : null);
                if (version is null) continue;

                // Someone on a full release isn't offered pre-releases.
                if (version.IsPreRelease && !current.IsPreRelease) continue;

                if (newest is null || version > newest)
                {
                    newest = version;
                    releaseUrl = release.TryGetProperty("html_url", out var url) ? url.GetString() ?? ReleasesPageUrl : ReleasesPageUrl;
                    releaseNotes = release.TryGetProperty("body", out var body) ? body.GetString() : null;
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
                    NewVersion = newest.ToString(),
                    ReleaseNotes = releaseNotes,
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

    /// <summary>
    /// Downloads the available update and restarts ArtifactX.
    /// If running unpackaged, opens the GitHub release page instead.
    /// </summary>
    public static async Task<bool> DownloadAndRestartAsync(Action<string>? statusCallback = null)
    {
        if (IsDownloading) return false;

        if (!IsInstalled)
        {
            if (LastResult?.ReleaseUrl is string url)
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(url));
            }
            return false;
        }

        IsDownloading = true;
        DownloadProgress = 0;
        Changed?.Invoke(null, EventArgs.Empty);

        try
        {
            statusCallback?.Invoke("Checking update package...");
            var mgr = new UpdateManager(new GithubSource(GithubRepoUrl, null, false));
            var updateInfo = await Task.Run(async () => await mgr.CheckForUpdatesAsync());
            if (updateInfo is null)
            {
                statusCallback?.Invoke("No update package found.");
                return false;
            }

            statusCallback?.Invoke("Downloading update (0%)...");
            await Task.Run(async () =>
            {
                await mgr.DownloadUpdatesAsync(updateInfo, progress =>
                {
                    DownloadProgress = progress;
                    DownloadProgressChanged?.Invoke(progress);
                    statusCallback?.Invoke($"Downloading update ({progress}%)...");
                });
            });

            statusCallback?.Invoke("Applying update and restarting ArtifactX...");
            mgr.ApplyUpdatesAndRestart(updateInfo);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateCheckService] Download and restart failed: {ex}");
            statusCallback?.Invoke($"Update failed: {ex.Message}");
            return false;
        }
        finally
        {
            IsDownloading = false;
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }
}
