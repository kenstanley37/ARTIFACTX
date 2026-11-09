using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Views.ExplorePages;

/// <summary>Read-only Reference page: the Egg Sequencer simulator
/// (EggSequencerViewModel, rules in ArtifactX.Core EggSequencer).</summary>
public sealed partial class EggSequencerPage : Page
{
    public EggSequencerViewModel ViewModel { get; } = new();

    public EggSequencerPage()
    {
        InitializeComponent();
        _ = ViewModel.LoadAsync();
    }

    public Visibility IsNotReady(bool ready) => ready ? Visibility.Collapsed : Visibility.Visible;

    private void Slot_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is EggSlotViewModel slot) ViewModel.Select(slot);
    }

    private void Item_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is EggCatalystRowViewModel row) ViewModel.Pick(row);
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => ViewModel.ClearSelectedSlot();
}
