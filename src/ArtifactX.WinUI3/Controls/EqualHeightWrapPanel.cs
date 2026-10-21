using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Foundation;

namespace ArtifactX.WinUI3.Controls;

/// <summary>A wrap panel whose children are all as tall as the tallest - for rows of cards
/// that should line up (the planet cards on Current System, 2026-10-20), so a card with
/// less in it doesn't leave a ragged edge.</summary>
public sealed class EqualHeightWrapPanel : Panel
{
    public static readonly DependencyProperty HorizontalSpacingProperty =
        DependencyProperty.Register(nameof(HorizontalSpacing), typeof(double), typeof(EqualHeightWrapPanel), new PropertyMetadata(0.0, Relayout));

    public static readonly DependencyProperty VerticalSpacingProperty =
        DependencyProperty.Register(nameof(VerticalSpacing), typeof(double), typeof(EqualHeightWrapPanel), new PropertyMetadata(0.0, Relayout));

    public double HorizontalSpacing
    {
        get => (double)GetValue(HorizontalSpacingProperty);
        set => SetValue(HorizontalSpacingProperty, value);
    }

    public double VerticalSpacing
    {
        get => (double)GetValue(VerticalSpacingProperty);
        set => SetValue(VerticalSpacingProperty, value);
    }

    private double _rowHeight;

    private static void Relayout(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((EqualHeightWrapPanel)d).InvalidateMeasure();

    protected override Size MeasureOverride(Size availableSize)
    {
        _rowHeight = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            _rowHeight = Math.Max(_rowHeight, child.DesiredSize.Height);
        }
        return Layout(availableSize.Width, arrange: false);
    }

    protected override Size ArrangeOverride(Size finalSize) => Layout(finalSize.Width, arrange: true);

    private Size Layout(double width, bool arrange)
    {
        double x = 0, y = 0, widest = 0;
        bool rowStarted = false;
        foreach (var child in Children)
        {
            double w = child.DesiredSize.Width;
            if (rowStarted && x + w > width)
            {
                x = 0;
                y += _rowHeight + VerticalSpacing;
            }
            if (arrange) child.Arrange(new Rect(x, y, w, _rowHeight));
            widest = Math.Max(widest, x + w);
            x += w + HorizontalSpacing;
            rowStarted = true;
        }
        return new Size(double.IsInfinity(width) ? widest : Math.Max(widest, 0), rowStarted ? y + _rowHeight : 0);
    }
}
