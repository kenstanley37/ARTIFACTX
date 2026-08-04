using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using Windows.Foundation;

namespace ArtifactX.WinUI3.Controls;

/// <summary>See the doc comment in AnimatedExpander.xaml for why this exists
/// instead of the native Expander control, and why it animates Height rather
/// than a fade. IsExpanded supports the same TwoWay x:Bind usage the native
/// control did - toggling it (from a header tap here, or set externally,
/// e.g. SaveFolderSelectViewModel's accordion logic collapsing a sibling)
/// always plays the matching animation, since both paths go through the
/// same OnIsExpandedChanged callback.</summary>
public sealed partial class AnimatedExpander : UserControl
{
    public static readonly DependencyProperty HeaderContentProperty =
        DependencyProperty.Register(nameof(HeaderContent), typeof(object), typeof(AnimatedExpander), new PropertyMetadata(null));
    public object HeaderContent
    {
        get => GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    public static readonly DependencyProperty BodyContentProperty =
        DependencyProperty.Register(nameof(BodyContent), typeof(object), typeof(AnimatedExpander), new PropertyMetadata(null));
    public object BodyContent
    {
        get => GetValue(BodyContentProperty);
        set => SetValue(BodyContentProperty, value);
    }

    /// <summary>Independent of IsExpanded - marks this card as the one
    /// SaveSessionManager is actually working within (see
    /// SaveFolderCandidate.IsActivePlatform), not just whichever is open.</summary>
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(AnimatedExpander), new PropertyMetadata(false));
    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(AnimatedExpander),
            new PropertyMetadata(false, OnIsExpandedChanged));
    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public AnimatedExpander()
    {
        InitializeComponent();
    }

    private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (AnimatedExpander)d;

        if ((bool)e.NewValue) self.BeginExpand();
        else self.BeginCollapse();
    }

    /// <summary>Measures BodyHost's natural desired height - independent of
    /// its CURRENT (collapsed, Height=0) arranged size - so the animation's
    /// target correctly reflects whatever's actually bound right now (a
    /// loading spinner, or the full slot grid if indexing already finished),
    /// then animates Height from 0 up to that value.</summary>
    private void BeginExpand()
    {
        double availableWidth = ActualWidth > 0 ? ActualWidth : double.PositiveInfinity;
        BodyHost.Measure(new Size(availableWidth, double.PositiveInfinity));

        ExpandHeightAnimation.To = BodyHost.DesiredSize.Height;
        ExpandStoryboard.Begin();
    }

    /// <summary>Height is Auto (released in ExpandStoryboard_Completed) while
    /// steady-state expanded, so it can't be animated FROM directly - Auto
    /// isn't a real number. Pins Height to ActualHeight first (the exact same
    /// visual size Auto was already rendering, so this causes no jump), then
    /// animates from that pinned value down to 0.</summary>
    private void BeginCollapse()
    {
        BodyHost.Height = BodyHost.ActualHeight;
        CollapseStoryboard.Begin();
    }

    private void ExpandStoryboard_Completed(object sender, object e)
    {
        if (IsExpanded) BodyHost.Height = double.NaN;
    }

    private void CollapseStoryboard_Completed(object sender, object e)
    {
        // Guards against a rapid re-expand firing before this late completion
        // callback runs - without this, it would stomp Height back to 0 on
        // content that's supposed to be visible again.
        if (!IsExpanded) BodyHost.Height = 0;
    }

    /// <summary>Keeps BodyHost's Clip in sync with its current animated size
    /// every frame, so BodyContent - arranged at its own natural size (see
    /// the ContentPresenter's Top alignment in XAML), not squashed to fit -
    /// is progressively revealed rather than reflowed as Height grows.</summary>
    private void BodyHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        BodyHost.Clip = new RectangleGeometry
        {
            Rect = new Rect(0, 0, Math.Max(0, e.NewSize.Width), Math.Max(0, e.NewSize.Height))
        };
    }

    private void HeaderRoot_Tapped(object sender, TappedRoutedEventArgs e) => IsExpanded = !IsExpanded;
}
