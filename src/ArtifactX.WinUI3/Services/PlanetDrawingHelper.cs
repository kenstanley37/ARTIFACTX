using ArtifactX.Core.NmsModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Globalization;

namespace ArtifactX.WinUI3.Services;

/// <summary>
/// Renders standard planet drawings (biome color circle, rings, accent outline for current location)
/// used by both the system overview and the planet details page.
/// </summary>
public static class PlanetDrawingHelper
{
    public static Windows.UI.Color ParseColour(string hex)
    {
        if (string.IsNullOrEmpty(hex) || hex[0] != '#') return Windows.UI.Color.FromArgb(255, 128, 128, 128);
        uint rgb = uint.Parse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return Windows.UI.Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }

    public static Grid CreatePlanetDrawing(KnownPlanet planet, double size, bool highlight)
    {
        var grid = new Grid { Width = size * 2, Height = size };
        grid.Children.Add(new Ellipse
        {
            Width = size,
            Height = size,
            Fill = new SolidColorBrush(ParseColour(NmsPlanets.TypeColour(planet.BiomeIndex))),
            Stroke = highlight ? ThemeResources.Brush("ThemeAccentBrush") : null,
            StrokeThickness = highlight ? 2 : 0,
        });
        if (planet.HasRings)
        {
            grid.Children.Add(new Ellipse
            {
                Width = size * 1.9,
                Height = size * 0.45,
                Stroke = ThemeResources.Brush("ThemeTextDimBrush"),
                StrokeThickness = 1.5,
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
                RenderTransform = new RotateTransform { Angle = -18 },
            });
        }
        return grid;
    }
}
