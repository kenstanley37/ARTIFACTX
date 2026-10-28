using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.Foundation;
using Windows.System;

namespace ArtifactX.WinUI3.Controls;

/// <summary>
/// The camera and mouse/keyboard handling shared by the 3D maps (<see cref="RegionStarMap"/>, <see cref="SystemMap"/>):
/// the view turns and zooms around a pivot point, like the game's galaxy map locks onto a system. Drag to turn,
/// right-drag (or middle-drag, or Shift+drag on a touchpad) to move, mouse wheel to zoom toward the pointer;
/// arrow keys turn, +/- zoom and Home raises <see cref="HomePressed"/>. The map control draws; it calls
/// <see cref="Project"/> for each point and redraws on <see cref="Changed"/>.
/// </summary>
public sealed class MapCamera
{
    private static readonly TimeSpan GlideTime = TimeSpan.FromMilliseconds(350);
    private readonly UserControl _host;
    private readonly Canvas _stage;
    private double _yaw = 0.6, _pitch = 0.35, _zoom = 1;
    private double _cosYaw, _sinYaw, _cosPitch, _sinPitch;
    private (double X, double Y, double Z) _pivot, _glideFrom, _glideTo;
    private DateTime _glideStart;
    private bool _gliding, _turning, _panning;
    private Point? _pressed;

    /// <summary>Half the width of the space the map shows at zoom 1, in the map's own units.</summary>
    public double HalfExtent { get; set; }

    public double Zoom => _zoom;

    /// <summary>The view moved, turned or zoomed: redraw.</summary>
    public event Action? Changed;

    /// <summary>A click (press and release without dragging) at this point on the stage.</summary>
    public event Action<Point>? Clicked;

    /// <summary>Home was pressed: return to the player's own place.</summary>
    public event Action? HomePressed;

    /// <param name="pitch">The starting tilt: 0 looks side-on, 1.5 straight down.</param>
    public MapCamera(UserControl host, Canvas stage, double halfExtent, double pitch = 0.35)
    {
        _host = host;
        _pitch = pitch;
        _stage = stage;
        HalfExtent = halfExtent;
        Angles();
        stage.SizeChanged += (_, _) =>
        {
            stage.Clip = new RectangleGeometry { Rect = new Rect(0, 0, stage.ActualWidth, stage.ActualHeight) };
            Changed?.Invoke();
        };
        stage.PointerPressed += OnPressed;
        stage.PointerMoved += OnMoved;
        stage.PointerReleased += OnReleased;
        stage.PointerWheelChanged += OnWheel;
        host.IsTabStop = true;
        host.KeyDown += OnKeyDown;
    }

    public double PixelsPerUnit => HalfExtent <= 0 ? 0 : Math.Min(_stage.ActualWidth, _stage.ActualHeight) / (HalfExtent * 2.6) * _zoom;

    /// <summary>Screen position and depth (larger is nearer) of a point: yaw about the vertical axis, then pitch.</summary>
    public (double X, double Y, double Depth) Project(double x, double y, double z)
    {
        double scale = PixelsPerUnit;
        x -= _pivot.X; y -= _pivot.Y; z -= _pivot.Z;
        double rx = x * _cosYaw + z * _sinYaw, rz = -x * _sinYaw + z * _cosYaw;
        double ry = y * _cosPitch - rz * _sinPitch, depth = y * _sinPitch + rz * _cosPitch;
        return (_stage.ActualWidth / 2 + rx * scale, _stage.ActualHeight / 2 - ry * scale, depth);
    }

    /// <summary>Put the pivot here at once (a new set of points).</summary>
    public void SetPivot(double x, double y, double z)
    {
        StopGlide();
        _pivot = (x, y, z);
        Changed?.Invoke();
    }

    /// <summary>Glide the pivot here (ease-out), so turning and zooming happen around it.</summary>
    public void GlideTo(double x, double y, double z)
    {
        _glideFrom = _pivot;
        _glideTo = (x, y, z);
        _glideStart = DateTime.UtcNow;
        if (_gliding) return;
        _gliding = true;
        CompositionTarget.Rendering += OnGlideFrame;
    }

    private void StopGlide()
    {
        if (!_gliding) return;
        CompositionTarget.Rendering -= OnGlideFrame;
        _gliding = false;
    }

