using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace ArtifactX.WinUI3.Controls;

/// <summary>See the doc comment in AnimatedExpander.xaml for why this exists
/// instead of the native Expander control. IsExpanded supports the same
/// TwoWay x:Bind usage the native control did - toggling it (from a header
/// tap here, or set externally, e.g. SaveFolderSelectViewModel's accordion
/// logic collapsing a sibling) always plays the matching animation, since
/// both paths go through the same OnIsExpandedChanged callback.</summary>
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

        if ((bool)e.NewValue)
        {
            // Must be visible before the fade-in starts, or there's nothing to animate.
            self.BodyHost.Visibility = Visibility.Visible;
            self.ExpandStoryboard.Begin();
        }
        else
        {
            // Begin() on this smoothly takes over from wherever ExpandStoryboard
            // currently has Opacity/Y/Angle, even mid-flight - no need to Stop()
            // the other storyboard first. Actually hiding BodyHost happens in
            // CollapseStoryboard_Completed, not here, guarded against the case
            // where a rapid re-expand fires before that completion callback runs.
            self.CollapseStoryboard.Begin();
        }
    }

    private void CollapseStoryboard_Completed(object sender, object e)
    {
        if (!IsExpanded) BodyHost.Visibility = Visibility.Collapsed;
    }

    private void HeaderRoot_Tapped(object sender, TappedRoutedEventArgs e) => IsExpanded = !IsExpanded;
}
