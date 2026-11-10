namespace ArtifactX.GameData;

/// <summary>
/// The No Man's Sky release this version of ArtifactX was tested with. The release name isn't in the game's own
/// files (the update splash is an image; the language files were searched 2026-10-28), so it is written here - but
/// only as "tested with", always compared with the installed build (NMS.exe's FileVersion) so the title bar can say
/// when the player's game is newer. Update all three after checking a new game release: a clean game data build
/// (no "failed to decode" in the build log) and the app's pages working on it. Leave <see cref="Version"/> empty when
/// the patch number isn't known; the label then reads "Forsaken". Neither Steam (update date only) nor the game's
/// main menu (bottom left: "185034 (Save Version 4234)", owner screenshot 2026-11-10) shows a patch number.
/// Forsaken 7.10, build 183791: libMBIN regenerated from NMS.exe's own structure metadata (scripts/libmbin),
/// clean build 2026-10-28.
/// Build 185034 (Steam update 2026-11-09, patch number not confirmed): five structures regenerated, among them
/// GcBuildingGlobals (new MinNodesBufferFree moved MinRadius from +0xC8C to +0xC90); clean build 2026-11-10.
/// </summary>
public static class TestedGameRelease
{
    public const string Name = "Forsaken";
    public const string Version = "";
    public const string Build = "185034";

    /// <summary>The release name with its patch number when known: "Forsaken 7.10" or just "Forsaken".</summary>
    public static string Label => Version.Length > 0 ? $"{Name} {Version}" : Name;

    /// <summary>-1 when the installed game is older than the tested one, 0 the same build, 1 newer, null unknown.</summary>
    public static int? Compare(GameInstall? game) =>
        game?.ExeVersion is { } v && long.TryParse(v, out long installed) && long.TryParse(Build, out long tested)
            ? installed.CompareTo(tested) : null;
}
