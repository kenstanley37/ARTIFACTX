using Microsoft.UI;
using Microsoft.UI.Windowing;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ArtifactX.WinUI3.Helpers.App;

/// <summary>
/// Gives the main window its taskbar and Alt-Tab icon. An unpackaged WinUI 3 window doesn't reliably take the .exe's
/// ApplicationIcon: the owner saw a blank taskbar icon on 2026-11-18, and again on 2026-11-19 with the 11-18 fix
/// (AppWindow.SetIcon with the loose Assets\AppIcon.ico, set before the title bar was customised) in place. So the icon
/// now comes from the .exe itself (the ApplicationIcon the SDK embeds as icon group 32512, IDI_APPLICATION), at the
/// window's DPI, both sizes sent with WM_SETICON; the loose .ico is the fallback. MainWindow applies it after the title
/// bar is set up and again when the window first activates.
/// </summary>
internal static class WindowIcon
{
    private const int IconGroupId = 32512, ImageIcon = 1, SmIconWidth = 11, IconWidth = 0;
    private const uint WmSetIcon = 0x0080;
    private static readonly IntPtr IconSmall = 0, IconBig = 1;

    // Kept for the life of the app: the window keeps using these handles.
    private static IntPtr _big, _small;

    public static void Apply(AppWindow window, IntPtr hwnd)
    {
        try
        {
            if (_big == 0)
            {
                uint dpi = GetDpiForWindow(hwnd);
                if (dpi == 0) dpi = 96;
                IntPtr module = GetModuleHandle(null);
                _big = LoadImage(module, IconGroupId, ImageIcon, GetSystemMetricsForDpi(IconWidth, dpi), GetSystemMetricsForDpi(IconWidth, dpi), 0);
                _small = LoadImage(module, IconGroupId, ImageIcon, GetSystemMetricsForDpi(SmIconWidth, dpi), GetSystemMetricsForDpi(SmIconWidth, dpi), 0);
            }
            if (_big != 0)
            {
                window.SetIcon(Win32Interop.GetIconIdFromIcon(_big));
                SendMessage(hwnd, WmSetIcon, IconBig, _big);
                if (_small != 0) SendMessage(hwnd, WmSetIcon, IconSmall, _small);
                return;
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or ExternalException)
        {
            System.Diagnostics.Debug.WriteLine($"[WindowIcon] embedded icon unavailable: {ex.Message}");
        }

        string file = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(file)) window.SetIcon(file);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr instance, nint name, uint type, int cx, int cy, uint load);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetricsForDpi(int index, uint dpi);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
}
