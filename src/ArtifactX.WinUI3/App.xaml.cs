using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ArtifactX.WinUI3.Services;
using Velopack;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ArtifactX.WinUI3;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    public static MainWindow? MainWindowInstance { get; private set; }

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        // Must run before anything else - Velopack intercepts special
        // install/update/uninstall command-line invocations (creating
        // shortcuts, cleaning up old versions, etc.) that only happen
        // during those lifecycle events, never during a normal launch.
        // There's no accessible custom Main() in this WinUI3 project (the
        // SDK generates its own), so this constructor - already documented
        // above as "the logical equivalent of main()" - is the earliest
        // reachable hook.
        VelopackApp.Build().Run();
        // Before anything opens the data: it moved from %LocalAppData% (the
        // install folder) to %AppData% on 2026-10-15.
        Services.LocalAppDataPaths.MoveFromOldFolder();
        // The game-data library logs next to the exe by default (the command-line
        // tool's behaviour) - not writable when installed for all users. The app
        // only logs during a game-data build, which picks its own file.
        ArtifactX.GameData.Services.LogService.EchoToConsole = false;
        ArtifactX.GameData.Services.LogService.UseLogFile(null);
        DevTimingLog.Start();

        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        // Initialize the SQLite local store engine cleanly before the window goes live
        //await ArtifactX.Data.DatabaseInitializer.InitializeAsync();

        DevTimingLog.Write("OnLaunched");
        DeleteWorkingFolder();
        DevTimingLog.Write("working folder moved aside");
        ThemeService.ApplySaved();
        DevTimingLog.Write("theme applied");

        _window = new MainWindow();
        DevTimingLog.Write("main window created");
        MainWindowInstance = (MainWindow)_window;
        _window.Activate();
        DevTimingLog.Write("main window activated");
        DevTimingLog.WatchUiThread(_window.DispatcherQueue);

        // The day's automatic backup of the player's own data (Settings page).
        _ = Task.Run(() =>
        {
            try { UserDatabase.DailyBackupIfDue(); }
            catch (Exception ex) { Debug.WriteLine($"[Startup] Daily backup failed: {ex.Message}"); }
        });
    }

    /// <summary>Clears last session's decrypted save copies (Working, next to
    /// the exe). It used to delete them file by file before the window opened -
    /// hundreds of MB with several accounts, all while the user stared at
    /// nothing (2026-10-12). Now the folder is renamed out of the way (instant)
    /// and deleted in the background; leftovers from a run that closed before
    /// its cleanup finished are swept up the same way. If the rename fails
    /// (something holds a file open), the folder is simply reused - indexing
    /// overwrites every copy it needs.</summary>
    public void DeleteWorkingFolder()
    {
        // Must match SaveWorkspaceService's working folder exactly, or this cleans up
        // nothing while stale slot data quietly accumulates.
        string workingRoot = LocalAppDataPaths.WorkingFolder;

        try
        {
            if (Directory.Exists(workingRoot))
                Directory.Move(workingRoot, $"{workingRoot}.old-{DateTime.UtcNow.Ticks}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[Startup Cleanup] Reusing the working directory: {ex.Message}");
        }

        _ = Task.Run(() =>
        {
            foreach (var stale in Directory.GetDirectories(LocalAppDataPaths.RootFolder, "Working.old-*"))
            {
                try { Directory.Delete(stale, recursive: true); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Debug.WriteLine($"[Startup Cleanup] Couldn't remove {stale}: {ex.Message}");
                }
            }
        });
    }
}