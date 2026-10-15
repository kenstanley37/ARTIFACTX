using ArtifactX.GameData;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Controls;

/// <summary>
/// A ship model drawn with a small software renderer (2026-10-15): z-buffered,
/// flat-shaded triangles into a bitmap, redrawn off the UI thread as the player
/// drags to turn it. No 3D engine or web view - a ship is ~75,000 triangles,
/// which draws in a few tens of milliseconds at this size.
/// </summary>
public sealed class ShipPreview : UserControl
{
    private readonly Image _image = new() { Stretch = Microsoft.UI.Xaml.Media.Stretch.None };
    private readonly TextBlock _hint = new()
    {
        Text = "Drag to turn", FontSize = 11, Opacity = 0.6, Margin = new Thickness(8, 6, 8, 6),
        HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom,
    };
    private ShipMesh? _mesh;
    private uint[] _triColours = Array.Empty<uint>();
    private Vector3 _centre;
    // Opens on the nose, three-quarter view from above, nose to the lower left
    // - the angle the user picked by hand (2026-10-15; the tail-on start felt
    // "weird"). Ships face +Z; a negative pitch looks down on the hull.
    private const float StartYaw = MathF.PI + 0.75f, StartPitch = -0.52f;
    private float _yaw = StartYaw, _pitch = StartPitch;
    private Windows.Foundation.Point? _drag;
    private int _drawing, _pending;

    public int PixelWidth { get; set; } = 420;
    public int PixelHeight { get; set; } = 260;

    public ShipPreview()
    {
        var grid = new Grid { Background = ThemeResources.Brush("ThemePanelAltBrush"), CornerRadius = new CornerRadius(6) };
        grid.Children.Add(_image);
        grid.Children.Add(_hint);
        Content = grid;
        PointerPressed += (_, e) => { _drag = e.GetCurrentPoint(this).Position; CapturePointer(e.Pointer); };
        PointerReleased += (_, e) => { _drag = null; ReleasePointerCapture(e.Pointer); };
        PointerMoved += OnPointerMoved;
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
    }

