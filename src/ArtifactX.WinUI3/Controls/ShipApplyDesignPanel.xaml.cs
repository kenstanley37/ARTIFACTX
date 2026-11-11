using ArtifactX.WinUI3.ViewModels.ShipDesignerViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

/// <summary>The "Apply the design to which ship?" choice; state and rules in <see cref="ApplyDesignViewModel"/>.</summary>
public sealed partial class ShipApplyDesignPanel : UserControl
{
    public ShipApplyDesignPanel(ApplyDesignViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public ApplyDesignViewModel ViewModel { get; }

    private void Rename_Click(object sender, RoutedEventArgs e) => ViewModel.RenameClicked();
}
