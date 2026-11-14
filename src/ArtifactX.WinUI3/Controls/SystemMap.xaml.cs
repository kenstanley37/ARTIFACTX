using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Helpers.Location;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Windows.Foundation;

namespace ArtifactX.WinUI3.Controls;

/// <summary>
/// The system you're in as an orrery, like the freighter's system scanner (#65): the star in the middle in its colour,
/// the planets on rings around it in planet order, moons on small rings around their planet, the space station beside
/// the planet it's nearest in the save, and you on your planet (outlined) or beside the body you're nearest in space.
/// It's a picture, not a scale model: ring sizes, speeds and where on its ring a body sits are made up; the order, which
/// moon is whose (system generator), rings and colours are real. The save's body centres (<c>6f=.aHM</c>) are only used
/// to find which planet the station and you are by. It opens looking straight down; drag to turn and tilt, right-drag to
/// move, wheel to zoom, Home or "Top-down" to look straight down again (<see cref="MapCamera"/>). The rings turn slowly,
/// redrawn at most ~30 times a second, and stand still when Windows' animation effects are off or the map isn't shown.
/// Clicking a discovered body raises <see cref="BodyClicked"/>.
/// </summary>
public sealed partial class SystemMap : UserControl
{
    public static readonly DependencyProperty BodiesProperty = DependencyProperty.Register(nameof(Bodies),
        typeof(IReadOnlyList<SystemMapBody>), typeof(SystemMap), new PropertyMetadata(null, (d, _) => ((SystemMap)d).Rebuild()));
    public static readonly DependencyProperty StationProperty = DependencyProperty.Register(nameof(Station),
        typeof(SystemMapMarker), typeof(SystemMap), new PropertyMetadata(null, (d, _) => ((SystemMap)d).Rebuild()));
    public static readonly DependencyProperty PlayerProperty = DependencyProperty.Register(nameof(Player),
        typeof(SystemMapMarker), typeof(SystemMap), new PropertyMetadata(null, (d, _) => ((SystemMap)d).Rebuild()));
    public static readonly DependencyProperty StarColourProperty = DependencyProperty.Register(nameof(StarColour),
        typeof(string), typeof(SystemMap), new PropertyMetadata("", (d, _) => ((SystemMap)d).Rebuild()));

    public IReadOnlyList<SystemMapBody>? Bodies
    {
        get => (IReadOnlyList<SystemMapBody>?)GetValue(BodiesProperty);
        set => SetValue(BodiesProperty, value);
    }

    /// <summary>The space station; null leaves it out.</summary>
    public SystemMapMarker? Station
    {
        get => (SystemMapMarker?)GetValue(StationProperty);
        set => SetValue(StationProperty, value);
    }

    /// <summary>Where you are in space; null when you're on a planet (that body is outlined) or unknown.</summary>
    public SystemMapMarker? Player
    {
        get => (SystemMapMarker?)GetValue(PlayerProperty);
        set => SetValue(PlayerProperty, value);
    }

    /// <summary>The star's colour word ("Yellow", "Purple"...).</summary>
    public string StarColour
    {
        get => (string)GetValue(StarColourProperty);
        set => SetValue(StarColourProperty, value);
    }

    /// <summary>A discovered planet or moon was clicked.</summary>
    public event EventHandler<SystemMapBody>? BodyClicked;

    private const double PlanetSize = 20, MoonSize = 11, TopDown = 1.5;
    private const double FirstRing = 1.6, RingGap = 1.0, MoonFirstRing = 0.42, MoonRingGap = 0.2;
    private static readonly TimeSpan FrameGap = TimeSpan.FromMilliseconds(33);

    /// <summary>A body on its ring: around the star (Parent null) or around its planet.</summary>
    private sealed class Orbiter
    {
        public required SystemMapBody Body;
        public required FrameworkElement Drawing;
        public required TextBlock Label;
        public Orbiter? Parent;
        public double Radius, Start, Period;
        public Polyline? Ring;
        public (double X, double Z) At;
        public (double X, double Y, double Depth) Screen;
    }

    /// <summary>The station or you, beside a body.</summary>
    private sealed class Companion
    {
        public required Shape Shape;
        public required TextBlock Label;
        public required Orbiter Beside;
        public double Offset, Turn;
        public bool IsPlayer;
        public (double X, double Y, double Depth) Screen;
    }

    private readonly MapCamera _camera;
    private readonly List<Orbiter> _orbiters = new();
    private readonly List<Companion> _companions = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan _lastFrame;
    private bool _ticking;
    private double _outer = FirstRing;

    public SystemMap()
    {
        InitializeComponent();
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
        _camera = new MapCamera(this, Stage, FirstRing, pitch: TopDown, yaw: 0);
        _camera.Changed += Draw;
        _camera.Clicked += OnClicked;
        _camera.HomePressed += ResetView;
        Loaded += (_, _) => StartTicking();
        Unloaded += (_, _) => StopTicking();
    }

