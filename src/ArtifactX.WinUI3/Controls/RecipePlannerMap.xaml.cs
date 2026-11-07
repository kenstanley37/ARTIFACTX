using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;

namespace ArtifactX.WinUI3.Controls;

/// <summary>Custom diagram renderer: draws dependency connectors behind XAML
/// cards using their measured bounds. Stable layouts keep their visuals.</summary>
public sealed partial class RecipePlannerMap : UserControl
{
    private string _layout = "";
    private bool _queued;
    public RecipePlannerViewModel ViewModel
    {
        get => (RecipePlannerViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(RecipePlannerViewModel), typeof(RecipePlannerMap), new PropertyMetadata(null));
    public RecipePlannerMap()
    {
        InitializeComponent();
        LayoutUpdated += (_, _) => QueueDraw();
        ActualThemeChanged += (_, _) => { _layout = ""; QueueDraw(); };
    }
    private void QueueDraw()
    {
        if (_queued || !IsLoaded) return;
        _queued = true;
        DispatcherQueue.TryEnqueue(() => { _queued = false; if (IsLoaded) Draw(); });
    }
    private static IEnumerable<RecipePlannerCard> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is RecipePlannerCard card) yield return card;
            else foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private void Draw()
    {
        var cards = Descendants(Surface).Where(c => c.ViewModel is not null && c.ActualWidth > 0)
            .ToDictionary(c => c.ViewModel.TreeKey);
        var bounds = cards.ToDictionary(p => p.Key, p => p.Value.TransformToVisual(Surface)
            .TransformBounds(new Rect(0, 0, p.Value.ActualWidth, p.Value.ActualHeight)));
        string layout = string.Join(";", bounds.Select(p => $"{p.Key}:{p.Value.X:F1},{p.Value.Y:F1},{p.Value.Width:F1},{p.Value.Height:F1}"));
        layout += string.Join(";", cards.Select(p => p.Key + ":" + string.Join(",", p.Value.ViewModel.Ingredients.Select(i => i.TreeKey))));
        if (_layout == layout) return;
        _layout = layout;
        Connections.Children.Clear();
        foreach (var (id, card) in cards)
            foreach (var input in card.ViewModel.Ingredients)
            {
                if (input.TreeKey == id || !bounds.TryGetValue(input.TreeKey, out var child)) continue;
                var parent = bounds[id];
                var start = new Point(parent.X + parent.Width / 2, parent.Bottom);
                var end = new Point(child.X + child.Width / 2, child.Y);
                var figure = new PathFigure { StartPoint = start };
                figure.Segments.Add(new BezierSegment { Point1 = new(start.X, start.Y + 15),
                    Point2 = new(end.X, end.Y - 15), Point3 = end });
                var geometry = new PathGeometry(); geometry.Figures.Add(figure);
                Connections.Children.Add(new Path { Data = geometry, StrokeThickness = 1.5,
                    Stroke = ThemeResources.Brush("ThemeLineBrush"), IsHitTestVisible = false });
            }
    }
}
