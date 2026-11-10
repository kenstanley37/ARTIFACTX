namespace ArtifactX.Maui.Services;

internal static class PortalScanDiagnostics
{
    [System.Diagnostics.Conditional("DEBUG")]
    public static void Write(string message)
    {
#if ANDROID
        Android.Util.Log.Debug("ArtifactX.Scan", message);
#else
        System.Diagnostics.Debug.WriteLine(message);
#endif
    }
}
