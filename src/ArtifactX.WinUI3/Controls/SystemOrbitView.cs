using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtifactX.WinUI3.Controls;

/// <summary>
/// The system you're in as a small orrery: the star, one orbit per known planet, moons
/// circling their own planet (BodyKindsService). Orbits turn slowly, outer ones slower;
/// nothing moves when Windows' "Animation effects" is off. Orbit spacing, planet sizes and
/// the star's colour are decorative - the save stores none of them. Scales to its size.
/// </summary>
public sealed class SystemOrbitView : UserControl
{
    private const double Centre = 180;

    public static readonly DependencyProperty BodiesProperty =
        DependencyProperty.Register(nameof(Bodies), typeof(IReadOnlyList<KnownPlanet>), typeof(SystemOrbitView), new PropertyMetadata(null, Redraw));

    public static readonly DependencyProperty CurrentPlanetProperty =
        DependencyProperty.Register(nameof(CurrentPlanet), typeof(int), typeof(SystemOrbitView), new PropertyMetadata(0, Redraw));

    /// <summary>The system's known planets and moons.</summary>
    public IReadOnlyList<KnownPlanet>? Bodies
    {
        get => (IReadOnlyList<KnownPlanet>?)GetValue(BodiesProperty);
        set => SetValue(BodiesProperty, value);
    }

    /// <summary>The planet you're on (0 in space), drawn larger and outlined.</summary>
    public int CurrentPlanet
    {
        get => (int)GetValue(CurrentPlanetProperty);
        set => SetValue(CurrentPlanetProperty, value);
    }

    private readonly Canvas _canvas = new() { Width = Centre * 2, Height = Centre * 2 };
    private readonly List<Storyboard> _animations = new();

    public SystemOrbitView()
    {
        Content = new Viewbox { Stretch = Stretch.Uniform, Child = _canvas };
        Loaded += (_, _) => Draw();
        Unloaded += (_, _) => Stop();
    }

    private static void Redraw(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (SystemOrbitView)d;
        if (view.IsLoaded) view.Draw();
    }

    private void Stop()
    {
        foreach (var storyboard in _animations) storyboard.Stop();
        _animations.Clear();
    }

    private void Draw()
    {
        Stop();
        _canvas.Children.Clear();
        var allBodies = Bodies ?? [];
        int current = CurrentPlanet;

        // Moons orbit their own planet rather than the star - unless that planet isn't known yet,
        // then they're drawn like planets.
        ulong system = allBodies.FirstOrDefault()?.SystemAddress ?? 0;
        var moonsOf = allBodies
            .Select(p => (Moon: p, Host: BodyKindsService.Host(system, p.PlanetIndex)))
            .Where(m => m.Host != 0)
            .Select(m => (m.Moon, Host: allBodies.FirstOrDefault(p => p.PlanetIndex == (m.Host > 0 ? m.Host : 1))))
            .Where(m => m.Host is not null && m.Host != m.Moon)
            .GroupBy(m => m.Host!).ToDictionary(g => g.Key, g => g.Select(m => m.Moon).ToList());
        var planets = allBodies.Except(moonsOf.Values.SelectMany(m => m)).ToList();
        bool animate = new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;

        Place(new Ellipse
        {
            Width = 90, Height = 90,
            Fill = new RadialGradientBrush
            {
                GradientStops =
                {
                    new GradientStop { Color = ColorHelper.FromArgb(150, 255, 214, 140), Offset = 0 },
                    new GradientStop { Color = ColorHelper.FromArgb(0, 255, 180, 90), Offset = 1 },
                },
            },
        });
        Place(new Ellipse { Width = 30, Height = 30, Fill = new SolidColorBrush(ColorHelper.FromArgb(255, 255, 236, 200)) });

        for (int i = 0; i < planets.Count; i++)
        {
            var planet = planets[i];
            double radius = 42 + (i + 1) * (130.0 / Math.Max(planets.Count, 1));
            bool here = planet.PlanetIndex == current;

            Place(new Ellipse
            {
                Width = radius * 2, Height = radius * 2,
                Stroke = ThemeResources.Brush(here ? "ThemeAccentBrush" : "ThemeLineBrush"),
                StrokeThickness = here ? 1.4 : 1,
                Opacity = here ? 0.9 : 0.6,
            });

            FrameworkElement body = PlanetDrawingHelper.CreatePlanetDrawing(planet, here ? 22 : 16, here);
            if (moonsOf.TryGetValue(planet, out var moons)) body = WithMoons((Grid)body, moons, current, animate);

            // The arm spins around the star; the body counter-spins so its ring keeps its tilt.
            var arm = new Grid { Width = radius * 2, Height = radius * 2 };
            body.HorizontalAlignment = HorizontalAlignment.Center;
            body.VerticalAlignment = VerticalAlignment.Top;
            body.Margin = new Thickness(0, -body.Height / 2, 0, 0);
            arm.Children.Add(body);
            Place(arm);

            double startAngle = (planet.PlanetIndex * 67 + (int)(planet.Address & 0x3F)) % 360;
            var armTurn = new RotateTransform { Angle = startAngle };
            arm.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            arm.RenderTransform = armTurn;
            var bodyTurn = new RotateTransform { Angle = -startAngle };
            body.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            body.RenderTransform = bodyTurn;

            if (!animate) continue;
            double seconds = 40 + i * 22;
            var storyboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
            storyboard.Children.Add(Spin(armTurn, startAngle, startAngle + 360, seconds));
            storyboard.Children.Add(Spin(bodyTurn, -startAngle, -startAngle - 360, seconds));
            storyboard.Begin();
            _animations.Add(storyboard);
        }
    }

