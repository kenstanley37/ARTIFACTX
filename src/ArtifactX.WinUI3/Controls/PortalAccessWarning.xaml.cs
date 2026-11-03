using ArtifactX.Core.Universe;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

/// <summary>A shared, general warning; never reports a save's unlock state.</summary>
public sealed partial class PortalAccessWarning : UserControl
{
    public static readonly DependencyProperty CodeProperty = DependencyProperty.Register(
        nameof(Code), typeof(string), typeof(PortalAccessWarning), new PropertyMetadata("", OnCodeChanged));

    public string Code
    {
        get => (string)GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    public PortalAccessWarning() => InitializeComponent();

    private static void OnCodeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((PortalAccessWarning)sender).Visibility = PortalTravel.RequiresPurpleStarAccess(args.NewValue as string)
            ? Visibility.Visible : Visibility.Collapsed;
}
