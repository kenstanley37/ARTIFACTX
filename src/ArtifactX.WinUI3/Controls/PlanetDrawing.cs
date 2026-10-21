using ArtifactX.Core.NmsModels;
using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

/// <summary>A planet drawn as a disc in its type's colour, with rings if it has them,
/// outlined when you're on it (PlanetDrawingHelper).</summary>
public sealed class PlanetDrawing : ContentControl
{
    public static readonly DependencyProperty PlanetProperty =
        DependencyProperty.Register(nameof(Planet), typeof(KnownPlanet), typeof(PlanetDrawing), new PropertyMetadata(null, Redraw));

    public static readonly DependencyProperty SizeProperty =
        DependencyProperty.Register(nameof(Size), typeof(double), typeof(PlanetDrawing), new PropertyMetadata(76.0, Redraw));

    public static readonly DependencyProperty HighlightProperty =
        DependencyProperty.Register(nameof(Highlight), typeof(bool), typeof(PlanetDrawing), new PropertyMetadata(false, Redraw));

    public KnownPlanet? Planet
    {
        get => (KnownPlanet?)GetValue(PlanetProperty);
        set => SetValue(PlanetProperty, value);
    }

    /// <summary>The disc's diameter; rings reach to twice that.</summary>
    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public bool Highlight
    {
        get => (bool)GetValue(HighlightProperty);
        set => SetValue(HighlightProperty, value);
    }

    public PlanetDrawing() => IsTabStop = false;

    private static void Redraw(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var drawing = (PlanetDrawing)d;
        drawing.Content = drawing.Planet is { } planet ? PlanetDrawingHelper.CreatePlanetDrawing(planet, drawing.Size, drawing.Highlight) : null;
    }
}
