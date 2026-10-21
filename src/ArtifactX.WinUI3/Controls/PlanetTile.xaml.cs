using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class PlanetTile : UserControl
{
    public static readonly DependencyProperty TileProperty =
        DependencyProperty.Register(nameof(Tile), typeof(PlanetTileViewModel), typeof(PlanetTile), new PropertyMetadata(null));

    public PlanetTileViewModel? Tile
    {
        get => (PlanetTileViewModel?)GetValue(TileProperty);
        set => SetValue(TileProperty, value);
    }

    /// <summary>The card (or its "Open ›") was clicked.</summary>
    public event EventHandler<PlanetTileViewModel>? OpenRequested;

    public PlanetTile() => InitializeComponent();

    public Brush CardBorder(bool here) => ThemeResources.Brush(here ? "ThemeAccentBrush" : "ThemeLineBrush");

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (Tile is { } tile) OpenRequested?.Invoke(this, tile);
    }

    private void Card_Tapped(object sender, TappedRoutedEventArgs e)
    {
        for (var element = e.OriginalSource as DependencyObject; element is not null && element != this; element = VisualTreeHelper.GetParent(element))
            if (element is ButtonBase or TextBlock { IsTextSelectionEnabled: true })
                return;
        if (Tile is { } tile) OpenRequested?.Invoke(this, tile);
    }
}
