using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
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

        DeleteWorkingFolder();


        _window = new MainWindow();
        MainWindowInstance = (MainWindow)_window;
        _window.Activate();
    }

    public void DeleteWorkingFolder()
    {
        // Must match SaveWorkspaceService's working folder exactly, or this cleans up
        // nothing while stale slot data quietly accumulates next to the exe instead.
        string workingRoot = Path.Combine(AppContext.BaseDirectory, "Working");

        try
        {
            if (Directory.Exists(workingRoot))
            {
                foreach (var file in Directory.GetFiles(workingRoot, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (IOException)
                    {
                        // Skip locked files but continue
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip files we can't touch
                    }
                }

                foreach (var dir in Directory.GetDirectories(workingRoot, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        Directory.Delete(dir, recursive: true);
                    }
                    catch (IOException)
                    {
                        // Skip locked subfolders
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip protected subfolders
                    }
                }

                // Finally try to delete the root folder itself
                try
                {
                    Directory.Delete(workingRoot, recursive: true);
                }
                catch (IOException)
                {
                    // If still locked, leave it — we’ll reuse it
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Startup Cleanup] Failed to clear working directory: {ex.Message}");
        }
    }
}