    /// <summary>Shows a model; <paramref name="colourOf"/> gives each material's RGB.</summary>
    public void Show(ShipMesh? mesh, Func<string, uint> colourOf)
    {
        _mesh = mesh;
        _yaw = StartYaw; _pitch = StartPitch; // each ship opens on its front
        if (mesh is null) { _image.Source = null; return; }
        var colours = new uint[mesh.Materials.Length];
        var cache = new Dictionary<string, uint>();
        for (int i = 0; i < colours.Length; i++)
        {
            if (!cache.TryGetValue(mesh.Materials[i], out uint c)) cache[mesh.Materials[i]] = c = colourOf(mesh.Materials[i]);
            colours[i] = c;
        }
        _triColours = colours;
        // Centre on where most of the ship is (a few stray far points shouldn't move it).
        var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
        foreach (var v in mesh.Corners) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
        _centre = (min + max) / 2;
        Redraw();
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_drag is not { } from || _mesh is null) return;
        var to = e.GetCurrentPoint(this).Position;
        // The model follows the mouse, like turning a real object (the first
        // version turned it the opposite way - "feels backwards", 2026-10-15).
        _yaw -= (float)(to.X - from.X) * 0.01f;
        _pitch = Math.Clamp(_pitch - (float)(to.Y - from.Y) * 0.01f, -1.4f, 1.4f);
        _drag = to;
        Redraw();
    }

    /// <summary>Draws on a worker; while one draw runs, the latest request waits for it.</summary>
    private void Redraw()
    {
        if (_mesh is null) return;
        if (Interlocked.Exchange(ref _drawing, 1) == 1) { _pending = 1; return; }
        var mesh = _mesh; var colours = _triColours; var centre = _centre;
        float yaw = _yaw, pitch = _pitch; int w = PixelWidth, h = PixelHeight;
        Task.Run(() => Render(mesh, colours, centre, yaw, pitch, w, h)).ContinueWith(t =>
            DispatcherQueue.TryEnqueue(() =>
            {
                if (t.Result is { } pixels && ReferenceEquals(mesh, _mesh))
                {
                    var bitmap = new WriteableBitmap(w, h);
                    using (var stream = bitmap.PixelBuffer.AsStream()) stream.Write(pixels, 0, pixels.Length);
                    _image.Source = bitmap;
                }
                _drawing = 0;
                if (Interlocked.Exchange(ref _pending, 0) == 1) Redraw();
            }));
    }

    /// <summary>Rotate, fit to the frame, z-buffer, light from above-front.</summary>
    private static byte[] Render(ShipMesh mesh, uint[] colours, Vector3 centre, float yaw, float pitch, int w, int h)
    {
        var rot = Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateRotationX(pitch);
        var p = new Vector3[mesh.Corners.Length];
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < p.Length; i++)
        {
            p[i] = Vector3.Transform(mesh.Corners[i] - centre, rot);
            minX = MathF.Min(minX, p[i].X); maxX = MathF.Max(maxX, p[i].X);
            minY = MathF.Min(minY, p[i].Y); maxY = MathF.Max(maxY, p[i].Y);
        }
        // Fit what is on screen (not the 3D diagonal), with a margin.
        float scale = 0.9f * MathF.Min(w / MathF.Max(1e-3f, maxX - minX), h / MathF.Max(1e-3f, maxY - minY));
        float cx = (minX + maxX) / 2, cy = (minY + maxY) / 2;
        var zbuf = new float[w * h];
        Array.Fill(zbuf, float.MaxValue);
        var px = new uint[w * h];
        Array.Fill(px, 0x00000000u); // transparent: the card shows through
        var light = Vector3.Normalize(new Vector3(-0.35f, 0.85f, -0.4f));
        for (int t = 0; t < colours.Length; t++)
        {
            Vector3 a = p[3 * t], b = p[3 * t + 1], c = p[3 * t + 2];
            var n = Vector3.Cross(b - a, c - a);
            float len = n.Length();
            if (len < 1e-12f) continue;
            float lit = 0.38f + 0.62f * MathF.Abs(Vector3.Dot(n / len, light));
            uint col = colours[t];
            uint argb = 0xFF000000u
                | (uint)MathF.Min(255, ((col >> 16) & 0xFF) * lit) << 16
                | (uint)MathF.Min(255, ((col >> 8) & 0xFF) * lit) << 8
                | (uint)MathF.Min(255, (col & 0xFF) * lit);
            float ax = w / 2f + (a.X - cx) * scale, ay = h / 2f - (a.Y - cy) * scale;
            float bx = w / 2f + (b.X - cx) * scale, by = h / 2f - (b.Y - cy) * scale;
            float qx = w / 2f + (c.X - cx) * scale, qy = h / 2f - (c.Y - cy) * scale;
            float area = (bx - ax) * (qy - ay) - (by - ay) * (qx - ax);
            if (MathF.Abs(area) < 1e-6f) continue;
            int x0 = Math.Max(0, (int)MathF.Floor(MathF.Min(ax, MathF.Min(bx, qx))));
            int x1 = Math.Min(w - 1, (int)MathF.Ceiling(MathF.Max(ax, MathF.Max(bx, qx))));
            int y0 = Math.Max(0, (int)MathF.Floor(MathF.Min(ay, MathF.Min(by, qy))));
            int y1 = Math.Min(h - 1, (int)MathF.Ceiling(MathF.Max(ay, MathF.Max(by, qy))));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float w0 = ((bx - x) * (qy - y) - (by - y) * (qx - x)) / area;
                    float w1 = ((qx - x) * (ay - y) - (qy - y) * (ax - x)) / area;
                    float w2 = 1 - w0 - w1;
                    if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                    float z = w0 * a.Z + w1 * b.Z + w2 * c.Z;
                    int i = y * w + x;
                    if (z < zbuf[i]) { zbuf[i] = z; px[i] = argb; }
                }
        }
        var bytes = new byte[px.Length * 4];
        Buffer.BlockCopy(px, 0, bytes, 0, bytes.Length); // BGRA, premultiplied (alpha is 0 or 255)
        return bytes;
    }
}
