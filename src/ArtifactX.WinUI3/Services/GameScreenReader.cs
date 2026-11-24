using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace ArtifactX.WinUI3.Services;

/// <summary>Result of reading the game screen: whether it worked, what to tell the player, and what was read.</summary>
public readonly record struct ScreenReadResult(bool Success, string Message, string? ReadInfo = null);

/// <summary>
/// Reads the No Man's Sky screen once, when the player presses "Read game screen" (GitHub #18).
/// Grabs the game's window, reads its text with Windows' own offline OCR and hands it to the
/// readers (planet page, system pop-ups, trade terminal, creature list, moon/planet pop-ups),
/// which fill in what the save doesn't keep. Nothing is kept from the screen itself - no
/// pictures, no text log - only what the readers store in user.db. Nothing leaves the PC.
///
/// Until 2026-11-24 this was ScreenCaptureLogService: an opt-in recorder that read the screen
/// every 2 seconds while the game ran, kept a text log (capture-log) and cropped pictures of
/// systems and creatures (system-pictures, creature-pictures) with no size limit. The owner
/// removed automatic capture: too costly on modest PCs, and the system generator now gives the
/// facts it was collecting. <see cref="Start"/> deletes those folders and the old setting.
/// </summary>
public static class GameScreenReader
{
    private static readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Starts the readers and removes what the old screen recorder left behind.</summary>
    public static void Start()
    {
        PlanetPageReader.Start();
        SystemPageReader.Start();
        UnmatchedBodiesService.Start();
        SettingsStore.Set(SettingsStore.Scope.Device, "ScreenCaptureLog", null);
        _ = Task.Run(() =>
        {
            foreach (var name in new[] { "capture-log", "system-pictures", "creature-pictures" })
            {
                string folder = Path.Combine(LocalAppDataPaths.RootFolder, name);
                try { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        });
    }

    /// <summary>
    /// Reads the game screen now. If ArtifactX covers the game (one monitor), the game is brought to
    /// the front for the grab and ArtifactX comes back after it.
    /// </summary>
    public static async Task<ScreenReadResult> ReadNowAsync()
    {
        if (!await _gate.WaitAsync(TimeSpan.FromSeconds(3)))
            return new ScreenReadResult(false, "Still reading the last screen. Try again in a moment.");

        try
        {
            if (!GameProcessMonitorService.IsGameRunning)
                return new ScreenReadResult(false, "No Man's Sky isn't running. Start the game, open the screen you want read, then try again.");

            IntPtr hwnd = GameWindow();
            if (hwnd == IntPtr.Zero)
                return new ScreenReadResult(false, "The No Man's Sky window wasn't found.");

            if (IsIconic(hwnd))
            {
                ShowWindow(hwnd, 9 /* SW_RESTORE */);
                await Task.Delay(250);
            }
            if (IsIconic(hwnd))
                return new ScreenReadResult(false, "No Man's Sky is minimised. Restore the game window and try again.");

            if (!GetWindowRect(hwnd, out var r))
                return new ScreenReadResult(false, "Couldn't find where the game window is.");

            int w = r.Right - r.Left, h = r.Bottom - r.Top;
            if (w < 320 || h < 200)
                return new ScreenReadResult(false, $"The game window is too small to read ({w}x{h}).");

            IntPtr fg = GetForegroundWindow();
            bool overlaps = fg != IntPtr.Zero && fg != hwnd && !IsIconic(fg) && GetWindowRect(fg, out var fgRect)
                && !(fgRect.Right <= r.Left || fgRect.Left >= r.Right || fgRect.Bottom <= r.Top || fgRect.Top >= r.Bottom);

            byte[] pixels;
            if (overlaps)
            {
                SetForegroundWindow(hwnd);
                await Task.Delay(250);
                pixels = Grab(r.Left, r.Top, w, h);
                SetForegroundWindow(fg);
            }
            else
            {
                pixels = Grab(r.Left, r.Top, w, h);
            }

            var (ocrReady, read, lineCount) = await Task.Run(() => ReadFrameAsync(pixels, w, h));
            if (!ocrReady)
                return new ScreenReadResult(false, "Windows text recognition isn't available for your Windows display language.");
            if (read is not null)
                return new ScreenReadResult(true, read, read);
            return new ScreenReadResult(true, lineCount == 0
                ? "No text was found on the game screen."
                : "This screen isn't one ArtifactX reads. Open a planet or system in Discoveries, a trade terminal, or a planet's fauna list, then try again.");
        }
        catch (Exception ex)
        {
            return new ScreenReadResult(false, $"Couldn't read the screen: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<(bool OcrReady, string? Read, int LineCount)> ReadFrameAsync(byte[] pixels, int w, int h)
    {
        if (OcrEngine.TryCreateFromUserProfileLanguages() is not { } ocr) return (false, null, 0);
        using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(pixels.AsBuffer(), BitmapPixelFormat.Bgra8, w, h, BitmapAlphaMode.Ignore);
        var result = await ocr.RecognizeAsync(bitmap);
        var lines = result.Lines.Select(l => l.Text).ToList();
        var placed = result.Lines.Select(l => (l.Text, l.Words.Count == 0 ? 0 : l.Words.Average(wd => wd.BoundingRect.Y + wd.BoundingRect.Height / 2))).ToList();
        // With the left edge too (share of the width), for screens laid out in columns.
        var placedX = result.Lines.Select(l => (l.Text, l.Words.Count == 0 ? 0 : l.Words.Min(wd => wd.BoundingRect.X) / w,
            l.Words.Count == 0 ? 0 : l.Words.Average(wd => wd.BoundingRect.Y + wd.BoundingRect.Height / 2))).ToList();

        string? read = null;
        void Add(string? part) { if (part is not null) read = read is null ? part : read + "\n" + part; }
        try { Add(await PlanetPageReader.TryReadAsync(lines, () => ReadTitleAsync(pixels, w, h), () => ReadPlanetDataAsync(pixels, w, h)) ?? await PlanetPageReader.TryReadFlightCardAsync(lines) ?? await PlanetPageReader.TryReadOnPlanetAsync(placed) ?? await SystemPageReader.TryReadAsync(lines)); }
        catch (Exception ex) { Add("Planet or system page: error " + ex.Message); }
        try { Add(await SystemMarketService.TryReadAsync(placed, (x0, x1, y) => ReadStripAsync(pixels, w, h, x0, x1, y))); }
        catch (Exception ex) { Add("Trade terminal: error " + ex.Message); }
        try { Add(CreatureListService.TryRead(placedX)); }
        catch (Exception ex) { Add("Fauna list: error " + ex.Message); }
        try { Add(BodyKindsService.TryRead(lines)); }
        catch (Exception ex) { Add("Moon or planet pop-up: error " + ex.Message); }
        return (true, read, lines.Count);
    }

    /// <summary>A part of the frame made ready for OCR on its own: grey, scaled, with a dark
    /// margin. Windows OCR misses words on a whole busy screen that it reads in a tight crop -
    /// the planet title (2026-10-17), a trading post's stock (2026-10-18), a planet's
    /// sentinels on a bright sky (Lefredid XVI's "Frenzied", 2026-10-20). Null when the area
    /// isn't inside the frame.</summary>
    private static SoftwareBitmap? Crop(byte[] px, int w, int h, int x0, int y0, int cw, int ch, double scale, int pad)
    {
        if (cw <= 0 || ch <= 0 || x0 < 0 || y0 < 0 || x0 + cw > w || y0 + ch > h) return null;
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
        return SoftwareBitmap.CreateCopyFromBuffer(o.AsBuffer(), BitmapPixelFormat.Bgra8, ow, oh, BitmapAlphaMode.Ignore);
    }

    private static async Task<IReadOnlyList<string>> ReadCropAsync(byte[] px, int w, int h, int x0, int y0, int cw, int ch, double scale, int pad)
    {
        if (OcrEngine.TryCreateFromUserProfileLanguages() is not { } ocr) return [];
        using var bitmap = Crop(px, w, h, x0, y0, cw, ch, scale, pad);
        if (bitmap is null) return [];
        return (await ocr.RecognizeAsync(bitmap)).Lines.Select(l => l.Text).ToList();
    }

    /// <summary>The planet name at the top left of a Discoveries page, read on its own:
    /// a tight crop shrunk to 60% and 75% (with a dark margin) reads the big title far
    /// better than the whole screen does. Tested on the recorded pages: "Guhul VI",
    /// "Euben XIII" and "Izent 62/B4" in full, where the whole screen gave nothing,
    /// "Euben" and "Izent 62/84".</summary>
    private static async Task<IReadOnlyList<string>> ReadTitleAsync(byte[] px, int w, int h)
    {
        var reads = new List<string>();
        foreach (double scale in new[] { 0.6, 0.75 })
            if ((await ReadCropAsync(px, w, h, w * 11 / 100, h * 17 / 100, w * 34 / 100, h * 9 / 100, scale, pad: 40)).FirstOrDefault() is { } line)
                reads.Add(line);
        return reads;
    }

    /// <summary>The Planet Data column of a Discoveries page (Weather, Sentinels, Flora, Fauna
    /// with their values: x 11.5-30%, y 30-53%) read on its own at 1.5x. On Lefredid XVI the
    /// whole screen read "Sentinels" with no value under it; every crop of the column read
    /// "Frenzied" (tested on the frame at 1x, 1.5x, 2x and 3x, 2026-10-20).</summary>
    private static Task<IReadOnlyList<string>> ReadPlanetDataAsync(byte[] px, int w, int h) =>
        ReadCropAsync(px, w, h, w * 115 / 1000, h * 30 / 100, w * 185 / 1000, h * 23 / 100, 1.5, pad: 30);

    /// <summary>One strip of the frame read on its own at twice the size (grey, dark margin):
    /// x from <paramref name="fx0"/> to <paramref name="fx1"/> of the width, 32 px tall at
    /// 1080p around <paramref name="yCentre"/>. Read "1/5" and "-4.1%" where the whole
    /// screen gave nothing (Belezna's trading post, 2026-10-18). "" when nothing is read.</summary>
    private static async Task<string> ReadStripAsync(byte[] px, int w, int h, double fx0, double fx1, double yCentre)
    {
        int half = Math.Max(8, h * 16 / 1080);
        var lines = await ReadCropAsync(px, w, h, (int)(w * fx0), (int)yCentre - half, (int)(w * (fx1 - fx0)), 2 * half, 2, pad: 20);
        return string.Join(" ", lines);
    }

    private static IntPtr GameWindow()
    {
        var processes = System.Diagnostics.Process.GetProcessesByName("NMS");
        try
        {
            foreach (var p in processes)
                if (p.MainWindowHandle != IntPtr.Zero) return p.MainWindowHandle;
            return FindWindow(null, "No Man's Sky");
        }
        finally { foreach (var p in processes) p.Dispose(); }
    }

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
