using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class StationClaimCard : UserControl
{
    public static readonly DependencyProperty ClaimProperty =
        DependencyProperty.Register(nameof(Claim), typeof(StationClaimViewModel), typeof(StationClaimCard), new PropertyMetadata(null, (d, _) => ((StationClaimCard)d).Bindings.Update()));

    public StationClaimViewModel? Claim
    {
        get => (StationClaimViewModel?)GetValue(ClaimProperty);
        set => SetValue(ClaimProperty, value);
    }

    /// <summary>Hide (or Show) was clicked.</summary>
    public event EventHandler<StationClaimViewModel>? HideToggled;

    public StationClaimCard() => InitializeComponent();

    private void Hide_Click(object sender, RoutedEventArgs e)
    {
        if (Claim is { } claim) HideToggled?.Invoke(this, claim);
    }
}
