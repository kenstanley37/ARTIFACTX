using Microsoft.Extensions.Logging;

namespace ArtifactX.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
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
        builder.Services.AddTransient<ViewModels.PortalScannerViewModel>();
        builder.Services.AddTransient<Views.ExplorePages.PortalGlyphsPage>();
        return builder.Build();
    }
}
