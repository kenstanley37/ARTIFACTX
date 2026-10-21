using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class BaseCard : UserControl
{
    public static readonly DependencyProperty RowProperty =
        DependencyProperty.Register(nameof(Row), typeof(BaseRowViewModel), typeof(BaseCard), new PropertyMetadata(null));

    public BaseRowViewModel? Row
    {
        get => (BaseRowViewModel?)GetValue(RowProperty);
        set => SetValue(RowProperty, value);
    }

    /// <summary>"Open ›" was clicked: show this row's own page.</summary>
    public event EventHandler<BaseRowViewModel>? OpenRequested;

    public BaseCard() => InitializeComponent();

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (Row is { } row) OpenRequested?.Invoke(this, row);
    }
}