    /// <summary>A planet's drawing with its moons circling it on small orbits.</summary>
    private Grid WithMoons(Grid planetDrawing, List<KnownPlanet> moons, int current, bool animate)
    {
        double extent = 34 + moons.Count * 16;
        var group = new Grid { Width = extent, Height = extent };
        planetDrawing.HorizontalAlignment = HorizontalAlignment.Center;
        planetDrawing.VerticalAlignment = VerticalAlignment.Center;
        group.Children.Add(planetDrawing);

        for (int k = 0; k < moons.Count; k++)
        {
            var moon = moons[k];
            bool here = moon.PlanetIndex == current;
            double orbitRadius = 15 + (k + 1) * 8;
            double size = here ? 9 : 7;

            group.Children.Add(new Ellipse
            {
                Width = orbitRadius * 2, Height = orbitRadius * 2,
                Stroke = ThemeResources.Brush("ThemeLineBrush"), StrokeThickness = 0.8, Opacity = 0.5,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            });
            var arm = new Grid
            {
                Width = orbitRadius * 2, Height = orbitRadius * 2,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
            };
            arm.Children.Add(new Ellipse
            {
                Width = size, Height = size,
                Fill = new SolidColorBrush(PlanetDrawingHelper.ParseColour(NmsPlanets.TypeColour(moon.BiomeIndex))),
                Stroke = here ? ThemeResources.Brush("ThemeAccentBrush") : null,
                StrokeThickness = here ? 1.5 : 0,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -size / 2, 0, 0),
            });
            double start = (moon.PlanetIndex * 113) % 360;
            var turn = new RotateTransform { Angle = start };
            arm.RenderTransform = turn;
            group.Children.Add(arm);

            if (!animate) continue;
            var storyboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
            storyboard.Children.Add(Spin(turn, start, start + 360, 9 + k * 5));
            storyboard.Begin();
            _animations.Add(storyboard);
        }
        return group;
    }

    private static DoubleAnimation Spin(RotateTransform target, double from, double to, double seconds)
    {
        var animation = new DoubleAnimation { From = from, To = to, Duration = TimeSpan.FromSeconds(seconds) };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, "Angle");
        return animation;
    }

    private void Place(FrameworkElement element)
    {
        Canvas.SetLeft(element, Centre - element.Width / 2);
        Canvas.SetTop(element, Centre - element.Height / 2);
        _canvas.Children.Add(element);
    }
}
