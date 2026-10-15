using ArtifactX.Core;
using System.Reflection;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Single source of truth for the app's own version: ArtifactX.WinUI3.csproj's
/// &lt;Version&gt; (e.g. "1.0.0-alpha1"), read from the informational version the
/// SDK stamps into the assembly - which, unlike AssemblyVersion, keeps the
/// pre-release label (2026-10-15: the title bar said "v1.0.0" on an alpha).
/// Bump &lt;Version&gt; there and publish a GitHub release tagged "v" + that
/// version to make UpdateCheckService pick it up.
/// </summary>
public static class AppVersionService
{
    public static ReleaseVersion Current { get; } =
        ReleaseVersion.TryParse(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion)
        ?? new ReleaseVersion(0, 0, 0, "");

    /// <summary>"1.0.0-alpha1" (no build metadata).</summary>
    public static string DisplayVersion => Current.ToString();
}
