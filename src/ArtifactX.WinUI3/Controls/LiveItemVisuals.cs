using ArtifactX.WinUI3.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;

namespace ArtifactX.WinUI3.Controls;

/// <summary>
/// How a selector strip (Multi-Tool, Starships) shows what LiveItemsService
/// found while the game played: a new item's button slides in with a short
/// glow and keeps a "NEW" pill and accent outline until it's clicked; an item
/// the game no longer has stays as a faded button until the save is reloaded.
/// </summary>
public static class LiveItemVisuals
{
    /// <summary>Accent outline and a "NEW" pill at the end of the button's content.</summary>
    public static void MarkNew(Button button, Panel content)
    {
        button.BorderBrush = ThemeResources.Brush("ThemeAccentBrush");
        button.BorderThickness = new Thickness(2);
        content.Children.Add(Pill("NEW", "ThemeAccentBrush", "ThemeOnAccentBrush"));
        ToolTipService.SetToolTip(button, "New in your game");
    }

    /// <summary>Slides and fades the button in, then pops it once so the eye
    /// lands on it. Plays only with Windows' animation effects on.</summary>
    public static void SlideIn(Button button)
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
        button.Loaded += (_, _) => storyboard.Begin();
    }

    /// <summary>A faded stand-in for something the game no longer has.</summary>
    public static Button Gone(GoneItem item)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        content.Children.Add(new TextBlock
        {
            Text = item.Name,
            TextDecorations = Windows.UI.Text.TextDecorations.Strikethrough,
            VerticalAlignment = VerticalAlignment.Center,
        });
        content.Children.Add(Pill("GONE", "ThemeLineBrush", "ThemeTextBrush"));
        var button = new Button
        {
            Content = content,
            Padding = new Thickness(12, 6, 12, 6),
            Opacity = 0.45,
        };
        ThemeResources.StyleChoiceButton(button, false);
        ToolTipService.SetToolTip(button, "No longer in your game - sold, scrapped or traded in No Man's Sky. It goes the next time this save is opened.");
        return button;
    }

    private static Border Pill(string text, string background, string foreground) => new()
    {
        Background = ThemeResources.Brush(background),
        CornerRadius = ThemeResources.CardCornerRadius,
        Padding = new Thickness(6, 1, 6, 1),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock
        {
            Text = text,
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            FontFamily = ThemeResources.Font("ThemeDisplayFont"),
            CharacterSpacing = 120,
            Foreground = ThemeResources.Brush(foreground),
        },
    };

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
