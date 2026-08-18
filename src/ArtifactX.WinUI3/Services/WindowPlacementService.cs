using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Remembers the main window's position, size, and maximized state across
/// launches - including which monitor it was on, for multi-monitor setups.
/// Uses the classic Win32 GetWindowPlacement/SetWindowPlacement APIs rather
/// than AppWindow's own Position/Size, because those only ever reflect the
/// window's CURRENT bounds - while maximized, there's no way to read the
/// underlying "restored" size/position back out of AppWindow, whereas
/// GetWindowPlacement gives that for free (rcNormalPosition) regardless of
/// the window's current show state. Persisted via LocalAppDataPaths (plain
/// System.IO), not Windows.Storage.ApplicationData - see that class for why.
///
/// Monitor validation uses the Win32 MonitorFromRect API rather than the
/// WinRT Microsoft.UI.Windowing.DisplayArea - confirmed via a real crash
/// report that DisplayArea.FindAll() throws InvalidCastException deep in
/// WinRT/COM marshaling when called from MainWindow's constructor, before
/// the window has been activated. Restore() runs at exactly that point (has
/// to, in order to size/position the window before it's first shown), so
/// this deliberately avoids any WinRT windowing API entirely and stays on
/// plain Win32 calls the whole way through, matching GetWindowPlacement/
/// SetWindowPlacement, which have never shown the same issue.
/// </summary>
public static class WindowPlacementService
{
    private static readonly string StateFilePath = Path.Combine(LocalAppDataPaths.RootFolder, "windowstate.json");

    private const int SW_SHOWNORMAL = 1;
    private const int SW_SHOWMAXIMIZED = 3;
    private const uint MONITOR_DEFAULTTONULL = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect(ref RECT lprc, uint dwFlags);

    public static void Save(IntPtr hwnd)
    {
        var placement = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
        if (!GetWindowPlacement(hwnd, ref placement)) return;

        var state = new SavedWindowState
        {
            Left = placement.rcNormalPosition.Left,
            Top = placement.rcNormalPosition.Top,
            Right = placement.rcNormalPosition.Right,
            Bottom = placement.rcNormalPosition.Bottom,
            IsMaximized = placement.showCmd == SW_SHOWMAXIMIZED
        };

        try
        {
            File.WriteAllText(StateFilePath, JsonSerializer.Serialize(state, WindowStateJsonContext.Default.SavedWindowState));
        }
        catch
        {
            // Best-effort - losing window placement on a write failure isn't worth surfacing to the user.
        }
    }

    public static void Restore(IntPtr hwnd)
    {
        SavedWindowState? state;
        try
        {
            if (!File.Exists(StateFilePath)) return; // first launch ever - let Windows use its own default placement
            state = JsonSerializer.Deserialize(File.ReadAllText(StateFilePath), WindowStateJsonContext.Default.SavedWindowState);
        }
        catch
        {
            return;
        }

        if (state is null) return;

        var savedRect = new RECT { Left = state.Left, Top = state.Top, Right = state.Right, Bottom = state.Bottom };
        if (!IsRectOnAnyMonitor(savedRect)) return; // saved monitor is gone (unplugged/reconfigured) - fall back to the default placement instead

        var placement = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
        if (!GetWindowPlacement(hwnd, ref placement)) return;

        placement.showCmd = state.IsMaximized ? SW_SHOWMAXIMIZED : SW_SHOWNORMAL;
        placement.rcNormalPosition = savedRect;

        SetWindowPlacement(hwnd, ref placement);
    }

    /// <summary>Guards against restoring the window to a monitor the user has
    /// since unplugged or reconfigured. MONITOR_DEFAULTTONULL makes
    /// MonitorFromRect return a null handle (rather than falling back to the
    /// nearest monitor) when the rect doesn't land on any currently connected
    /// display.</summary>
    private static bool IsRectOnAnyMonitor(RECT rect) =>
        MonitorFromRect(ref rect, MONITOR_DEFAULTTONULL) != IntPtr.Zero;
}

internal sealed class SavedWindowState
{
    public int Left { get; set; }
    public int Top { get; set; }
    public int Right { get; set; }
    public int Bottom { get; set; }
    public bool IsMaximized { get; set; }
}

[JsonSerializable(typeof(SavedWindowState))]
internal partial class WindowStateJsonContext : JsonSerializerContext { }
