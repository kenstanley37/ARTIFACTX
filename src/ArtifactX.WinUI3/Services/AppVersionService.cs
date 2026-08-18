using System;
using System.Reflection;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Single source of truth for the app's own version, read from the
/// AssemblyVersion the SDK generates from ArtifactX.WinUI3.csproj's
/// &lt;Version&gt; property - not hand-maintained anywhere else. Bump
/// &lt;Version&gt; there and tag/publish a matching GitHub release to make
/// UpdateCheckService pick it up.
/// </summary>
public static class AppVersionService
{
    public static Version Current { get; } =
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    public static string DisplayVersion => $"{Current.Major}.{Current.Minor}.{Current.Build}";

    // Manually bumped whenever ArtifactX is actually re-tested against a new
    // No Man's Sky update - NOT detected/read from the game itself (that
    // would tell users what's installed, not whether this app's field
    // mappings have actually been checked against it). Shown in the title
    // bar's info row next to the app's own version. Update this string by
    // hand after verifying against a new NMS patch.
    public const string VerifiedNmsUpdate = "SWARM";
}
