using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;

namespace ArtifactX.WinUI3.Controls;

/// <summary>A brief pulse on a field the game just changed (LiveFieldsService), so the
/// eye finds what moved. Plays only with Windows' animation effects on.</summary>
public static class FieldHighlight
{
    public static void Flash(UIElement element)
    {
        if (!new Windows.UI.ViewManagement.UISettings().AnimationsEnabled) return;

        var transform = new CompositeTransform();
        element.RenderTransform = transform;
        element.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);

        var storyboard = new Storyboard();
        Add(storyboard, element, "Opacity", (0, 0.25), (350, 1), (550, 0.5), (900, 1));
        Add(storyboard, transform, "ScaleX", (0, 1), (350, 1.04), (650, 1));
        Add(storyboard, transform, "ScaleY", (0, 1), (350, 1.04), (650, 1));
        storyboard.Completed += (_, _) => element.Opacity = 1;
        storyboard.Begin();
    }

    private static void Add(Storyboard storyboard, DependencyObject target, string property, params (int Ms, double Value)[] frames)
    {
        var animation = new DoubleAnimationUsingKeyFrames { EnableDependentAnimation = false };
        foreach (var (ms, value) in frames)
            animation.KeyFrames.Add(new EasingDoubleKeyFrame
            {
                KeyTime = TimeSpan.FromMilliseconds(ms),
                Value = value,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            });
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        storyboard.Children.Add(animation);
    }
}
