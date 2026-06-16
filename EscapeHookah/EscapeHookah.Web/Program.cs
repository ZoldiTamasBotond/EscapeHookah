using EscapeHookah.Shared.Services;
using EscapeHookah.Web.Components;
using EscapeHookah.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add device-specific services used by the EscapeHookah.Shared project
builder.Services.AddSingleton<IFormFactor, FormFactor>();

// Register HttpClient and Firebase auth/reservation/menu services as scoped so auth session is per user
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<EscapeHookah.Shared.Services.FirebaseAuthService>();
builder.Services.AddScoped<IFirebaseAuthService>(sp => sp.GetRequiredService<EscapeHookah.Shared.Services.FirebaseAuthService>());
builder.Services.AddScoped<EscapeHookah.Shared.Services.IReservationService, EscapeHookah.Shared.Services.ReservationService>();
builder.Services.AddScoped<EscapeHookah.Shared.Services.IMenuService, EscapeHookah.Shared.Services.MenuService>();

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

// Optional startup seed: create initial admin user if requested via config or environment
var seedAdmin = builder.Configuration["SeedAdmin"] ?? Environment.GetEnvironmentVariable("SEED_ADMIN");
if (!string.IsNullOrEmpty(seedAdmin) && seedAdmin.Equals("true", System.StringComparison.OrdinalIgnoreCase))
{
    try
    {
        var auth = app.Services.GetRequiredService<IFirebaseAuthService>();
        // Create admin user with email admin@local and password admin, username 'admin'
        // This call is safe to run even if user already exists (method returns false on failure)
        auth.CreateAdminUser("admin@local", "admin", "Admin", "User", "admin", "").GetAwaiter().GetResult();
    }
    catch (System.Exception)
    {
        // Ignore seed failures
    }
}

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