    private void ResetView() => _camera.ResetView(0, TopDown, 0, 0, 0);

    private void TopDown_Click(object sender, RoutedEventArgs e) => ResetView();

    private void StartTicking()
    {
        if (_ticking || !new Windows.UI.ViewManagement.UISettings().AnimationsEnabled) return;
        _ticking = true;
        CompositionTarget.Rendering += OnFrame;
    }

    private void StopTicking()
    {
        if (!_ticking) return;
        _ticking = false;
        CompositionTarget.Rendering -= OnFrame;
    }

    private void OnFrame(object? sender, object e)
    {
        if (Visibility != Visibility.Visible || _orbiters.Count == 0 || _clock.Elapsed - _lastFrame < FrameGap) return;
        _lastFrame = _clock.Elapsed;
        Draw();
    }

    private void Rebuild()
    {
        Stage.Children.Clear();
        _orbiters.Clear();
        _companions.Clear();
        var bodies = Bodies ?? [];
        if (bodies.Count == 0) return;

        AddStar();

        // Planets in planet order; a moon whose planet isn't in the list is drawn as a planet.
        var planets = bodies.Where(b => !b.IsMoon || bodies.All(o => o.Index != b.Host)).OrderBy(b => b.Index).ToList();
        for (int i = 0; i < planets.Count; i++)
        {
            var planet = AddOrbiter(planets[i], null, FirstRing + i * RingGap, 90 + i * 35);
            var moons = bodies.Where(b => b.IsMoon && b.Host == planets[i].Index).OrderBy(b => b.Index).ToList();
            for (int k = 0; k < moons.Count; k++)
                AddOrbiter(moons[k], planet, MoonFirstRing + k * MoonRingGap, 20 + k * 8);
        }
        _outer = FirstRing + (planets.Count - 1) * RingGap + MoonFirstRing;
        _camera.HalfExtent = _outer * 1.05;

        if (Station is { } station && Nearest(station) is { } host)
            _companions.Add(AddCompanion(station, host, new Rectangle
            {
                Width = 10, Height = 10, Fill = ThemeResources.Brush("ThemeAccentAltBrush"),
                RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new RotateTransform { Angle = 45 },
            }, "ThemeTextDimBrush", StationTurn, 0, false));
        if (Player is { } you && NearestToPlayer(you) is { } beside)
            _companions.Add(AddCompanion(you, beside.Body, new Ellipse
            {
                Width = 12, Height = 12, Fill = ThemeResources.Brush("ThemeTextBrush"),
                Stroke = ThemeResources.Brush("ThemeDamageStatBrush"), StrokeThickness = 3,
            }, "ThemeTextBrush", beside.Station ? StationTurn : -0.6, beside.Station ? 0.2 : 0, true));

        _camera.SetPivot(0, 0, 0);
    }

    private void AddStar()
    {
        var colour = PlanetDrawingHelper.ParseColour(StarColours.Hex(StarColour));
        var glow = new Ellipse
        {
            Width = 64, Height = 64, IsHitTestVisible = false,
            Fill = new RadialGradientBrush
            {
                GradientStops =
                {
                    new GradientStop { Color = ColorHelper.FromArgb(150, colour.R, colour.G, colour.B), Offset = 0 },
                    new GradientStop { Color = ColorHelper.FromArgb(0, colour.R, colour.G, colour.B), Offset = 1 },
                },
            },
        };
        var core = new Ellipse { Width = 22, Height = 22, Fill = new SolidColorBrush(colour) };
        string name = StarColour is { Length: > 0 } c ? $"{c} star" : "Star";
        ToolTipService.SetToolTip(core, name);
        AutomationProperties.SetName(core, name);
        Stage.Children.Add(glow);
        Stage.Children.Add(core);
        _star = (glow, core);
    }

    private (Ellipse Glow, Ellipse Core)? _star;

