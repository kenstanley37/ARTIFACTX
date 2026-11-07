using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

/// <summary>The Frigates page's Feed Vessel predictor (FrigateFeedingViewModel).</summary>
public sealed partial class FrigateFeedingPanel : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(FrigateFeedingViewModel), typeof(FrigateFeedingPanel), new PropertyMetadata(null));

    public FrigateFeedingViewModel ViewModel
    {
        get => (FrigateFeedingViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public FrigateFeedingPanel() => InitializeComponent();

    private void AddToExosuit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is FrigateFoodRowViewModel row) ViewModel.AddToExosuit(row);
    }
}
