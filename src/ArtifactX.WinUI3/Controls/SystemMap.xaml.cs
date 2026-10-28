using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;

namespace ArtifactX.WinUI3.Controls;

/// <summary>
/// The system you're in as a 3D map: every planet and moon at its real centre from the save
/// (<c>6f=.aHM</c>), moons tied to the planet they orbit (system generator; moons sit 226,000 units from
/// their planet, matched against nine save systems 2026-10-27), the body you're on outlined, and the space
/// station (<see cref="SystemMapStation"/>) as a diamond. Discovered bodies use the planet drawing (biome
/// colour, rings; Dead planets are grey); undiscovered ones are a dashed outline. The sun isn't placed yet.
/// Same controls as the region map (<see cref="MapCamera"/>);
/// clicking a discovered body raises <see cref="BodyClicked"/>.
/// </summary>
public sealed partial class SystemMap : UserControl
{
    public static readonly DependencyProperty BodiesProperty = DependencyProperty.Register(nameof(Bodies),
        typeof(IReadOnlyList<SystemMapBody>), typeof(SystemMap), new PropertyMetadata(null, (d, _) => ((SystemMap)d).Rebuild()));

    public IReadOnlyList<SystemMapBody>? Bodies
    {
        get => (IReadOnlyList<SystemMapBody>?)GetValue(BodiesProperty);
        set => SetValue(BodiesProperty, value);
    }

    public static readonly DependencyProperty StationProperty = DependencyProperty.Register(nameof(Station),
        typeof(SystemMapStation), typeof(SystemMap), new PropertyMetadata(null, (d, _) => ((SystemMap)d).Rebuild()));

    /// <summary>The space station, drawn as a diamond; null leaves it out.</summary>
    public SystemMapStation? Station
    {
        get => (SystemMapStation?)GetValue(StationProperty);
        set => SetValue(StationProperty, value);
    }

    /// <summary>A discovered planet or moon was clicked.</summary>
    public event EventHandler<SystemMapBody>? BodyClicked;

    private const double PlanetSize = 20, MoonSize = 11;
    private readonly List<(FrameworkElement Drawing, TextBlock Label, SystemMapBody Body)> _bodies = new();
    private readonly List<(Line Line, SystemMapBody Moon, SystemMapBody Host)> _tethers = new();
    private readonly MapCamera _camera;
    private (double X, double Y, double Z) _middle;
    private (Rectangle Marker, TextBlock Label)? _station;

    public SystemMap()
    {
        InitializeComponent();
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
        _camera = new MapCamera(this, Stage, 1);
        _camera.Changed += Draw;
        _camera.Clicked += OnClicked;
        _camera.HomePressed += () =>
        {
            if (_bodies.FirstOrDefault(b => b.Body.IsHere).Body is { } here) _camera.GlideTo(here.X, here.Y, here.Z);
            else _camera.GlideTo(_middle.X, _middle.Y, _middle.Z);
        };
    }

    private void Rebuild()
    {
        Stage.Children.Clear();
        _bodies.Clear();
        _tethers.Clear();
        _station = null;
        var bodies = Bodies ?? [];
        var line = ThemeResources.Brush("ThemeLineBrush");
        foreach (var moon in bodies.Where(b => b.IsMoon))
            if (bodies.FirstOrDefault(b => b.Index == moon.Host) is { } host)
            {
                var tether = new Line { Stroke = line, StrokeThickness = 1, Opacity = 0.8, IsHitTestVisible = false };
                _tethers.Add((tether, moon, host));
                Stage.Children.Add(tether);
            }
        foreach (var body in bodies)
        {
            double size = body.IsMoon ? MoonSize : PlanetSize;
            FrameworkElement drawing = body.Planet is KnownPlanet planet
                ? PlanetDrawingHelper.CreatePlanetDrawing(planet, size, body.IsHere)
                // Not discovered: a dashed outline, so it can't be mistaken for a grey (Dead) planet.
                : new Ellipse
                {
                    Width = size, Height = size, Fill = ThemeResources.Brush("ThemeVoidBrush"),
                    Stroke = ThemeResources.Brush(body.IsHere ? "ThemeAccentBrush" : "ThemeTextDimBrush"), StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 2, 2 },
                };
            ToolTipService.SetToolTip(drawing, $"{body.Name}\n{body.Kind}" + (body.Planet is null ? "\nNot discovered yet" : ""));
            var label = new TextBlock
            {
                Text = body.Name, FontSize = body.IsMoon ? 11 : 12, IsHitTestVisible = false,
                Foreground = ThemeResources.Brush(body.IsHere ? "ThemeAccentBrush" : body.IsMoon ? "ThemeTextDimBrush" : "ThemeTextBrush"),
            };
            _bodies.Add((drawing, label, body));
            Stage.Children.Add(drawing);
            Stage.Children.Add(label);
        }

