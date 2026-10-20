using ArtifactX.Core.Models;
using ArtifactX.WinUI3.Services;
using ArtifactX.WinUI3.ViewModels.ExploreViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ArtifactX.WinUI3.Views.ExplorePages;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class PlanetDetailsPage : Page
{
    public PlanetDetailsViewModel ViewModel { get; set; } = new();

    public PlanetDetailsPage()
    {
        InitializeComponent();
        this.DataContext = ViewModel;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        // Handle incoming telemetry or planet object parameters passed from discovery/system lists
        if (e.Parameter is PlanetDetailsViewModel incomingViewModel)
        {
            ViewModel = incomingViewModel;
            this.DataContext = ViewModel;
            this.Bindings.Update();
        }
        else if (e.Parameter is string planetName && !string.IsNullOrEmpty(planetName))
        {
            ViewModel.PlanetName = planetName;
        }
    }

    private async void ExportDataButton_Click(object sender, RoutedEventArgs e)
    {
        // Example interactive handler for exporting planet records
        ContentDialog exportDialog = new ContentDialog
        {
            Title = "Telemetry Exported",
            Content = $"Successfully saved survey data for {ViewModel.PlanetName} to your workspace exports.",
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot,
            RequestedTheme = this.ActualTheme
        };

        await exportDialog.ShowAsync();
    }
}
