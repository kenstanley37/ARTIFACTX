using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Views;

/// <summary>Read-only Reference page: what each item does in the Egg Sequencer
/// (EggSequencerViewModel, rules in ArtifactX.Core EggSequencer).</summary>
public sealed partial class EggSequencerPage : Page
{
    public EggSequencerViewModel ViewModel { get; } = new();

    public EggSequencerPage()
    {
        InitializeComponent();
        _ = ViewModel.LoadAsync();
    }
}
