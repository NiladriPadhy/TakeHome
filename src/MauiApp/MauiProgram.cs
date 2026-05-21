using Microsoft.Extensions.Logging;
using Presentation.Services;
using Presentation.ViewModels;

namespace MauiApp;

public static class MauiProgram
{
    public static Microsoft.Maui.Hosting.MauiApp CreateMauiApp()
    {
        var builder = Microsoft.Maui.Hosting.MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddHttpClient<MeasurementService>(client =>
        {
            client.BaseAddress = new Uri("http://localhost:5100");
            client.DefaultRequestHeaders.Add("x-api-key", "local-dev");
        });

        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<MainPage>();

        return builder.Build();
    }
}
