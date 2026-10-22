using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class AllianceStatRing : UserControl
{
    public static readonly DependencyProperty StatProperty =
        DependencyProperty.Register(nameof(Stat), typeof(AllianceStatViewModel), typeof(AllianceStatRing),
            new PropertyMetadata(null, (d, _) => ((AllianceStatRing)d).Bindings.Update()));

    public AllianceStatViewModel? Stat
    {
        get => (AllianceStatViewModel?)GetValue(StatProperty);
        set => SetValue(StatProperty, value);
    }

    /// <summary>Words before the ring, as in the game screen's left column.</summary>
    public bool TextOnLeft
    {
        get => LeftText.Visibility == Visibility.Visible;
        set
        {
            LeftText.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            RightText.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public AllianceStatRing()
    {
        InitializeComponent();
        TextOnLeft = false;
    }
}
