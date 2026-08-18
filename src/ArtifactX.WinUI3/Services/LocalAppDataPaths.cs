using System;
using System.IO;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Resolves the app's own local data folder using plain Win32 paths rather
/// than Windows.Storage.ApplicationData, which requires package identity and
/// throws InvalidOperationException when called from an unpackaged process -
/// confirmed via a real unpackaged debug run (SaveFolderSettingsService's
/// ApplicationData.Current.LocalSettings.Values access threw exactly that).
/// %LocalAppData%\ArtifactX works identically whether the app is packaged or
/// not, unlike ApplicationData.Current, so everything that used to go through
/// LocalSettings/LocalFolder now goes through here instead.
/// </summary>
public static class LocalAppDataPaths
{
    private static readonly Lazy<string> RootLazy = new(() =>
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArtifactX");
        Directory.CreateDirectory(root);
        return root;
    });

    public static string RootFolder => RootLazy.Value;

    /// <summary>Returns (and ensures the existence of) a named subfolder under
    /// the app's local data root, e.g. "Loadouts".</summary>
    public static string GetSubfolder(string name)
    {
        string path = Path.Combine(RootFolder, name);
        Directory.CreateDirectory(path);
        return path;
    }
}
