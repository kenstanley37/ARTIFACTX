using ArtifactX.Maui.Services;
using ArtifactX.Maui.Views.AppPages;

namespace ArtifactX.Maui;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        ThemeService.ApplySaved();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new LoadingPage())
        {
            Title = "ArtifactX",
        };
    }
}
