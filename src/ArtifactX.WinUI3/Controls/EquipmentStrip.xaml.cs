using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class EquipmentStrip : UserControl
{
    public static readonly DependencyProperty ItemsProperty =
        DependencyProperty.Register(nameof(Items), typeof(IReadOnlyList<EquipmentChipViewModel>), typeof(EquipmentStrip), new PropertyMetadata(null));

    public IReadOnlyList<EquipmentChipViewModel>? Items
    {
        get => (IReadOnlyList<EquipmentChipViewModel>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>An item or an empty slot was clicked (GONE ones do nothing).</summary>
    public event EventHandler<EquipmentChipViewModel>? ChipClicked;

    public EquipmentStrip() => InitializeComponent();

    private void Chip_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is EquipmentChipViewModel { IsGone: false } chip)
            ChipClicked?.Invoke(this, chip);
    }

    private void Chip_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is EquipmentChipViewModel { AnimateIn: true })
            SlideIn(button);
    }

    /// <summary>Slides and fades a new item's button in, then pops it once so the eye
    /// lands on it. Plays only with Windows' animation effects on.</summary>
    private static void SlideIn(Button button)
    {
        if (!new Windows.UI.ViewManagement.UISettings().AnimationsEnabled) return;

        var transform = new CompositeTransform();
        button.RenderTransform = transform;
        button.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        button.Opacity = 0;

        var storyboard = new Storyboard();
        Add(storyboard, button, "Opacity", (0, 0), (450, 1));
        Add(storyboard, transform, "TranslateY", (0, 18), (450, 0));
        Add(storyboard, transform, "ScaleX", (0, 1), (450, 1), (600, 1.08), (800, 1));
        Add(storyboard, transform, "ScaleY", (0, 1), (450, 1), (600, 1.08), (800, 1));
        storyboard.Completed += (_, _) => button.Opacity = 1;
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
