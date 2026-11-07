using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class RawFieldsPanel : UserControl
{
    public static readonly DependencyProperty RowsProperty =
        DependencyProperty.Register(nameof(Rows), typeof(ObservableCollection<CompanionRawFieldViewModel>), typeof(RawFieldsPanel), new PropertyMetadata(null));

    public RawFieldsPanel() => InitializeComponent();

    public ObservableCollection<CompanionRawFieldViewModel>? Rows
    {
        get => (ObservableCollection<CompanionRawFieldViewModel>?)GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    private static CompanionRawFieldViewModel? RowOf(object sender) =>
        (sender as FrameworkElement)?.DataContext as CompanionRawFieldViewModel;

    private void Text_LostFocus(object sender, RoutedEventArgs e) => RowOf(sender)?.CommitText();

    private void Flag_Click(object sender, RoutedEventArgs e) => RowOf(sender)?.CommitFlag();

    private void Generate_Click(object sender, RoutedEventArgs e) => RowOf(sender)?.Generate();
}
