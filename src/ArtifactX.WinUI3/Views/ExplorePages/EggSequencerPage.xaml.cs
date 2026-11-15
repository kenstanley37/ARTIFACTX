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
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        _ = ViewModel.LoadAsync();
    }

    /// <summary>Draws the picked egg's creature (the panel is a control, so the page hands it the look).</summary>
    private async void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EggSequencerViewModel.Look) && ViewModel.Look is { } look)
            await EggPreview.ShowCreatureAsync(look.Rig, look.Parts, seed: look.Seed, colourSeed: look.ColourSeed);
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