    private void OnGlideFrame(object? sender, object e)
    {
        double t = Math.Min(1, (DateTime.UtcNow - _glideStart) / GlideTime);
        double k = 1 - Math.Pow(1 - t, 3);
        _pivot = (_glideFrom.X + (_glideTo.X - _glideFrom.X) * k, _glideFrom.Y + (_glideTo.Y - _glideFrom.Y) * k,
            _glideFrom.Z + (_glideTo.Z - _glideFrom.Z) * k);
        if (t >= 1 || !_host.IsLoaded) StopGlide();
        Changed?.Invoke();
    }

    private void Angles()
    {
        (_cosYaw, _sinYaw, _cosPitch, _sinPitch) = (Math.Cos(_yaw), Math.Sin(_yaw), Math.Cos(_pitch), Math.Sin(_pitch));
    }

    /// <summary>Move the pivot by (rx, ry) map units along the screen's right and up directions.</summary>
    private void MovePivot(double rx, double ry)
    {
        double y = ry * _cosPitch, rz = -ry * _sinPitch;
        double x = rx * _cosYaw - rz * _sinYaw, z = rx * _sinYaw + rz * _cosYaw;
        StopGlide();
        _pivot = (_pivot.X + x, _pivot.Y + y, _pivot.Z + z);
    }

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_stage);
        _pressed = point.Position;
        _turning = false;
        _panning = point.Properties.IsRightButtonPressed || point.Properties.IsMiddleButtonPressed
            || (e.KeyModifiers & VirtualKeyModifiers.Shift) != 0;
        _host.Focus(FocusState.Pointer);
        _stage.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_pressed is not { } start) return;
        var now = e.GetCurrentPoint(_stage).Position;
        double dx = now.X - start.X, dy = now.Y - start.Y;
        if (!_turning && Math.Abs(dx) + Math.Abs(dy) < 4) return;
        _turning = true;
        if (_panning)
        {
            // The points follow the pointer: the screen offset turned back into map space.
            double scale = PixelsPerUnit;
            if (scale > 0) MovePivot(-dx / scale, dy / scale);
        }
        else
        {
            _yaw += dx * 0.01;
            _pitch = Math.Clamp(_pitch + dy * 0.01, -1.5, 1.5);
            Angles();
        }
        _pressed = now;
        Changed?.Invoke();
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        _stage.ReleasePointerCapture(e.Pointer);
        bool click = !_turning && !_panning;
        _pressed = null;
        _panning = false;
        _turning = false;
        if (click) Clicked?.Invoke(e.GetCurrentPoint(_stage).Position);
    }

    private void OnWheel(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_stage);
        double before = PixelsPerUnit;
        _zoom = Math.Clamp(_zoom * (point.Properties.MouseWheelDelta > 0 ? 1.15 : 1 / 1.15), 0.6, 12);
        double after = PixelsPerUnit;
        // Zoom toward the pointer: shift the pivot so the spot under the cursor stays put on screen.
        if (before > 0 && after > 0)
        {
            double ox = point.Position.X - _stage.ActualWidth / 2, oy = point.Position.Y - _stage.ActualHeight / 2;
            double k = 1 / before - 1 / after;
            MovePivot(ox * k, -oy * k);
        }
        Changed?.Invoke();
        e.Handled = true;
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        const VirtualKey Plus = (VirtualKey)187, Minus = (VirtualKey)189; // the main keyboard's =/+ and - keys
        switch (e.Key)
        {
            case VirtualKey.Left: _yaw -= 0.1; break;
            case VirtualKey.Right: _yaw += 0.1; break;
            case VirtualKey.Up: _pitch = Math.Clamp(_pitch - 0.1, -1.5, 1.5); break;
            case VirtualKey.Down: _pitch = Math.Clamp(_pitch + 0.1, -1.5, 1.5); break;
            case VirtualKey.Add or Plus: _zoom = Math.Min(12, _zoom * 1.15); break;
            case VirtualKey.Subtract or Minus: _zoom = Math.Max(0.6, _zoom / 1.15); break;
            case VirtualKey.Home: HomePressed?.Invoke(); e.Handled = true; return;
            default: return;
        }
        Angles();
        Changed?.Invoke();
        e.Handled = true;
    }
}
