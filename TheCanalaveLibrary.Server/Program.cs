using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Server;
using TheCanalaveLibrary.Core;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
// Redis (write-behind cache, Layer 7) is post-MVP — no feature reads/writes IDistributedCache yet.
// Re-add builder.AddRedisDistributedCache("cache") when the first Redis-backed feature is built.

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization();

// Add HttpContextAccessor to access the HttpContext from services.
builder.Services.AddHttpContextAccessor();

// Add services for Razor Pages, which are required for the _Host.cshtml fallback.
builder.Services.AddRazorPages();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddScoped<UserDeletionService>();

// Scoped "who is the current viewer" companion to User — settled WU12. Must be registered before the
// DbContexts below resolve it as a constructor dependency for the content-rating query filter.
builder.Services.AddScoped<IActiveUserContext, ServerActiveUserContext>();

// --- Database Contexts ---
// Plain AddDbContext, never the Aspire Npgsql package's AddNpgsqlDbContext — settled WU12
// (forward_plan.md "Aspire orchestration during MVP dev" narrower correction; layer2-services.md
// "DbContext Registration"). AddNpgsqlDbContext always registers via EF Core's DbContextPool with no
// opt-out, and pooled contexts are built from the root provider — incompatible with ApplicationDbContext
// taking the Scoped IActiveUserContext above as a constructor dependency for the content-rating filter.
string canalaveConnectionString = builder.Configuration.GetConnectionString("canalavedb")!;

builder.Services.AddDbContext<ApplicationDbContext>(options => options
    .UseNpgsql(canalaveConnectionString, npgsql => npgsql.EnableRetryOnFailure())
    .UseSnakeCaseNamingConvention());

// Register the dedicated read-only DbContext for high-performance queries
builder.Services.AddDbContext<ReadOnlyApplicationDbContext>(options => options
    .UseNpgsql(canalaveConnectionString, npgsql => npgsql.EnableRetryOnFailure())
    .UseSnakeCaseNamingConvention()
    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)); // Good practice for a read-only context

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// --- START: Corrected Identity Configuration ---

// 1. Add base authentication services and the cookie scheme
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies(); // This is the correct way to add the cookie handler

// 2. Configure ApplicationCookie to handle 401/403 responses for Blazor
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = 401;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = 403;
        return Task.CompletedTask;
    };
});

// 3. Add the core Identity services, specifying your User type
builder.Services.AddIdentityCore<User>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        
        // Add sensible password requirements
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;
    })
    .AddRoles<ApplicationRole>() // Add the Role service
    .AddEntityFrameworkStores<ApplicationDbContext>() // Connect to your DbContext
    .AddApiEndpoints() // Add the new .NET 8 Identity API endpoints
    .AddDefaultTokenProviders(); // Add token providers for email, 2FA, etc.

// Override the default IUserClaimsPrincipalFactory<User> (registered above via AddRoles, TryAddScoped)
// with one that also bakes ShowMatureContent/Theme/PrefersAnimatedSprites into the auth cookie's claims
// at sign-in — settled WU12, see ApplicationUserClaimsPrincipalFactory. Must come AFTER the Identity
// chain above: the last registration for a given service type wins on single-instance resolution.
builder.Services.AddScoped<IUserClaimsPrincipalFactory<User>, ApplicationUserClaimsPrincipalFactory>();

// --- END: Corrected Identity Configuration ---

builder.Services.AddSingleton<IEmailSender<User>, IdentityNoOpEmailSender>();

// Add your custom development data seeder
builder.Services.AddScoped<DataSeeder>();

// Services for dependency injection for the server
builder.Services.AddScoped<IDeviceDetectionService, ServerDeviceDetectionService>();
builder.Services.AddScoped<IStoryReadService, ServerStoryReadService>();
builder.Services.AddScoped<IStoryWriteService, ServerStoryWriteService>();
builder.Services.AddScoped<ISpriteReadService, ServerSpriteReadService>();
builder.Services.AddScoped<ITagReadService, ServerTagReadService>();
// MVP impl writes under wwwroot/uploads/ — settled WU12. Post-MVP swap target: S3ImageStorageService
// (MinIO dev / R2 prod), behind this same interface — see workplan.md Post-MVP section.
builder.Services.AddScoped<IImageStorageService, LocalImageStorageService>();
// Configuration happens once at construction and Sanitize() is thread-safe thereafter, so this is a
// singleton rather than scoped (see ServerHtmlSanitizationService). No call site yet — chapter/comment/
// etc. write services inject it when they land (WU17, WU19, ...).
builder.Services.AddSingleton<IHtmlSanitizationService, ServerHtmlSanitizationService>();

WebApplication app = builder.Build();

// --- START: Seeding Logic (from our discussion) ---

// Run development seeder
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
        try
        {
            await seeder.SeedDevelopmentDataAsync();
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred during development data seeding.");
        }
    }
}
/*
// Run production admin seeder (safe to run in all environments)
try
{
    await ProductionSeeder.SeedProductionAdminAsync(app.Services);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogCritical(ex, "Failed to seed production admin account.");
}

// --- END: Seeding Logic ---
*/

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseMigrationsEndPoint();
    app.MapDevDiagnosticsEndpoints();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.MapDefaultEndpoints();

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// This must come before MapRazorComponents
app.UseStaticFiles(); 
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(
        typeof(TheCanalaveLibrary.Client.WasmClientAssemblyIdentifier).Assembly,
        typeof(TheCanalaveLibrary.SharedUI.SharedUIAssemblyIdentifier).Assembly
    );

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.MapStoryEndpoints();

app.Run();

// Top-level statements generate an internal `Program` class by default — making it `public partial`
// is the standard ASP.NET Core pattern that lets `WebApplicationFactory<Program>` (a different
// assembly, TheCanalaveLibrary.Tests.Integration) reference this type. See testing.md.
public partial class Program;