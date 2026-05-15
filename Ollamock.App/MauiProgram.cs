using Ollamock.App.Services;
using Ollamock.App.ViewModels;

namespace Ollamock.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<BridgeService>(s => new BridgeService("http://localhost:11434"));
        builder.Services.AddSingleton<DashboardViewModel>();

        return builder.Build();
    }
}
