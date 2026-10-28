using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using Windows.Foundation;

namespace ArtifactX.WinUI3.Controls;

/// <summary>
/// A region's stars in 3D from their generated positions (RegionStarPositions), drawn as dots on a
/// canvas: unvisited systems bright, visited ones dim, purple systems in the theme's second accent,
/// your system ringed. Drag to turn, mouse wheel to zoom, click a star to raise <see cref="StarSelected"/>.
/// Drawing the dots is the control's job, so it builds them in code; the page binds Stars and Selected.
/// </summary>
public sealed partial class RegionStarMap : UserControl
{
    public static readonly DependencyProperty StarsProperty = DependencyProperty.Register(nameof(Stars),
        typeof(IReadOnlyList<RegionMapStar>), typeof(RegionStarMap), new PropertyMetadata(null, (d, _) => ((RegionStarMap)d).Rebuild()));
    public static readonly DependencyProperty SelectedProperty = DependencyProperty.Register(nameof(Selected),
        typeof(RegionMapStar), typeof(RegionStarMap), new PropertyMetadata(null, (d, _) => ((RegionStarMap)d).Draw()));

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

    private const double RegionHalf = 50; // positions run about -50..50 on each axis
    private readonly List<(Ellipse Dot, RegionMapStar Star)> _dots = new();
    private readonly List<Line> _edges = new();
    private double _yaw = 0.6, _pitch = 0.35, _zoom = 1;
    private Point? _pressed;
    private bool _turning;

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
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
        for (int i = 0; i < 12; i++)
        {
            var edge = new Line { Stroke = ThemeResources.Brush("ThemeLineBrush"), StrokeThickness = 1, IsHitTestVisible = false };
            _edges.Add(edge);
            Stage.Children.Add(edge);
        }
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
        Draw();
    }

    /// <summary>Rotate (yaw about the vertical axis, then pitch), project and paint, nearest on top.</summary>
    private void Draw()
    {
        double width = Stage.ActualWidth, height = Stage.ActualHeight;
        if (width <= 0 || height <= 0) return;
        double scale = Math.Min(width, height) / (RegionHalf * 2.6) * _zoom;
        double cy = Math.Cos(_yaw), sy = Math.Sin(_yaw), cp = Math.Cos(_pitch), sp = Math.Sin(_pitch);
        (double X, double Y, double Depth) Project(double x, double y, double z)
        {
            double rx = x * cy + z * sy, rz = -x * sy + z * cy;
            double ry = y * cp - rz * sp, depth = y * sp + rz * cp;
            return (width / 2 + rx * scale, height / 2 - ry * scale, depth);
        }

        int[,] cube = { { 0, 1 }, { 1, 3 }, { 3, 2 }, { 2, 0 }, { 4, 5 }, { 5, 7 }, { 7, 6 }, { 6, 4 }, { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 } };
        for (int i = 0; i < 12; i++)
        {
            var a = Corner(cube[i, 0]);
            var b = Corner(cube[i, 1]);
            var pa = Project(a.X, a.Y, a.Z);
            var pb = Project(b.X, b.Y, b.Z);
            (_edges[i].X1, _edges[i].Y1, _edges[i].X2, _edges[i].Y2) = (pa.X, pa.Y, pb.X, pb.Y);
        }

        var visited = ThemeResources.Brush("ThemeTextDimBrush");
        var unvisited = ThemeResources.Brush("ThemeAccentBrush");
        var purple = ThemeResources.Brush("ThemeAccentAltBrush");
        var here = ThemeResources.Brush("ThemeDamageStatBrush");
        var selected = ThemeResources.Brush("ThemeTextBrush");
        foreach (var (dot, star) in _dots)
        {
            var p = Project(star.X, star.Y, star.Z);
            double size = Math.Clamp((star.Visited ? 4.5 : 6.5) * (1 + p.Depth / (RegionHalf * 4)) * Math.Sqrt(_zoom), 2.5, 16);
            bool isSelected = ReferenceEquals(star, Selected);
            if (star.IsHere || isSelected) size += 6;
            dot.Width = dot.Height = size;
            dot.Fill = star.IsPurple && !star.Visited ? purple : star.Visited ? visited : unvisited;
            dot.Opacity = star.Visited && !star.IsHere ? 0.55 : 1;
            dot.Stroke = star.IsHere ? here : isSelected ? selected : null;
            dot.StrokeThickness = star.IsHere || isSelected ? 2 : 0;
            Canvas.SetLeft(dot, p.X - size / 2);
            Canvas.SetTop(dot, p.Y - size / 2);
            Canvas.SetZIndex(dot, (int)(p.Depth * 10) + (star.IsHere || isSelected ? 100000 : 0));
        }
    }

    private static (double X, double Y, double Z) Corner(int i) =>
        ((i & 1) != 0 ? RegionHalf : -RegionHalf, (i & 2) != 0 ? RegionHalf : -RegionHalf, (i & 4) != 0 ? RegionHalf : -RegionHalf);

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        _pressed = e.GetCurrentPoint(Stage).Position;
        _turning = false;
        Stage.CapturePointer(e.Pointer);
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_pressed is not { } start) return;
        var now = e.GetCurrentPoint(Stage).Position;
        double dx = now.X - start.X, dy = now.Y - start.Y;
        if (!_turning && Math.Abs(dx) + Math.Abs(dy) < 4) return;
        _turning = true;
        _yaw += dx * 0.01;
        _pitch = Math.Clamp(_pitch + dy * 0.01, -1.5, 1.5);
        _pressed = now;
        Draw();
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        Stage.ReleasePointerCapture(e.Pointer);
        bool click = !_turning;
        _pressed = null;
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
        _zoom = Math.Clamp(_zoom * (delta > 0 ? 1.15 : 1 / 1.15), 0.6, 6);
        Draw();
        e.Handled = true;
    }
}
