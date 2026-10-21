using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class SettlementMaterialsPanel : UserControl
{
    public SettlementMaterialsPanel() => InitializeComponent();

    public SettlementMaterialsViewModel ViewModel { get; } = new();
}
