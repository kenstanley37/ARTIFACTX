using System;
using System.Collections.Generic;
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

/// <summary>Result returned by a manual or forced screen capture.</summary>
public readonly record struct CaptureResult(bool Success, string Message, string? ReadInfo = null);

/// <summary>
/// Records what the game shows, to learn which screens could be read for planet and
/// system details (GitHub #18, first step, 2026-10-17). Off unless the player turns it
/// on in Settings. Every 2 seconds, while No Man's Sky is running AND is the active
/// window (never another app - borderless capture reads the screen, not the game),
/// it grabs the game's window, skips frames that look like the last one kept, reads
/// the text with Windows' own offline OCR, hands it to the readers (planet, system, market,
/// creatures...), and keeps the text it read - not the picture - in
/// %AppData%\ArtifactX\capture-log\&lt;date&gt;\. Oldest go past 200 MB. Nothing leaves the PC.
/// (System and creature pictures the readers choose to keep live in their own folders.)
/// </summary>
public static class ScreenCaptureLogService
{
    private const string SettingKey = "ScreenCaptureLog";
    private const long MaxBytes = 200L << 20;
    // "A new screen": enough small cells of a 96x54 grey thumbnail changed clearly. A
    // whole-screen average missed scrolling a list - only the list moves, so a trade
    // terminal's later pages weren't kept for a minute (2026-10-17).
    private const int ThumbW = 96, ThumbH = 54;
    private const int CellChange = 24;           // grey levels for "this cell changed"
    private const double ChangedFraction = 0.015; // share of cells that must change