    private Orbiter AddOrbiter(SystemMapBody body, Orbiter? parent, double radius, double period)
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
        string type = body.Planet is KnownPlanet kp
            ? $" · {kp.Classification(PlanetTypesService.Descriptor(kp.SystemAddress, kp.PlanetIndex), isMoon: body.IsMoon)}" : "";
        string tip = $"{body.Name}\n{body.Kind}{type}" + (body.Planet is null ? "\nNot discovered yet" : "");
        ToolTipService.SetToolTip(drawing, tip);
        AutomationProperties.SetName(drawing, tip.Replace('\n', ','));
        var label = Label(body.Name, body.IsMoon ? 11 : 12, body.IsHere ? "ThemeAccentBrush" : body.IsMoon ? "ThemeTextDimBrush" : "ThemeTextBrush");
        var ring = new Polyline
        {
            Stroke = ThemeResources.Brush(body.IsHere ? "ThemeAccentBrush" : "ThemeLineBrush"),
            StrokeThickness = body.IsHere ? 1.4 : 1, Opacity = parent is null ? 0.7 : 0.5, IsHitTestVisible = false,
        };
        Stage.Children.Add(ring);
        Stage.Children.Add(drawing);
        Stage.Children.Add(label);
        var orbiter = new Orbiter
        {
            Body = body, Drawing = drawing, Label = label, Parent = parent, Radius = radius, Period = period, Ring = ring,
            // Spread round the ring (golden angle), the same for a system every time.
            Start = body.Index * 2.39996 + (parent is null ? 0 : 1.1),
        };
        _orbiters.Add(orbiter);
        return orbiter;
    }

    /// <summary>How far round from straight out (away from the star) the station sits beside its planet; you sit just
    /// past it when you're at the station.</summary>
    private const double StationTurn = 0.6;

    private Companion AddCompanion(SystemMapMarker marker, SystemMapBody beside, Shape shape, string labelBrush, double turn, double extra, bool isPlayer)
    {
        string tip = $"{marker.Name}\n{marker.Detail}";
        ToolTipService.SetToolTip(shape, tip);
        AutomationProperties.SetName(shape, tip.Replace('\n', ','));
        var label = Label(marker.Name, 11, labelBrush);
        Stage.Children.Add(shape);
        Stage.Children.Add(label);
        var orbiter = _orbiters.First(o => o.Body == beside);
        return new Companion { Shape = shape, Label = label, Beside = orbiter, Offset = (orbiter.Body.IsMoon ? 0.16 : orbiter.Body.Planet?.HasRings == true ? 0.45 : 0.3) + extra, Turn = turn, IsPlayer = isPlayer };
    }

    private static TextBlock Label(string text, double size, string brush)
    {
        var label = new TextBlock { Text = text, FontSize = size, IsHitTestVisible = false, Foreground = ThemeResources.Brush(brush) };
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return label;
    }

    /// <summary>The planet whose centre in the save is nearest the marker (a moon's planet, for a moon).</summary>
    private SystemMapBody? Nearest(SystemMapMarker marker)
    {
        var bodies = Bodies ?? [];
        var nearest = bodies.MinBy(b => Distance(b.X, b.Y, b.Z, marker));
        return nearest is { IsMoon: true } moon && bodies.FirstOrDefault(b => b.Index == moon.Host) is { } host ? host : nearest;
    }

    /// <summary>The body you're nearest in space, or the station's planet when the station is nearer than any body.</summary>
    private (SystemMapBody Body, bool Station)? NearestToPlayer(SystemMapMarker you)
    {
        var bodies = Bodies ?? [];
        var nearest = bodies.MinBy(b => Distance(b.X, b.Y, b.Z, you));
        if (nearest is null) return null;
        if (Station is { } station && Distance(station.X, station.Y, station.Z, you) < Distance(nearest.X, nearest.Y, nearest.Z, you)
            && Nearest(station) is { } stationPlanet)
            return (stationPlanet, true);
        return (nearest, false);
    }

    private static double Distance(double x, double y, double z, SystemMapMarker m) =>
        Math.Sqrt((x - m.X) * (x - m.X) + (y - m.Y) * (y - m.Y) + (z - m.Z) * (z - m.Z));

    /// <summary>Move everything to where it is now, project, paint (nearest on top) and set the labels apart.</summary>
    private void Draw()
    {
        if (Stage.ActualWidth <= 0 || Stage.ActualHeight <= 0 || _orbiters.Count == 0) return;
        double seconds = _ticking ? _clock.Elapsed.TotalSeconds : 0;
        int Z(double depth) => (int)(depth * 1000 / Math.Max(_camera.HalfExtent, 1)) + 10000;

        if (_star is { } star)
        {
            var (glow, core) = star;
            var s = _camera.Project(0, 0, 0);
            Place(glow, s.X, s.Y);
            Place(core, s.X, s.Y);
            Canvas.SetZIndex(glow, Z(s.Depth) - 1);
            Canvas.SetZIndex(core, Z(s.Depth));
        }

        // Planets first, so a moon can follow its planet.
        foreach (var o in _orbiters)
        {
            double angle = o.Start + seconds * 2 * Math.PI / o.Period;
            var (cx, cz) = o.Parent?.At ?? (0, 0);
            o.At = (cx + o.Radius * Math.Cos(angle), cz + o.Radius * Math.Sin(angle));
            o.Screen = _camera.Project(o.At.X, 0, o.At.Z);
            Place(o.Drawing, o.Screen.X, o.Screen.Y);
            Canvas.SetZIndex(o.Drawing, Z(o.Screen.Depth) + (o.Body.IsHere ? 100000 : 0));
            if (o.Ring is { } ring)
            {
                var points = new PointCollection();
                int steps = o.Parent is null ? 72 : 32;
                for (int k = 0; k <= steps; k++)
                {
                    double a = k * 2 * Math.PI / steps;
                    var p = _camera.Project(cx + o.Radius * Math.Cos(a), 0, cz + o.Radius * Math.Sin(a));
                    points.Add(new Point(p.X, p.Y));
                }
                ring.Points = points;
                Canvas.SetZIndex(ring, 1);
            }
        }
        foreach (var c in _companions)
        {
            var (bx, bz) = c.Beside.At;
            double outward = Math.Atan2(bz, bx) + c.Turn;
            c.Screen = _camera.Project(bx + c.Offset * Math.Cos(outward), 0, bz + c.Offset * Math.Sin(outward));
            Place(c.Shape, c.Screen.X, c.Screen.Y);
            Canvas.SetZIndex(c.Shape, Z(c.Screen.Depth) + (c.IsPlayer ? 150000 : 120000));
        }
        PlaceLabels();
    }

    private static void Place(FrameworkElement element, double x, double y)
    {
        Canvas.SetLeft(element, x - element.Width / 2);
        Canvas.SetTop(element, y - element.Height / 2);
    }

    /// <summary>Each label right of its body, else left, below or above - the first spot that doesn't cover a label
    /// already placed. You, the planet you're on and planets go first; moons and the station after.</summary>
    private void PlaceLabels()
    {
        var items = _orbiters.Select(o => (o.Label, o.Screen, Half: o.Drawing.Height / 2 + (o.Body.Planet?.HasRings == true ? 10 : 3),
                Rank: o.Body.IsHere ? 0 : o.Body.IsMoon ? 2 : 1, Own: (FrameworkElement)o.Drawing))
            .Concat(_companions.Select(c => (c.Label, c.Screen, Half: c.Shape.Height / 2 + 3, Rank: c.IsPlayer ? 0 : 3, Own: (FrameworkElement)c.Shape)))
            .OrderBy(i => i.Rank).ToList();
        // Labels keep off the star and every other body as well as each other.
        var bodies = _orbiters.Select(o => (Box: Box(o.Drawing, o.Body.Planet?.HasRings == true), Element: (FrameworkElement)o.Drawing))
            .Concat(_companions.Select(c => (Box: Box(c.Shape, true), Element: (FrameworkElement)c.Shape)))
            .Concat(_star is { } star ? [(Box: Box(star.Core, true), Element: (FrameworkElement)star.Core)] : []).ToList();
        var taken = new List<Rect>();
        foreach (var (label, at, half, _, own) in items)
        {
            double w = label.DesiredSize.Width, h = label.DesiredSize.Height;
            Point[] spots =
            [
                new(at.X + half, at.Y - h / 2), new(at.X - half - w, at.Y - h / 2),
                new(at.X - w / 2, at.Y + half), new(at.X - w / 2, at.Y - half - h),
            ];
            var chosen = spots[0];
            foreach (var spot in spots)
            {
                var rect = new Rect(spot.X, spot.Y, w, h);
                if (taken.Any(t => Overlaps(t, rect)) || bodies.Any(b => !ReferenceEquals(b.Element, own) && Overlaps(b.Box, rect))) continue;
                chosen = spot;
                break;
            }
            taken.Add(new Rect(chosen.X, chosen.Y, w, h));
            Canvas.SetLeft(label, chosen.X);
            Canvas.SetTop(label, chosen.Y);
            Canvas.SetZIndex(label, 300000);
        }
    }

    /// <summary>The space a drawing covers: its full width with rings, else just its disc (planet drawings are twice as
    /// wide as tall, to leave room for rings).</summary>
    private static Rect Box(FrameworkElement element, bool fullWidth)
    {
        double w = fullWidth ? element.Width : element.Height;
        return new(Canvas.GetLeft(element) + (element.Width - w) / 2, Canvas.GetTop(element), w, element.Height);
    }

    private static bool Overlaps(Rect a, Rect b) => a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;

    /// <summary>The nearest discovered body under the pointer.</summary>
    private void OnClicked(Point point)
    {
        SystemMapBody? hit = null;
        int best = int.MinValue;
        foreach (var o in _orbiters)
        {
            double size = Math.Max(o.Drawing.Height, 14);
            double cx = Canvas.GetLeft(o.Drawing) + o.Drawing.Width / 2, cy = Canvas.GetTop(o.Drawing) + o.Drawing.Height / 2;
            if (Math.Abs(point.X - cx) > size / 2 || Math.Abs(point.Y - cy) > size / 2) continue;
            int z = Canvas.GetZIndex(o.Drawing);
            if (z > best) { best = z; hit = o.Body; }
        }
        if (hit is { Planet: not null }) BodyClicked?.Invoke(this, hit);
    }
}