        if (Station is { } station && bodies.Count > 0)
        {
            var marker = new Rectangle
            {
                Width = 10, Height = 10, Fill = ThemeResources.Brush("ThemeAccentAltBrush"),
                RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new RotateTransform { Angle = 45 },
            };
            ToolTipService.SetToolTip(marker, $"{station.Name}\nSpace station");
            var label = new TextBlock { Text = station.Name, FontSize = 11, IsHitTestVisible = false, Foreground = ThemeResources.Brush("ThemeTextDimBrush") };
            _station = (marker, label);
            Stage.Children.Add(marker);
            Stage.Children.Add(label);
        }

        // Fit the whole system, centred on its middle.
        if (bodies.Count > 0)
        {
            _middle = (bodies.Average(b => b.X), bodies.Average(b => b.Y), bodies.Average(b => b.Z));
            _camera.HalfExtent = Math.Max(1, bodies.Max(b => Math.Max(Math.Abs(b.X - _middle.X),
                Math.Max(Math.Abs(b.Y - _middle.Y), Math.Abs(b.Z - _middle.Z))))) * 1.1;
        }
        _camera.SetPivot(_middle.X, _middle.Y, _middle.Z);
    }

    /// <summary>Project and paint, nearest on top; labels sit right of their body.</summary>
    private void Draw()
    {
        if (Stage.ActualWidth <= 0 || Stage.ActualHeight <= 0) return;
        var placed = new Dictionary<int, (double X, double Y, double Depth)>();
        foreach (var (drawing, label, body) in _bodies)
        {
            var p = _camera.Project(body.X, body.Y, body.Z);
            placed[body.Index] = p;
            // The planet drawing is twice as wide as tall (room for rings), so centre on both halves.
            Canvas.SetLeft(drawing, p.X - drawing.Width / 2);
            Canvas.SetTop(drawing, p.Y - drawing.Height / 2);
            Canvas.SetLeft(label, p.X + drawing.Height / 2 + (body.Planet?.HasRings == true ? 12 : 5));
            Canvas.SetTop(label, p.Y - 8);
            int z = (int)(p.Depth * 1000 / Math.Max(_camera.HalfExtent, 1)) + 10000;
            Canvas.SetZIndex(drawing, z + (body.IsHere ? 100000 : 0));
            Canvas.SetZIndex(label, z + 200000);
        }
        if (_station is var (stationMarker, stationLabel) && Station is { } station)
        {
            var p = _camera.Project(station.X, station.Y, station.Z);
            Canvas.SetLeft(stationMarker, p.X - 5);
            Canvas.SetTop(stationMarker, p.Y - 5);
            Canvas.SetLeft(stationLabel, p.X + 9);
            Canvas.SetTop(stationLabel, p.Y - 8);
            int z = (int)(p.Depth * 1000 / Math.Max(_camera.HalfExtent, 1)) + 10000;
            Canvas.SetZIndex(stationMarker, z);
            Canvas.SetZIndex(stationLabel, z + 200000);
        }
        foreach (var (tether, moon, host) in _tethers)
        {
            var a = placed[moon.Index];
            var b = placed[host.Index];
            (tether.X1, tether.Y1, tether.X2, tether.Y2) = (a.X, a.Y, b.X, b.Y);
        }
    }

    /// <summary>The nearest discovered body under the pointer.</summary>
    private void OnClicked(Point point)
    {
        SystemMapBody? hit = null;
        int best = int.MinValue;
        foreach (var (drawing, _, body) in _bodies)
        {
            double size = Math.Max(drawing.Height, 14);
            double cx = Canvas.GetLeft(drawing) + drawing.Width / 2, cy = Canvas.GetTop(drawing) + drawing.Height / 2;
            if (Math.Abs(point.X - cx) > size / 2 || Math.Abs(point.Y - cy) > size / 2) continue;
            int z = Canvas.GetZIndex(drawing);
            if (z > best) { best = z; hit = body; }
        }
        if (hit is { Planet: not null }) BodyClicked?.Invoke(this, hit);
    }
}
