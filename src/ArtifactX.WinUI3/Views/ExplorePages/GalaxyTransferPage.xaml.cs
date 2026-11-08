using ArtifactX.WinUI3.ViewModels.ExploreViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ArtifactX.WinUI3.Views.ExplorePages;

public sealed partial class GalaxyTransferPage : Page
{
    public GalaxyTransferViewModel ViewModel { get; } = new();

    public GalaxyTransferPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ = ViewModel.LoadAsync();
    }

    private void FilterAll_Click(object sender, RoutedEventArgs e) => ViewModel.SetFilter("All");
    private void FilterLush_Click(object sender, RoutedEventArgs e) => ViewModel.SetFilter("Lush");
    private void FilterHarsh_Click(object sender, RoutedEventArgs e) => ViewModel.SetFilter("Harsh");
    private void FilterEmpty_Click(object sender, RoutedEventArgs e) => ViewModel.SetFilter("Empty");
    private void FilterNormal_Click(object sender, RoutedEventArgs e) => ViewModel.SetFilter("Normal");

    private async void Transfer_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedGalaxy is not { } target) return;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"Confirm Transfer to {target.Name} (Galaxy {target.Number})",
            Content = new TextBlock
            {
                Text = $"Are you sure you want to stage a transfer to {target.Name}?\n\n" +
                       $"• Destination: Gateway Space Station (~5,571 LY from the Galactic Core)\n" +
                       $"• Spawn: Inside the station docking bay walkway next to your starship\n" +
                       $"• Network: Station will be registered in your Teleport Network\n\n" +
                       $"Edits are staged in memory. Click Save at the top of the window to write changes when No Man's Sky is closed.",
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20
            },
            PrimaryButtonText = $"Transfer to {target.Name}",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            ViewModel.StageTransfer();
        }
    }

    private void Revert_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.RevertTransfer();
    }

    private void CopyGlyphs_Click(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(ViewModel.GatewayPortalGlyphs);
        Clipboard.SetContent(package);
    }
}
