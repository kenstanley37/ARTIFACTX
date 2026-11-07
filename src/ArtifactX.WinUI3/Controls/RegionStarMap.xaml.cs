using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;

namespace ArtifactX.WinUI3.Controls;

/// <summary>
/// A region's stars in 3D from their generated positions (RegionStarPositions), drawn as dots on a
/// canvas: unvisited systems bright, visited ones dim, unvisited purple systems violet,
/// your system a large white dot in a ring. The view (<see cref="MapCamera"/>) turns and zooms around a
/// pivot star: the selected star (your system until another is clicked) glides to the centre. Click a star
/// to raise <see cref="StarSelected"/>; Home returns to your system.
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
    private readonly MapCamera _camera;

    public RegionStarMap()
    {
        InitializeComponent();
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
        _camera = new MapCamera(this, Stage, RegionHalf);
        _camera.Changed += Draw;
        _camera.Clicked += OnClicked;
        _camera.HomePressed += () =>
        {
            if (_dots.FirstOrDefault(d => d.Star.IsHere).Star is { } here) StarSelected?.Invoke(this, here);
        };
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
        if (Selected is { } selected) _camera.SetPivot(selected.X, selected.Y, selected.Z);
        else if (_dots.Count > 0)
            _camera.SetPivot(_dots.Average(d => d.Star.X), _dots.Average(d => d.Star.Y), _dots.Average(d => d.Star.Z));
        else Draw();
    }

    private void CentreOnSelected()
    {
        if (Selected is { } star) _camera.GlideTo(star.X, star.Y, star.Z);
        else Draw();
    }

    /// <summary>Project and paint, nearest on top.</summary>
    private void Draw()
    {
        if (Stage.ActualWidth <= 0 || Stage.ActualHeight <= 0) return;
        var visited = ThemeResources.Brush("ThemeTextDimBrush");
        var unvisited = ThemeResources.Brush("ThemeAccentBrush");
        var purple = ThemeResources.Brush("ThemePurpleSystemBrush");
        var here = ThemeResources.Brush("ThemeDamageStatBrush");
        var white = ThemeResources.Brush("ThemeTextBrush");
        double zoom = Math.Sqrt(_camera.Zoom);
        foreach (var (dot, star) in _dots)
        {
            var p = _camera.Project(star.X, star.Y, star.Z);
            double size = Math.Clamp((star.Visited ? 4.5 : 6.5) * (1 + p.Depth / (RegionHalf * 4)) * zoom, 2.5, 16);
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

    /// <summary>The topmost dot under the pointer (nearest to the viewer wins).</summary>
    private void OnClicked(Point point)
    {
        (Ellipse Dot, RegionMapStar Star)? hit = null;
        foreach (var entry in _dots)
        {
            double left = Canvas.GetLeft(entry.Dot), top = Canvas.GetTop(entry.Dot), size = Math.Max(entry.Dot.Width, 10);
            double cx = left + entry.Dot.Width / 2, cy = top + entry.Dot.Height / 2;
            if (Math.Abs(point.X - cx) > size / 2 || Math.Abs(point.Y - cy) > size / 2) continue;
            if (hit is null || Canvas.GetZIndex(entry.Dot) > Canvas.GetZIndex(hit.Value.Dot)) hit = entry;
        }
        if (hit is { } found) StarSelected?.Invoke(this, found.Star);
    }
}
