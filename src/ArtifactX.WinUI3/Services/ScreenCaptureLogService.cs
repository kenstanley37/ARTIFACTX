using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Records what the game shows, to learn which screens could be read for planet and
/// system details (GitHub #18, first step, 2026-10-17). Off unless the player turns it
/// on in Settings. Every 2 seconds, while No Man's Sky is running AND is the active
/// window (never another app - borderless capture reads the screen, not the game),
/// it grabs the game's window, skips frames that look like the last one kept, reads
/// the text with Windows' own offline OCR, and keeps the picture and text in
/// %AppData%\ArtifactX\capture-log\&lt;date&gt;\. Oldest days go once it passes 1 GB.
/// Nothing is understood or filled in yet; nothing leaves the PC.
/// </summary>
public static class ScreenCaptureLogService
{
    private const string SettingKey = "ScreenCaptureLog";
    private const long MaxBytes = 1L << 30;
    private const int ThumbW = 64, ThumbH = 36;
    private const double ChangeThreshold = 6.0; // mean grey difference (0-255) for "a new screen"

    private static Timer? _timer;
    private static int _busy;
    private static byte[]? _lastThumb;
    private static int _savedSinceCheck;
    private static bool _recording;

    /// <summary>Whether the player has turned recording on (this PC only).</summary>
    public static bool Enabled
    {
        get => SettingsStore.Get(SettingsStore.Scope.Device, SettingKey) == "true";
        set
        {
            SettingsStore.Set(SettingsStore.Scope.Device, SettingKey, value ? "true" : null);
            Update();
        }
    }

    /// <summary>True while it's switched on and the game is running (the title bar says so).</summary>
    public static bool IsRecording => _recording;

    public static event Action? RecordingChanged;

    public static string Folder => LocalAppDataPaths.GetSubfolder("capture-log");

    public static void Start()
    {
        GameProcessMonitorService.RunningStateChanged += (_, _) => Update();
        Update();
    }

    private static void Update()
    {
        bool on = Enabled && GameProcessMonitorService.IsGameRunning;
        if (on && _timer is null) _timer = new Timer(_ => Tick(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        if (!on && _timer is not null) { _timer.Dispose(); _timer = null; _lastThumb = null; }
        if (on == _recording) return;
        _recording = on;
        RecordingChanged?.Invoke();
    }

    private static void Tick()
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1) return;
        try { CaptureOnce().GetAwaiter().GetResult(); }
        catch (Exception ex) { Debug.WriteLine($"[ScreenCaptureLog] {ex.Message}"); }
        finally { _busy = 0; }
    }

    private static async Task CaptureOnce()
    {
        if (!Enabled || !GameProcessMonitorService.IsGameRunning) return;
        IntPtr hwnd = GameWindow();
        // Only the game, only when it's in front: alt-tabbed to a browser = nothing captured.
        if (hwnd == IntPtr.Zero || GetForegroundWindow() != hwnd || IsIconic(hwnd)) return;
        if (!GetWindowRect(hwnd, out var r)) return;
        int w = r.Right - r.Left, h = r.Bottom - r.Top;
        if (w < 320 || h < 200) return;

        byte[] pixels = Grab(r.Left, r.Top, w, h);
        byte[] thumb = Thumb(pixels, w, h);
        if (_lastThumb is not null && Difference(thumb, _lastThumb) < ChangeThreshold) return;
        _lastThumb = thumb;

        using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(pixels.AsBuffer(), BitmapPixelFormat.Bgra8, w, h, BitmapAlphaMode.Ignore);
        string text = "";
        if (OcrEngine.TryCreateFromUserProfileLanguages() is { } ocr)
        {
            var result = await ocr.RecognizeAsync(bitmap);
            text = string.Join(Environment.NewLine, result.Lines.Select(l => l.Text));
        }

        var now = DateTime.Now;
        string day = Path.Combine(Folder, now.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(day);
        string name = now.ToString("HHmmss_fff");
        using (var file = new FileStream(Path.Combine(day, name + ".jpg"), FileMode.Create, FileAccess.ReadWrite))
        using (var stream = file.AsRandomAccessStream())
        {
            var options = new BitmapPropertySet { ["ImageQuality"] = new BitmapTypedValue(0.8, Windows.Foundation.PropertyType.Single) };
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream, options);
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync();
        }
        var info = new StringBuilder()
            .AppendLine($"time: {now:yyyy-MM-dd HH:mm:ss.fff}")
            .AppendLine($"window: {w}x{h}")
            .AppendLine($"save: {SaveSessionManager.ActiveLabel ?? "-"}")
            .AppendLine("--- text ---")
            .AppendLine(text);
        await File.WriteAllTextAsync(Path.Combine(day, name + ".txt"), info.ToString());

        if (++_savedSinceCheck >= 50) { _savedSinceCheck = 0; Trim(); }
    }

