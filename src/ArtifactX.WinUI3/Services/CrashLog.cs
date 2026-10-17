using System;
using System.IO;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Records unhandled exceptions to %APPDATA%\ArtifactX\logs\crash.log, in every
/// build. Without it an exception thrown in an event handler ends the app as a
/// bare 0xc000027b in Microsoft.UI.Xaml.dll with nothing to go on (2026-10-17:
/// a crash on clicking inputs left only E_NOINTERFACE in the Windows event log).
/// The app still closes as before; this only keeps the evidence.
/// </summary>
public static class CrashLog
{
    private static readonly object Gate = new();

    public static void Attach(Microsoft.UI.Xaml.Application app)
    {
        app.UnhandledException += (_, e) => Write("UI", e.Exception, e.Message);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Write("process", e.ExceptionObject as Exception, null);
        TaskScheduler.UnobservedTaskException += (_, e) => Write("background task", e.Exception, null);
    }

    private static void Write(string where, Exception? ex, string? message)
    {
        try
        {
            string folder = LocalAppDataPaths.GetSubfolder("logs");
            Directory.CreateDirectory(folder);
            string text = $"==== {DateTime.Now:yyyy-MM-dd HH:mm:ss} unhandled ({where}), v{typeof(CrashLog).Assembly.GetName().Version}{Environment.NewLine}"
                + (message is { Length: > 0 } && message != ex?.Message ? message + Environment.NewLine : "")
                + (ex?.ToString() ?? "(no exception object)") + Environment.NewLine + Environment.NewLine;
            lock (Gate) File.AppendAllText(Path.Combine(folder, "crash.log"), text);
        }
        catch
        {
            // Logging must never be what takes the app down.
        }
    }
}
