using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace ArtifactX.WinUI3.Helpers.Layout;

/// <summary>
/// When a page's pinned summary bar (<see cref="Controls.PinnedSummary"/>) should show: once the page's own
/// header card has scrolled (nearly) out of view, so the summary never repeats what is still on screen.
/// </summary>
public static class PinnedHeader
{
    /// <summary>How much of the header may still show, in pixels, when the summary takes over.</summary>
    private const double Overlap = 24;

    public static bool IsScrolledAway(ScrollViewer scroller, FrameworkElement header)
    {
        if (header.ActualHeight <= 0 || !header.IsLoaded || header.Visibility != Visibility.Visible) return false;
        var bottom = header.TransformToVisual(scroller).TransformPoint(new Point(0, header.ActualHeight));
        return bottom.Y <= Overlap;
    }
}
