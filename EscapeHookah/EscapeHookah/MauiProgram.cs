using EscapeHookah.Services;
using EscapeHookah.Shared.Services;
using Microsoft.Extensions.Logging;

namespace EscapeHookah;

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
            });

        // Register services
        builder.Services.AddSingleton<IFormFactor, FormFactor>();
        builder.Services.AddSingleton<IFirebaseAuthService, FirebaseAuthService>();
        builder.Services.AddSingleton<IReservationService, ReservationService>();
        // Register IMenuService used by shared pages
        builder.Services.AddSingleton<EscapeHookah.Shared.Services.IMenuService, EscapeHookah.Shared.Services.MenuService>();
        builder.Services.AddScoped(sp => new HttpClient());

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        // Global exception handlers to surface unhandled errors from MAUI/BlazorWebView host
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            try { System.Diagnostics.Debug.WriteLine($"Global UnhandledException: {e.ExceptionObject}"); } catch { }
        };

        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            try { System.Diagnostics.Debug.WriteLine($"Global UnobservedTaskException: {e.Exception}"); e.SetObserved(); } catch { }
        };

        return app;
    }
}
