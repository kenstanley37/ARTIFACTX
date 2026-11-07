using ArtifactX.WinUI3.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ArtifactX.WinUI3.Controls;

public sealed partial class ItemPictureView : UserControl
{
    public ItemPictureView() => InitializeComponent();

    /// <summary>Shows <paramref name="gameId"/>'s picture as large as the game has it.</summary>
    public static async Task ShowAsync(XamlRoot root, string gameId, string name)
    {
        var view = new ItemPictureView();
        var dialog = new ContentDialog
        {
            Title = name,
            Content = view,
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = root,
        };
        _ = view.LoadAsync(gameId, name);
        await dialog.ShowAsync();
    }

    private async Task LoadAsync(string gameId, string name)
    {
        AutomationProperties.SetName(Picture, $"Picture of {name}");
        Picture.Source = CatalogService.TryGet(gameId)?.Icon;
        StatusTxt.Text = "Reading the full-size picture from your game files...";
        byte[]? png = await FullSizeIconService.LoadPngAsync(gameId);
        if (png is null)
        {
            StatusTxt.Text = Picture.Source is null
                ? "This item has no picture in your game files."
                : "Only the small picture is available - the full-size one wasn't found in your game files.";
            return;
        }
        var bitmap = new BitmapImage();
        using var stream = new MemoryStream(png);
        await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
        Picture.Source = bitmap;
        StatusTxt.Text = $"{bitmap.PixelWidth} x {bitmap.PixelHeight} pixels, as the game has it.";
    }
}
