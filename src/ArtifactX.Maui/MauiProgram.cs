using Camera.MAUI;
using Microsoft.Extensions.Logging;

namespace ArtifactX.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCameraView()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Exo2-Variable.ttf", "Exo2");
                fonts.AddFont("TitilliumWeb-Regular.ttf", "TitilliumWeb");
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton<Services.IPortalPhotoScanService, Services.PortalPhotoScanService>();
        builder.Services.AddSingleton<Services.ISystemNavigationService, Services.SystemNavigationService>();
#if ANDROID
        builder.Services.AddSingleton<Services.IGalaxyTextReader, Platforms.Android.GalaxyTextReader>();
#elif IOS || MACCATALYST
        builder.Services.AddSingleton<Services.IGalaxyTextReader, Services.AppleGalaxyTextReader>();
#elif WINDOWS
        builder.Services.AddSingleton<Services.IGalaxyTextReader, Platforms.Windows.GalaxyTextReader>();
#endif
        builder.Services.AddTransient<Services.IPortalFrameAnalyzer, Services.PortalFrameAnalyzer>();
        builder.Services.AddTransient<ViewModels.LivePortalScannerViewModel>();
        builder.Services.AddTransient<Views.ExplorePages.LivePortalScannerPage>();
        builder.Services.AddTransient<ViewModels.PortalScannerViewModel>();
        builder.Services.AddTransient<Views.ExplorePages.PortalGlyphsPage>();
        builder.Services.AddTransient<ViewModels.SystemFromGlyphsViewModel>();
        builder.Services.AddTransient<Views.ExplorePages.SystemDetailsPage>();
        return builder.Build();
    }
}
