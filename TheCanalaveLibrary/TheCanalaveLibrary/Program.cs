using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Components;
using TheCanalaveLibrary.Components.Account;
using TheCanalaveLibrary.Data;
using TheCanalaveLibrary.Core.Models;
using TheCanalaveLibrary.Core.ServiceInterfaces;
using TheCanalaveLibrary.Core.Story;
using TheCanalaveLibrary.Endpoints;
using TheCanalaveLibrary.Services; // Make sure this is present

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddRedisDistributedCache("cache");

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

// --- Database Contexts ---
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
        .UseSnakeCaseNamingConvention()
);

// Register the dedicated read-only DbContext for high-performance queries
builder.Services.AddDbContext<ReadOnlyApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
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

// --- END: Corrected Identity Configuration ---

builder.Services.AddSingleton<IEmailSender<User>, IdentityNoOpEmailSender>();

// Add your custom development data seeder
builder.Services.AddScoped<DataSeeder>();

// Services for dependency injection for the server
builder.Services.AddScoped<IDeviceDetectionService, ServerDeviceDetectionService>();
builder.Services.AddScoped<IStoryReadService, DbStoryReadService>();
builder.Services.AddScoped<IStoryWriteService, DbStoryWriteService>();
builder.Services.AddScoped<ISpriteService, FileSystemSpriteService>();

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
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

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