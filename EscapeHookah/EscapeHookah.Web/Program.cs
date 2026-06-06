using EscapeHookah.Shared.Services;
using EscapeHookah.Web.Components;
using EscapeHookah.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add device-specific services used by the EscapeHookah.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();

// Menu service available without authentication
builder.Services.AddSingleton<EscapeHookah.Shared.Services.IMenuService, EscapeHookah.Shared.Services.MenuService>();

// Configure Firebase FCM service if path provided via env
var fcmServiceAccount = builder.Configuration["Fcm:ServiceAccountPath"] ?? Environment.GetEnvironmentVariable("FCM_SERVICE_ACCOUNT_PATH");
if (!string.IsNullOrEmpty(fcmServiceAccount))
{
    builder.Services.AddHttpClient();
    builder.Services.AddSingleton<FcmService>(sp => new FcmService(sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FcmService>>(), sp.GetRequiredService<IHttpClientFactory>(), builder.Configuration));
    builder.Services.AddHostedService<ReservationNotifierHostedService>();
}
else
{
    // When no service account provided, register a null FcmService to avoid DI failures
    builder.Services.AddSingleton<FcmService>(sp => null!);
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(EscapeHookah.Shared._Imports).Assembly);

app.Run();