    private static Timer? _timer;
    private static readonly SemaphoreSlim _gate = new(1, 1);
    private static byte[]? _lastThumb;
    private static int _savedSinceCheck;
    private static bool _recording;
    private static string _lastText = "";

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
        PlanetPageReader.Start();
        SystemPageReader.Start();
        UnmatchedBodiesService.Start();
        // Pictures kept by earlier versions go: only the text is kept now.
        _ = Task.Run(() =>
        {
            try
            {
                if (Directory.Exists(Folder))
                    foreach (var jpg in Directory.EnumerateFiles(Folder, "*.jpg", SearchOption.AllDirectories))
                        try { File.Delete(jpg); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            catch (IOException) { }
        });
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
        if (!_gate.Wait(0)) return;
        try { CaptureOnce().GetAwaiter().GetResult(); }
        catch (Exception ex) { Debug.WriteLine($"[ScreenCaptureLog] {ex.Message}"); }
        finally { _gate.Release(); }
    }

    private static async Task CaptureOnce()
    {
        if (!Enabled || !GameProcessMonitorService.IsGameRunning) return;
        IntPtr hwnd = GameWindow();
        if (hwnd == IntPtr.Zero || IsIconic(hwnd)) return;
        if (!GetWindowRect(hwnd, out var r)) return;
        int w = r.Right - r.Left, h = r.Bottom - r.Top;
        if (w < 320 || h < 200) return;

        // Capture whenever the game is unobstructed. If another window is in the foreground (e.g. browser),
        // check if it actually overlaps the game rect. If foreground is on a secondary monitor or
        // non-overlapping (e.g. side-by-side ArtifactX), continue capturing so the screen reader
        // works continuously without requiring the game to have focus.
        IntPtr fg = GetForegroundWindow();
        if (fg != IntPtr.Zero && fg != hwnd && !IsIconic(fg) && GetWindowRect(fg, out var fgRect))
        {
            bool overlaps = !(fgRect.Right <= r.Left || fgRect.Left >= r.Right || fgRect.Bottom <= r.Top || fgRect.Top >= r.Bottom);
            if (overlaps) return;
        }

        byte[] pixels = Grab(r.Left, r.Top, w, h);
        byte[] thumb = Thumb(pixels, w, h);
        if (_lastThumb is not null && ChangedShare(thumb, _lastThumb) < ChangedFraction) return;
        _lastThumb = thumb;

        await ProcessFrameAsync(pixels, w, h, force: false);
    }

    /// <summary>
    /// Manually captures the game screen immediately and runs OCR and all discovery/market/fauna readers.
    /// Does not require background recording to be enabled. Bypasses frame duplicate heuristics.
    /// </summary>
    public static async Task<CaptureResult> CaptureNowAsync()
    {
        if (!await _gate.WaitAsync(TimeSpan.FromSeconds(3)))
            return new CaptureResult(false, "Screen capture is busy processing another frame.");

        try
        {
            if (!GameProcessMonitorService.IsGameRunning)
                return new CaptureResult(false, "No Man's Sky is not currently running. Launch the game before capturing screens.");

            IntPtr hwnd = GameWindow();
            if (hwnd == IntPtr.Zero)
                return new CaptureResult(false, "No Man's Sky game window was not found.");

            if (IsIconic(hwnd))
            {
                ShowWindow(hwnd, 9 /* SW_RESTORE */);
                await Task.Delay(250);
            }

            if (IsIconic(hwnd))
                return new CaptureResult(false, "No Man's Sky is minimized. Please restore the game window to capture.");

            if (!GetWindowRect(hwnd, out var r))
                return new CaptureResult(false, "Could not determine game window location.");

            int w = r.Right - r.Left, h = r.Bottom - r.Top;
            if (w < 320 || h < 200)
                return new CaptureResult(false, $"Game window is too small ({w}x{h}).");

            IntPtr fg = GetForegroundWindow();
            bool overlaps = false;
            if (fg != IntPtr.Zero && fg != hwnd && !IsIconic(fg) && GetWindowRect(fg, out var fgRect))
            {
                overlaps = !(fgRect.Right <= r.Left || fgRect.Left >= r.Right || fgRect.Bottom <= r.Top || fgRect.Top >= r.Bottom);
            }

            IntPtr appHwnd = IntPtr.Zero;
            if (overlaps)
            {
                try
                {
                    if (App.MainWindowInstance is not null)
                        appHwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
                }
                catch { }

                SetForegroundWindow(hwnd);
                await Task.Delay(250);
            }

            byte[] pixels = Grab(r.Left, r.Top, w, h);
            byte[] thumb = Thumb(pixels, w, h);
            _lastThumb = thumb;

            if (appHwnd != IntPtr.Zero)
            {
                SetForegroundWindow(appHwnd);
            }

            var (ocrSuccess, readInfo, text) = await ProcessFrameAsync(pixels, w, h, force: true);
            if (!ocrSuccess)
                return new CaptureResult(false, "Windows OCR engine could not be initialized for the current user language.");

            if (!string.IsNullOrWhiteSpace(readInfo))
                return new CaptureResult(true, $"Capture successful:\n{readInfo}", readInfo);

            int lineCount = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries).Length;
            return new CaptureResult(true, $"Screen captured ({w}x{h}, {lineCount} text lines recognized), but no discovery or station screen was matched.", null);
        }
        catch (Exception ex)
        {
            return new CaptureResult(false, $"Capture error: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<(bool OcrSuccess, string? ReadInfo, string Text)> ProcessFrameAsync(byte[] pixels, int w, int h, bool force)
    {
        using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(pixels.AsBuffer(), BitmapPixelFormat.Bgra8, w, h, BitmapAlphaMode.Ignore);
        string text = "";
        var lines = new List<string>();
        var placed = new List<(string Text, double Y)>();
        // With the left edge too (share of the width), for screens laid out in columns.
        var placedX = new List<(string Text, double X, double Y)>();
        if (OcrEngine.TryCreateFromUserProfileLanguages() is not { } ocr)
        {
            return (false, null, "");
        }

        var result = await ocr.RecognizeAsync(bitmap);
        lines = result.Lines.Select(l => l.Text).ToList();
        placed = result.Lines.Select(l => (l.Text, l.Words.Count == 0 ? 0 : l.Words.Average(wd => wd.BoundingRect.Y + wd.BoundingRect.Height / 2))).ToList();
        placedX = result.Lines.Select(l => (l.Text, l.Words.Count == 0 ? 0 : l.Words.Min(wd => wd.BoundingRect.X) / w,
            l.Words.Count == 0 ? 0 : l.Words.Average(wd => wd.BoundingRect.Y + wd.BoundingRect.Height / 2))).ToList();
        text = string.Join(Environment.NewLine, lines);

        // Same words as the last kept screen (the cursor moved over the same list): skip it unless forced.
        string words = new string(text.Where(char.IsLetter).ToArray());
        if (!force && words.Length > 0 && words == _lastText) return (true, null, text);
        _lastText = words;

        string? read = null;
        try { read = await PlanetPageReader.TryReadAsync(lines, () => ReadTitleAsync(pixels, w, h)) ?? await PlanetPageReader.TryReadFlightCardAsync(lines) ?? await PlanetPageReader.TryReadOnPlanetAsync(placed) ?? await SystemPageReader.TryReadAsync(lines); }
        catch (Exception ex) { read = "planet page: error " + ex.Message; }
        try
        {
            if (await SystemMarketService.TryReadAsync(placed, (x0, x1, y) => ReadStripAsync(pixels, w, h, x0, x1, y)) is { } market) read = (read is null ? "" : read + " | ") + market;
        }
        catch (Exception ex) { read = (read is null ? "" : read + " | ") + "trade terminal: error " + ex.Message; }
        try
        {
            if (await CreatureListService.TryReadAsync(placedX, path => SaveCreaturePictureAsync(pixels, w, h, path)) is { } creatures)
                read = (read is null ? "" : read + " | ") + creatures;
        }
        catch (Exception ex) { read = (read is null ? "" : read + " | ") + "fauna list: error " + ex.Message; }
        try
        {
            if (BodyKindsService.TryRead(lines) is { } body) read = (read is null ? "" : read + " | ") + body;
        }
        catch (Exception ex) { read = (read is null ? "" : read + " | ") + "body pop-up: error " + ex.Message; }
        try
        {
            if (SystemPageReader.SystemView(placed) is ulong viewed)
            {
                int labels = SystemPageReader.LabelsShown(viewed, placed);
                var facts = UniverseInfo.For(viewed);
                int bodies = facts.PlanetCount + facts.MoonCount;
                // Settled view only (most labels readable), or forced manual capture if at least 1 label visible.
                bool shouldSave = (labels * 2 >= bodies) || (force && labels > 0);
                if (shouldSave && await SaveSystemPictureAsync(viewed, pixels, w, h, labels, force))
                    read = (read is null ? "" : read + " | ") + $"system picture saved for {UniverseInfo.SystemName(viewed)} ({labels}/{bodies} labels)";
            }
        }
        catch (Exception ex) { read = (read is null ? "" : read + " | ") + "system picture: error " + ex.Message; }

        var now = DateTime.Now;
        string day = Path.Combine(Folder, now.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(day);
        string name = now.ToString("HHmmss_fff");
        // Only the text read from the frame is kept, not the picture: the pictures were for
        // testing the readers and reached 1.1 GB (user, 2026-10-18).
        var info = new StringBuilder()
            .AppendLine($"time: {now:yyyy-MM-dd HH:mm:ss.fff}")
            .AppendLine($"window: {w}x{h}")
            .AppendLine($"mode: {(force ? "manual" : "auto")}")
            .AppendLine($"save: {SaveSessionManager.ActiveLabel ?? "-"}")
            .AppendLine($"read: {read ?? "-"}")
            .AppendLine("--- text ---")
            .AppendLine(text);
        await File.WriteAllTextAsync(Path.Combine(day, name + ".txt"), info.ToString());
        if (read is not null) await File.AppendAllTextAsync(Path.Combine(day, "read.log"), $"{now:HH:mm:ss} {(force ? "[MANUAL] " : "")}{read}{Environment.NewLine}");

        if (++_savedSinceCheck >= 50) { _savedSinceCheck = 0; Trim(); }

        return (true, read, text);
    }

    /// <summary>The planet name at the top left of a Discoveries page, read on its own:
    /// a tight crop shrunk to 60% and 75% (with a dark margin) reads the big title far
    /// better than the whole screen does. Tested on the recorded pages: "Guhul VI",
    /// "Euben XIII" and "Izent 62/B4" in full, where the whole screen gave nothing,
    /// "Euben" and "Izent 62/84".</summary>
    private static async Task<IReadOnlyList<string>> ReadTitleAsync(byte[] px, int w, int h)
    {
        var reads = new List<string>();
        if (OcrEngine.TryCreateFromUserProfileLanguages() is not { } ocr) return reads;
        int x0 = w * 11 / 100, y0 = h * 17 / 100, cw = w * 34 / 100, ch = h * 9 / 100;
        foreach (double scale in new[] { 0.6, 0.75 })
        {
            const int pad = 40;
            int ow = (int)(cw * scale) + 2 * pad, oh = (int)(ch * scale) + 2 * pad;
            var o = new byte[ow * oh * 4];
            for (int y = 0; y < oh; y++)
                for (int x = 0; x < ow; x++)
                {
                    int sx = (int)((x - pad) / scale), sy = (int)((y - pad) / scale), j = (y * ow + x) * 4;
                    byte l = 0;
                    if (sx >= 0 && sy >= 0 && sx < cw && sy < ch)
                    {
                        int k = ((y0 + sy) * w + x0 + sx) * 4;
                        l = (byte)((px[k] * 29 + px[k + 1] * 150 + px[k + 2] * 77) >> 8);
                    }
                    o[j] = o[j + 1] = o[j + 2] = l; o[j + 3] = 255;
                }
            using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(o.AsBuffer(), BitmapPixelFormat.Bgra8, ow, oh, BitmapAlphaMode.Ignore);
            var result = await ocr.RecognizeAsync(bitmap);
            if (result.Lines.FirstOrDefault()?.Text is { } line) reads.Add(line);
        }
        return reads;
    }

    /// <summary>One strip of the frame read on its own at twice the size (grey, dark margin):
    /// x from <paramref name="fx0"/> to <paramref name="fx1"/> of the width, 32 px tall at
    /// 1080p around <paramref name="yCentre"/>. Read "1/5" and "-4.1%" where the whole
    /// screen gave nothing (Belezna's trading post, 2026-10-18). "" when nothing is read.</summary>
    private static async Task<string> ReadStripAsync(byte[] px, int w, int h, double fx0, double fx1, double yCentre)
    {
        if (OcrEngine.TryCreateFromUserProfileLanguages() is not { } ocr) return "";
        int half = Math.Max(8, h * 16 / 1080);
        int x0 = (int)(w * fx0), cw = (int)(w * (fx1 - fx0)), y0 = (int)yCentre - half, ch = 2 * half;
        if (cw <= 0 || y0 < 0 || y0 + ch > h || x0 + cw > w) return "";
        const int pad = 20;
        const double scale = 2;
        int ow = (int)(cw * scale) + 2 * pad, oh = (int)(ch * scale) + 2 * pad;
        var o = new byte[ow * oh * 4];
        for (int y = 0; y < oh; y++)
            for (int x = 0; x < ow; x++)
            {
                int sx = (int)((x - pad) / scale), sy = (int)((y - pad) / scale), j = (y * ow + x) * 4;
                byte l = 0;
                if (sx >= 0 && sy >= 0 && sx < cw && sy < ch)
                {
                    int k = ((y0 + sy) * w + x0 + sx) * 4;
                    l = (byte)((px[k] * 29 + px[k + 1] * 150 + px[k + 2] * 77) >> 8);
                }
                o[j] = o[j + 1] = o[j + 2] = l; o[j + 3] = 255;
            }
        using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(o.AsBuffer(), BitmapPixelFormat.Bgra8, ow, oh, BitmapAlphaMode.Ignore);
        var result = await ocr.RecognizeAsync(bitmap);
        return string.Join(" ", result.Lines.Select(l => l.Text));
    }

    /// <summary>Raised (any thread) when a system's picture was saved.</summary>
    public static event Action<ulong>? SystemPictureSaved;
    // Labels shown by the picture kept for each system this session: a frame showing more replaces it.
    private static readonly Dictionary<ulong, int> PicturedThisSession = new();

    private static string PicturePath(ulong system) =>
        Path.Combine(LocalAppDataPaths.GetSubfolder("system-pictures"), system.ToString("X", System.Globalization.CultureInfo.InvariantCulture) + ".jpg");

    /// <summary>The picture of a system's planets as the game's Discoveries view draws them, if one was taken.</summary>
    public static string? SystemPicture(ulong system) => File.Exists(PicturePath(system)) ? PicturePath(system) : null;

    /// <summary>Crops the planets from a system's Discoveries view (right of the list, below
    /// the name: x 46-93%, y 32-87%) and keeps it for the Planets page, once a session per
    /// system - kept apart from the recordings, so clearing those never removes it.</summary>
    private static async Task<bool> SaveSystemPictureAsync(ulong system, byte[] px, int w, int h, int labels, bool force = false)
    {
        lock (PicturedThisSession)
        {
            if (!force && PicturedThisSession.TryGetValue(system, out int best) && best >= labels) return false;
            PicturedThisSession[system] = labels;
        }
        int x0 = w * 46 / 100, y0 = h * 32 / 100, cw = w * 47 / 100, ch = h * 55 / 100;
        var crop = new byte[cw * ch * 4];
        for (int y = 0; y < ch; y++) System.Buffer.BlockCopy(px, ((y0 + y) * w + x0) * 4, crop, y * cw * 4, cw * 4);
        using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(crop.AsBuffer(), BitmapPixelFormat.Bgra8, cw, ch, BitmapAlphaMode.Ignore);
        using (var file = new FileStream(PicturePath(system), FileMode.Create, FileAccess.ReadWrite))
        using (var stream = file.AsRandomAccessStream())
        {
            var options = new BitmapPropertySet { ["ImageQuality"] = new BitmapTypedValue(0.88, Windows.Foundation.PropertyType.Single) };
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream, options);
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync();
        }
        SystemPictureSaved?.Invoke(system);
        return true;
    }

    /// <summary>The creature shown on the Discoveries fauna screen (left of the list, with its
    /// title: x 6-66%, y 15-93%), kept for the Planets page beside its traits (2026-10-18). Title,
    /// gender, size and notes are kept in it on purpose, for a larger view later (user).</summary>
    private static async Task<bool> SaveCreaturePictureAsync(byte[] px, int w, int h, string path)
    {
        int x0 = w * 6 / 100, y0 = h * 15 / 100, cw = w * 60 / 100, ch = h * 78 / 100;
        var crop = new byte[cw * ch * 4];
        for (int y = 0; y < ch; y++) System.Buffer.BlockCopy(px, ((y0 + y) * w + x0) * 4, crop, y * cw * 4, cw * 4);
        using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(crop.AsBuffer(), BitmapPixelFormat.Bgra8, cw, ch, BitmapAlphaMode.Ignore);
        using (var file = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
        using (var stream = file.AsRandomAccessStream())
        {
            var options = new BitmapPropertySet { ["ImageQuality"] = new BitmapTypedValue(0.88, Windows.Foundation.PropertyType.Single) };
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream, options);
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync();
        }
        return true;
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
        try
        {
            foreach (var p in processes)
            {
                if (p.MainWindowHandle != IntPtr.Zero) return p.MainWindowHandle;
            }
            IntPtr found = FindWindow(null, "No Man's Sky");
            if (found != IntPtr.Zero) return found;
            return IntPtr.Zero;
        }
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

    private static double ChangedShare(byte[] a, byte[] b)
    {
        int changed = 0;
        for (int i = 0; i < a.Length; i++) if (Math.Abs(a[i] - b[i]) > CellChange) changed++;
        return (double)changed / a.Length;
    }

    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize; public int biWidth, biHeight; public ushort biPlanes, biBitCount;
        public uint biCompression, biSizeImage; public int biXPelsPerMeter, biYPelsPerMeter; public uint biClrUsed, biClrImportant;
    }

    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)] private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);
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
