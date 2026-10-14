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
}
