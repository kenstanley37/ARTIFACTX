using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Debug builds only: a plain-text timing log for startup and Save Selection,
/// at %APPDATA%\ArtifactX\logs\timing.log (recreated each launch), so a
/// slow start can be measured from one ordinary run instead of guessed at.
/// Each line: milliseconds since launch, thread (UI or pool), message.
/// Also records any stretch where the UI thread stops responding for more
/// than 250 ms (see WatchUiThread). Release builds compile all of it away.
/// </summary>
public static class DevTimingLog
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly object Gate = new();
    private static string? _path;
    private static int _uiThreadId = -1;

    [Conditional("DEBUG")]
    public static void Start()
    {
        _uiThreadId = Environment.CurrentManagedThreadId;
        string folder = LocalAppDataPaths.GetSubfolder("logs");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "timing.log");
        File.WriteAllText(_path, $"ArtifactX timing log, {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}");
        Write("launch");
    }

    [Conditional("DEBUG")]
    public static void Write(string message)
    {
        if (_path is null) return;
        string thread = Environment.CurrentManagedThreadId == _uiThreadId ? "UI  " : $"T{Environment.CurrentManagedThreadId,-3}";
        string line = $"{Clock.ElapsedMilliseconds,7} ms  {thread}  {message}{Environment.NewLine}";
        lock (Gate)
        {
            try { File.AppendAllText(_path, line); }
            catch (IOException) { }
        }
    }

    /// <summary>Ticks every 100 ms on the UI thread; a tick arriving late
    /// means the UI thread was busy, and how long it was stuck is logged.</summary>
    [Conditional("DEBUG")]
    public static void WatchUiThread(Microsoft.UI.Dispatching.DispatcherQueue queue)
    {
        var timer = queue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(100);
        long last = Clock.ElapsedMilliseconds;
        timer.Tick += (_, _) =>
        {
            long now = Clock.ElapsedMilliseconds;
            if (now - last > 350) Write($"UI thread was busy for {now - last - 100} ms");
            last = now;
        };
        timer.Start();
        // Stop watching after two minutes; startup is what's being measured.
        var stop = queue.CreateTimer();
        stop.Interval = TimeSpan.FromMinutes(2);
        stop.IsRepeating = false;
        stop.Tick += (_, _) => { timer.Stop(); Write("watch stopped"); };
        stop.Start();
    }
}
