namespace ArtifactX.Maui.Services;

/// <summary>
/// Scanner timing log (logcat tag ArtifactX.Scan). Compiled in for Debug, and for a Release build only with
/// <c>-p:ScanDiagnostics=true</c>, so release timings can be measured on a test phone without shipping logs.
/// </summary>
internal static class PortalScanDiagnostics
{
    [System.Diagnostics.Conditional("SCAN_DIAGNOSTICS")]
    public static void Write(string message)
    {
#if ANDROID
        Android.Util.Log.Debug("ArtifactX.Scan", message);
#else
        System.Diagnostics.Debug.WriteLine(message);
#endif
    }
}
