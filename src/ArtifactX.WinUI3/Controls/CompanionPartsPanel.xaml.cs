using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class CompanionPartsPanel : UserControl
{
    public static readonly DependencyProperty RowsProperty =
        DependencyProperty.Register(nameof(Rows), typeof(ObservableCollection<CompanionPartRowViewModel>), typeof(CompanionPartsPanel), new PropertyMetadata(null));

    public static readonly DependencyProperty ShowNoDataProperty =
        DependencyProperty.Register(nameof(ShowNoData), typeof(bool), typeof(CompanionPartsPanel), new PropertyMetadata(false));

    public CompanionPartsPanel() => InitializeComponent();

    public ObservableCollection<CompanionPartRowViewModel>? Rows
    {
        get => (ObservableCollection<CompanionPartRowViewModel>?)GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    /// <summary>The species' rig has no catalog data, so nothing can be edited.</summary>
    public bool ShowNoData
    {
        get => (bool)GetValue(ShowNoDataProperty);
        set => SetValue(ShowNoDataProperty, value);
    }
}
