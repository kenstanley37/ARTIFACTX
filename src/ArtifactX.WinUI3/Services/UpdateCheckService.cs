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
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/kenstanley37/ARTIFACTX/releases/latest";
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

            using var response = await client.GetAsync(LatestReleaseApiUrl);

            // No release has been published yet (true as of this writing) -
            // GitHub returns a plain 404 for /releases/latest in that case,
            // not an empty/malformed body, so this is worth its own status
            // rather than folding it into Error.
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new UpdateCheckResult { Status = UpdateCheckStatus.NoReleasesYet, Message = "No releases published yet." };
            }

            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync();

            // JsonDocument (DOM parse), not JsonSerializer.Deserialize<T> -
            // this project publishes trimmed in Release
            // (PublishTrimmed=True), and reflection-based deserialization
            // without a source-generated JsonSerializerContext risks
            // breaking under trimming. See AppSettingsJsonContext for the
            // pattern used where a full typed model is actually needed.
            using var doc = JsonDocument.Parse(json);
            string? tagName = doc.RootElement.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() : null;
            string releaseUrl = doc.RootElement.TryGetProperty("html_url", out var urlProp)
                ? urlProp.GetString() ?? ReleasesPageUrl
                : ReleasesPageUrl;

            Version? latest = ParseVersion(tagName);
            if (latest is null)
            {
                return new UpdateCheckResult { Status = UpdateCheckStatus.Error, Message = "Couldn't read the latest release version." };
            }

            // Normalize both sides to Major.Minor.Build before comparing -
            // Version.TryParse leaves missing components as -1, which would
            // otherwise skew comparisons against AppVersionService.Current's
            // always-4-part AssemblyVersion.
            var normalizedLatest = new Version(latest.Major, Math.Max(latest.Minor, 0), Math.Max(latest.Build, 0));
            var normalizedCurrent = new Version(AppVersionService.Current.Major, AppVersionService.Current.Minor, AppVersionService.Current.Build);

            if (normalizedLatest > normalizedCurrent)
            {
                return new UpdateCheckResult
                {
                    Status = UpdateCheckStatus.UpdateAvailable,
                    Message = $"Version {normalizedLatest} is available (you have {AppVersionService.DisplayVersion}).",
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

    private static Version? ParseVersion(string? tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName)) return null;
        string trimmed = tagName.TrimStart('v', 'V');
        return Version.TryParse(trimmed, out var version) ? version : null;
    }
}
