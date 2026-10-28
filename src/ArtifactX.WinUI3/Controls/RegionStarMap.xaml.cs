using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.System;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;

namespace ArtifactX.WinUI3.Controls;

/// <summary>
/// A region's stars in 3D from their generated positions (RegionStarPositions), drawn as dots on a
/// canvas: unvisited systems bright, visited ones dim, unvisited purple systems violet,
/// your system a large white dot in a ring. The view turns and zooms around a pivot star, as the game's
/// galaxy map locks onto a system: the selected star (your system until another is clicked) glides to the
/// centre. Drag to turn, right-drag or Shift+drag to move, mouse wheel to zoom, click a star to raise
/// <see cref="StarSelected"/>; arrow keys turn, +/- zoom and Home returns to your system.
/// Drawing the dots is the control's job, so it builds them in code; the page binds Stars and Selected.
/// </summary>
public sealed partial class RegionStarMap : UserControl
{
    public static readonly DependencyProperty StarsProperty = DependencyProperty.Register(nameof(Stars),
        typeof(IReadOnlyList<RegionMapStar>), typeof(RegionStarMap), new PropertyMetadata(null, (d, _) => ((RegionStarMap)d).Rebuild()));
    public static readonly DependencyProperty SelectedProperty = DependencyProperty.Register(nameof(Selected),
        typeof(RegionMapStar), typeof(RegionStarMap), new PropertyMetadata(null, (d, _) => ((RegionStarMap)d).CentreOnSelected()));

    public IReadOnlyList<RegionMapStar>? Stars
    {
        get => (IReadOnlyList<RegionMapStar>?)GetValue(StarsProperty);
        set => SetValue(StarsProperty, value);
    }

    public RegionMapStar? Selected
    {
        get => (RegionMapStar?)GetValue(SelectedProperty);
        set => SetValue(SelectedProperty, value);
    }

    /// <summary>A star was clicked.</summary>
    public event EventHandler<RegionMapStar>? StarSelected;

    // Positions run about -50..50 on each axis, a few grid stars slightly beyond, so no region box is drawn.
    private const double RegionHalf = 50;
    private readonly List<(Ellipse Dot, RegionMapStar Star)> _dots = new();
    private double _yaw = 0.6, _pitch = 0.35, _zoom = 1;
    private (double X, double Y, double Z) _pivot, _glideFrom, _glideTo;
    private DateTime _glideStart;
    private bool _gliding;
    private static readonly TimeSpan GlideTime = TimeSpan.FromMilliseconds(350);
    private Point? _pressed;
    private bool _turning, _panning;

    public RegionStarMap()
    {
        InitializeComponent();
        SizeChanged += (_, _) =>
        {
            Stage.Clip = new RectangleGeometry { Rect = new Rect(0, 0, Stage.ActualWidth, Stage.ActualHeight) };
            Draw();
        };
        Stage.PointerPressed += OnPressed;
        Stage.PointerMoved += OnMoved;
        Stage.PointerReleased += OnReleased;
        Stage.PointerWheelChanged += OnWheel;
        IsTabStop = true;
        KeyDown += OnKeyDown;
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
    }

    private void Rebuild()
    {
        foreach (var (dot, _) in _dots) Stage.Children.Remove(dot);
        _dots.Clear();
        foreach (var star in Stars ?? [])
        {
            var dot = new Ellipse();
            ToolTipService.SetToolTip(dot, $"{star.Name}\n{(star.IsPurple ? $"System {star.System} (purple)" : $"System {star.System}")} · {star.Status}");
            _dots.Add((dot, star));
            Stage.Children.Add(dot);
        }
        // Centre on the picked star, else on the middle of this region's stars (not the last region's pivot).
        if (Selected is { } selected) _pivot = (selected.X, selected.Y, selected.Z);
        else if (_dots.Count > 0)
            _pivot = (_dots.Average(d => d.Star.X), _dots.Average(d => d.Star.Y), _dots.Average(d => d.Star.Z));
        StopGlide();
        Draw();
    }

    /// <summary>Glide the pivot to the selected star (ease-out), so turning and zooming happen around it.</summary>
    private void CentreOnSelected()
    {
        if (Selected is not { } star) { Draw(); return; }
        _glideFrom = _pivot;
        _glideTo = (star.X, star.Y, star.Z);
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
        if (t >= 1 || !IsLoaded) StopGlide();
        Draw();
    }

    private double PixelsPerUnit => Math.Min(Stage.ActualWidth, Stage.ActualHeight) / (RegionHalf * 2.6) * _zoom;

