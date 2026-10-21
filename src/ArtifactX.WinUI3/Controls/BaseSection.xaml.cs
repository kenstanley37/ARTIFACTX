using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class BaseSection : UserControl
{
    public static readonly DependencyProperty SectionProperty =
        DependencyProperty.Register(nameof(Section), typeof(BaseSectionViewModel), typeof(BaseSection), new PropertyMetadata(null));

    public BaseSectionViewModel? Section
    {
        get => (BaseSectionViewModel?)GetValue(SectionProperty);
        set => SetValue(SectionProperty, value);
    }

    /// <summary>A card's "Open ›" was clicked.</summary>
    public event EventHandler<BaseRowViewModel>? OpenRequested;

    public BaseSection() => InitializeComponent();

    private void Header_Click(object sender, RoutedEventArgs e) => Section?.Toggle();
    private void SelectAll_Click(object sender, RoutedEventArgs e) => Section?.SelectAll();
    private void DeselectAll_Click(object sender, RoutedEventArgs e) => Section?.DeselectAll();
    private void Card_OpenRequested(object? sender, BaseRowViewModel row) => OpenRequested?.Invoke(this, row);
}
