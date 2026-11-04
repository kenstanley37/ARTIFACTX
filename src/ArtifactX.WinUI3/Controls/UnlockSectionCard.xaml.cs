using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class UnlockSectionCard : UserControl
{
    public static readonly DependencyProperty SectionProperty =
        DependencyProperty.Register(nameof(Section), typeof(UnlockSectionViewModel), typeof(UnlockSectionCard), new PropertyMetadata(null));

    public UnlockSectionViewModel? Section
    {
        get => (UnlockSectionViewModel?)GetValue(SectionProperty);
        set => SetValue(SectionProperty, value);
    }

    public UnlockSectionCard() => InitializeComponent();

    private void All_Click(object sender, RoutedEventArgs e) => Section?.ToggleAll();

    private void Row_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: AccountItemRowViewModel row } box) Section?.SetRow(row, box.IsChecked == true);
    }
}
