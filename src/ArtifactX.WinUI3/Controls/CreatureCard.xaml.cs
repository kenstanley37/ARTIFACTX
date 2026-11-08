using ArtifactX.WinUI3.ViewModels.ExploreViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ArtifactX.WinUI3.Controls;

/// <summary>Which creature a CreatureCard's button was for, and the button (to show the result beside).</summary>
public sealed record CreatureCardRequest(CreatureCardViewModel Creature, FrameworkElement Anchor);

public sealed partial class CreatureCard : UserControl
{
    public static readonly DependencyProperty CreatureProperty =
        DependencyProperty.Register(nameof(Creature), typeof(CreatureCardViewModel), typeof(CreatureCard), new PropertyMetadata(null));

    public CreatureCardViewModel? Creature
    {
        get => (CreatureCardViewModel?)GetValue(CreatureProperty);
        set => SetValue(CreatureProperty, value);
    }

    /// <summary>"Take picture" was clicked.</summary>
    public event EventHandler<CreatureCardRequest>? CaptureRequested;

    /// <summary>"Remove" was clicked.</summary>
    public event EventHandler<CreatureCardRequest>? DeleteRequested;

    /// <summary>"View 3D model" was clicked.</summary>
    public event EventHandler<CreatureCardRequest>? Preview3DRequested;

    public CreatureCard() => InitializeComponent();

    private async void View3DModel_Click(object sender, RoutedEventArgs e)
    {
        if (Creature is { } creature)
        {
            if (Preview3DRequested is not null)
                Preview3DRequested.Invoke(this, new(creature, (FrameworkElement)sender));
            else
                await CreaturePreviewControl.ShowAsync(XamlRoot, creature.Creature);
        }
    }

    private void Capture_Click(object sender, RoutedEventArgs e)
    {
        if (Creature is { } creature) CaptureRequested?.Invoke(this, new(creature, (FrameworkElement)sender));
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Creature is { } creature) DeleteRequested?.Invoke(this, new(creature, (FrameworkElement)sender));
    }
}
