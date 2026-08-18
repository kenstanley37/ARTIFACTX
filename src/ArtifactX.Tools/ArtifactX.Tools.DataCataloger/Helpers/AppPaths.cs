namespace ArtifactX.Tools.DataCataloger.Helpers;

public static class AppPaths
{
    public static string WorkingFolder =>
        Path.Combine(AppContext.BaseDirectory, "Working");

    public static void EnsureWorkingFolder()
    {
        if (!Directory.Exists(WorkingFolder))
            Directory.CreateDirectory(WorkingFolder);
    }
}