    /// <summary>Rotate (yaw about the vertical axis, then pitch), project and paint, nearest on top.</summary>
    private void Draw()
    {
        double width = Stage.ActualWidth, height = Stage.ActualHeight;
        if (width <= 0 || height <= 0) return;
        double scale = PixelsPerUnit;
        double cy = Math.Cos(_yaw), sy = Math.Sin(_yaw), cp = Math.Cos(_pitch), sp = Math.Sin(_pitch);
        (double X, double Y, double Depth) Project(double x, double y, double z)
        {
            x -= _pivot.X; y -= _pivot.Y; z -= _pivot.Z;
            double rx = x * cy + z * sy, rz = -x * sy + z * cy;
            double ry = y * cp - rz * sp, depth = y * sp + rz * cp;
            return (width / 2 + rx * scale, height / 2 - ry * scale, depth);
        }

        var visited = ThemeResources.Brush("ThemeTextDimBrush");
        var unvisited = ThemeResources.Brush("ThemeAccentBrush");
        var purple = ThemeResources.Brush("ThemePurpleSystemBrush");
        var here = ThemeResources.Brush("ThemeDamageStatBrush");
        var white = ThemeResources.Brush("ThemeTextBrush");
        foreach (var (dot, star) in _dots)
        {
            var p = Project(star.X, star.Y, star.Z);
            double size = Math.Clamp((star.Visited ? 4.5 : 6.5) * (1 + p.Depth / (RegionHalf * 4)) * Math.Sqrt(_zoom), 2.5, 16);
            bool isSelected = ReferenceEquals(star, Selected);
            // Your system: a white dot in a thick ring, bigger than anything else, so it never blends in.
            if (star.IsHere) size = Math.Max(size, 8) + 8;
            else if (isSelected) size += 6;
            dot.Width = dot.Height = size;
            dot.Fill = star.IsHere ? white : star.IsPurple && !star.Visited ? purple : star.Visited ? visited : unvisited;
            dot.Opacity = star.Visited && !star.IsHere ? 0.55 : 1;
            dot.Stroke = star.IsHere ? here : isSelected ? white : null;
            dot.StrokeThickness = star.IsHere ? 3 : isSelected ? 2 : 0;
            Canvas.SetLeft(dot, p.X - size / 2);
            Canvas.SetTop(dot, p.Y - size / 2);
            Canvas.SetZIndex(dot, (int)(p.Depth * 10) + (star.IsHere || isSelected ? 100000 : 0));
        }
    }

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(Stage);
        _pressed = point.Position;
        _turning = false;
        // Right or middle button, or Shift with the left (touchpads), moves the view instead of turning it.
        _panning = point.Properties.IsRightButtonPressed || point.Properties.IsMiddleButtonPressed
            || (e.KeyModifiers & VirtualKeyModifiers.Shift) != 0;
        Focus(FocusState.Pointer);
        Stage.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    /// <summary>Move the pivot so the stars follow the pointer: the screen offset turned back into region space.</summary>
    private void Pan(double dx, double dy)
    {
        double scale = PixelsPerUnit;
        if (scale <= 0) return;
        double rx = -dx / scale, ry = dy / scale;
        double cy = Math.Cos(_yaw), sy = Math.Sin(_yaw), cp = Math.Cos(_pitch), sp = Math.Sin(_pitch);
        double y = ry * cp, rz = -ry * sp;
        double x = rx * cy - rz * sy, z = rx * sy + rz * cy;
        StopGlide();
        _pivot = (_pivot.X + x, _pivot.Y + y, _pivot.Z + z);
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_pressed is not { } start) return;
        var now = e.GetCurrentPoint(Stage).Position;
        double dx = now.X - start.X, dy = now.Y - start.Y;
        if (!_turning && Math.Abs(dx) + Math.Abs(dy) < 4) return;
        _turning = true;
        if (_panning) Pan(dx, dy);
        else
        {
            _yaw += dx * 0.01;
            _pitch = Math.Clamp(_pitch + dy * 0.01, -1.5, 1.5);
        }
        _pressed = now;
        Draw();
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        Stage.ReleasePointerCapture(e.Pointer);
        bool click = !_turning && !_panning;
        _pressed = null;
        _panning = false;
        _turning = false;
        if (!click) return;
        var point = e.GetCurrentPoint(Stage).Position;
        // The topmost dot under the pointer (nearest to the viewer wins).
        (Ellipse Dot, RegionMapStar Star)? hit = null;
        foreach (var entry in _dots)
        {
            double left = Canvas.GetLeft(entry.Dot), top = Canvas.GetTop(entry.Dot), size = Math.Max(entry.Dot.Width, 10);
            double cx = left + entry.Dot.Width / 2, cyDot = top + entry.Dot.Height / 2;
            if (Math.Abs(point.X - cx) > size / 2 || Math.Abs(point.Y - cyDot) > size / 2) continue;
            if (hit is null || Canvas.GetZIndex(entry.Dot) > Canvas.GetZIndex(hit.Value.Dot)) hit = entry;
        }
        if (hit is { } found) StarSelected?.Invoke(this, found.Star);
    }

    private void OnWheel(object sender, PointerRoutedEventArgs e)
    {
        int delta = e.GetCurrentPoint(Stage).Properties.MouseWheelDelta;
        _zoom = Math.Clamp(_zoom * (delta > 0 ? 1.15 : 1 / 1.15), 0.6, 12);
        Draw();
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
            case VirtualKey.Home:
                foreach (var (_, star) in _dots)
                    if (star.IsHere) { StarSelected?.Invoke(this, star); break; }
                break;
            default: return;
        }
        Draw();
        e.Handled = true;
    }
}