    /// <summary>Keeps the log under 1 GB by deleting the oldest files.</summary>
    public static void Trim()
    {
        var files = new DirectoryInfo(Folder).GetFiles("*", SearchOption.AllDirectories).OrderBy(f => f.LastWriteTimeUtc).ToList();
        long total = files.Sum(f => f.Length);
        foreach (var f in files)
        {
            if (total <= MaxBytes) break;
            total -= f.Length;
            try { f.Delete(); } catch (IOException) { }
        }
    }

    public static long SizeOnDisk() =>
        new DirectoryInfo(Folder).GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);

    public static void DeleteAll()
    {
        foreach (var dir in new DirectoryInfo(Folder).GetDirectories())
            try { dir.Delete(true); } catch (IOException) { }
        _lastThumb = null;
    }

    private static IntPtr GameWindow()
    {
        var processes = Process.GetProcessesByName("NMS");
        try { return processes.Select(p => p.MainWindowHandle).FirstOrDefault(h => h != IntPtr.Zero); }
        finally { foreach (var p in processes) p.Dispose(); }
    }

    // ------------------------------------------------------------- pixels

    /// <summary>The screen area as BGRA, top row first (GDI: works for borderless and windowed games).</summary>
    private static byte[] Grab(int x, int y, int w, int h)
    {
        IntPtr screen = GetDC(IntPtr.Zero), mem = CreateCompatibleDC(screen), bmp = CreateCompatibleBitmap(screen, w, h);
        IntPtr old = SelectObject(mem, bmp);
        try
        {
            BitBlt(mem, 0, 0, w, h, screen, x, y, 0x00CC0020 /* SRCCOPY */);
            var info = new BITMAPINFOHEADER { biSize = 40, biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32 };
            var pixels = new byte[w * h * 4];
            GetDIBits(mem, bmp, 0, (uint)h, pixels, ref info, 0);
            return pixels;
        }
        finally
        {
            SelectObject(mem, old);
            DeleteObject(bmp);
            DeleteDC(mem);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    private static byte[] Thumb(byte[] px, int w, int h)
    {
        var t = new byte[ThumbW * ThumbH];
        for (int ty = 0; ty < ThumbH; ty++)
            for (int tx = 0; tx < ThumbW; tx++)
            {
                int k = ((ty * h / ThumbH) * w + tx * w / ThumbW) * 4;
                t[ty * ThumbW + tx] = (byte)((px[k] * 29 + px[k + 1] * 150 + px[k + 2] * 77) >> 8);
            }
        return t;
    }

    private static double Difference(byte[] a, byte[] b)
    {
        long sum = 0;
        for (int i = 0; i < a.Length; i++) sum += Math.Abs(a[i] - b[i]);
        return (double)sum / a.Length;
    }

    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize; public int biWidth, biHeight; public ushort biPlanes, biBitCount;
        public uint biCompression, biSizeImage; public int biXPelsPerMeter, biYPelsPerMeter; public uint biClrUsed, biClrImportant;
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);
    [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr hdc, IntPtr bmp, uint start, uint lines, [Out] byte[] bits, ref BITMAPINFOHEADER info, uint usage);
}
