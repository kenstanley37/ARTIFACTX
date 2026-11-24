using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

/// <summary>Shown in a dialog (see CurrentSystemPage's background menu); the picker loads itself.</summary>
public sealed partial class ScreenshotPicker : UserControl
{
    public ScreenshotPickerViewModel Picker { get; }

    public ScreenshotPicker(ScreenshotPickerViewModel picker)
    {
        Picker = picker;
        InitializeComponent();
    }

    private void Shots_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        Picker.Selected = ShotsView.SelectedItem as ScreenshotChoiceViewModel;
}
