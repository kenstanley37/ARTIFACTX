using Microsoft.UI.Dispatching;
using System;
using System.Diagnostics;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Polls for a running NMS.exe (confirmed Steam process name) so the app can
/// block saving while the game might read or rewrite the same files. GOG and
/// Microsoft Store builds are unconfirmed - MSIX-packaged apps in particular
/// may need package-family-name detection rather than a plain process name.
/// Do not assume they match until verified the same way NMS.exe was.
/// </summary>
public static class GameProcessMonitorService
{
    private const string ProcessName = "NMS";
    private static DispatcherQueueTimer? _timer;
    private static bool _isRunning;

    public static bool IsGameRunning => _isRunning;

    /// <summary>Fires whenever the running state actually changes (not on
    /// every poll) - true on transition to running, false on transition to
    /// stopped. Consumers care about the transition, not the poll interval.</summary>
    public static event EventHandler<bool>? RunningStateChanged;

    public static void Start(DispatcherQueue dispatcherQueue)
    {
        if (_timer is not null) return;

        _timer = dispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(3);
        _timer.Tick += (_, _) => Poll();
        _timer.Start();

        Poll();
    }

    private static void Poll()
    {
        // Process.GetProcessesByName returns live Process objects, each
        // wrapping a native OS handle - IDisposable, but nothing here needs
        // to keep holding it open past the length check. Left undisposed,
        // every 3-second poll for the app's entire lifetime would leak a
        // handle until the next GC finalizer pass gets around to it.
        var processes = Process.GetProcessesByName(ProcessName);
        bool nowRunning = processes.Length > 0;
        foreach (var process in processes) process.Dispose();

        if (nowRunning == _isRunning) return;

        _isRunning = nowRunning;
        RunningStateChanged?.Invoke(null, nowRunning);
    }
}