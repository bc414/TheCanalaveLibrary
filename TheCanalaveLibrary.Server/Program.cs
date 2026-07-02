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

// Register the dedicated read-only DbContext for high-performance queries — via a SCOPED factory,
// not AddDbContext. Blazor Server interleaves sibling components' async init on one circuit scope,
// so a circuit-scoped read context instance is hit concurrently the moment two components (layout
// chrome + page dispatcher, or one page's Task.WhenAll loads) query at once → EF throws
// "A second operation was started on this context instance." Read services therefore create a
// short-lived context per method from IDbContextFactory<ReadOnlyApplicationDbContext>.
// ServiceLifetime.Scoped (not the default Singleton) is required so factory-created contexts can
// resolve the Scoped IActiveUserContext ctor dependency for the named query filters — the same
// scoped-deps constraint that ruled out pooling above. AddDbContextFactory also registers the
// context type itself as a scoped service, so non-circuit consumers (e.g.
// ApplicationUserClaimsPrincipalFactory, request-scoped) may still inject it directly.
// See layer2-services.md "Read-context concurrency: factory per method".
builder.Services.AddDbContextFactory<ReadOnlyApplicationDbContext>(options => options
    .UseNpgsql(canalaveConnectionString, npgsql => npgsql.EnableRetryOnFailure())
    .UseSnakeCaseNamingConvention()
    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking), // Good practice for a read-only context
    ServiceLifetime.Scoped);

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
builder.Services.AddScoped<IDiscoveryDefaultsReadService, ServerDiscoveryDefaultsReadService>();
// Singleton: OptimisticSpriteReadService is stateless — pure string builder, no host/disk deps.
// SpriteBaseUrl defaults to /sprites/themes (dev wwwroot); override in production for R2/CDN.
var spriteBaseUrl = builder.Configuration["Sprites:BaseUrl"] ?? "/sprites/themes";
builder.Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService(spriteBaseUrl));
builder.Services.AddScoped<ITagReadService, ServerTagReadService>();
builder.Services.AddScoped<ITagWriteService, ServerTagWriteService>();
// Server-only write-time probe — checks File.Exists at mod-write time (never at render time).
// Post-MVP: replace with R2SpriteAssetProbe behind this same interface. See audit/Sprites.md L2.
builder.Services.AddSingleton<ISpriteAssetProbe, LocalSpriteAssetProbe>();
// MVP impl writes under wwwroot/uploads/ — settled WU12. Post-MVP swap target: S3ImageStorageService
// (MinIO dev / R2 prod), behind this same interface — see workplan.md Post-MVP section.
builder.Services.AddScoped<IImageStorageService, LocalImageStorageService>();
// Configuration happens once at construction and Sanitize() is thread-safe thereafter, so this is a
// singleton rather than scoped (see ServerHtmlSanitizationService). First call site: WU17 chapter
// write service. Future: WU19 comments, WU29 recommendations, WU31 blog posts, WU35 messaging.
builder.Services.AddSingleton<IHtmlSanitizationService, ServerHtmlSanitizationService>();
// Chapters (WU17/WU26) — L2 read/write services + reading-progress tracker.
builder.Services.AddScoped<IChapterReadService, ServerChapterReadService>();
builder.Services.AddScoped<IChapterWriteService, ServerChapterWriteService>();
builder.Services.AddScoped<IReadingProgressWriteService, ServerReadingProgressWriteService>();
// Comments (WU19) — L2 read/write services (Features 23/24/25/26, chapter context only for MVP).
builder.Services.AddScoped<ICommentReadService, ServerCommentReadService>();
builder.Services.AddScoped<ICommentWriteService, ServerCommentWriteService>();
// Recommendations (WU29) — L2 read/write services (Features 27/28/29/30).
builder.Services.AddScoped<IRecommendationReadService, ServerRecommendationReadService>();
builder.Services.AddScoped<IRecommendationWriteService, ServerRecommendationWriteService>();
// Following/Vouches (WU21) — L2 read/write services.
builder.Services.AddScoped<IFollowingReadService, ServerFollowingReadService>();
builder.Services.AddScoped<IFollowingWriteService, ServerFollowingWriteService>();
// UserStoryInteractions (WU15) — L2 read/write services.
builder.Services.AddScoped<IUserStoryInteractionReadService, ServerUserStoryInteractionReadService>();
builder.Services.AddScoped<IUserStoryInteractionWriteService, ServerUserStoryInteractionWriteService>();
// Blog Posts (WU31/WU32) — L2 read/write services (Features 35/36). Profile blog posts shipped
// WU31; GroupBlogPost create/view added WU32. Feature 56 (feature contributions) deferred post-MVP.
builder.Services.AddScoped<IBlogPostReadService, ServerBlogPostWriteService>();
builder.Services.AddScoped<IBlogPostWriteService, ServerBlogPostWriteService>();
// Groups (WU32) — L2 read/write services (Features 38/39/40).
builder.Services.AddScoped<IGroupReadService, ServerGroupWriteService>();
builder.Services.AddScoped<IGroupWriteService, ServerGroupWriteService>();
// Notifications (WU22) — L2 read/write services (Features 41/42/43).
// WU22 delivers: service infra + NotifyNewFollowerAsync/NotifyNewVouchAsync + Following seam wiring.
// Fan-out notify methods land incrementally with their triggering work-units (workplan.md WU22).
builder.Services.AddScoped<INotificationReadService, ServerNotificationWriteService>();
builder.Services.AddScoped<INotificationWriteService, ServerNotificationWriteService>();
// Messaging (WU35) — L2 read/write services (Feature 49). Stateless MVP; SignalR post-MVP.
// No Notification rows for PMs — messaging has its own LastReadTimestamp watermark bookkeeping.
// See cross-cutting.md "Private Messaging Architecture" and layer2-services.md "AllowPrivateMessages Gate".
builder.Services.AddScoped<IMessagingReadService, ServerMessagingReadService>();
builder.Services.AddScoped<IMessagingWriteService, ServerMessagingWriteService>();
// Moderation (WU34) — Features 46/47/48. Mod pages are server-rendered, no dispatcher/WASM.
// Write service inherits read (CQRS-lite). Forwarding delegate ensures one instance per scope
// when either interface is injected.
builder.Services.AddScoped<IModerationWriteService, ServerModerationWriteService>();
builder.Services.AddScoped<IModerationReadService>(sp => sp.GetRequiredService<IModerationWriteService>());
// Profiles + Theme Selection (WU30) — L2 services (Features 20/21/22/3).
// IUserSettingsService: self-edit exception (spec §3.5) — no userId param; resolves from IActiveUserContext.
// IUserProfileReadService: public display (includePrivate bool, not a source switch).
// IThemeReadService: Sprites cluster (Feature 3 owns Theme entity).
builder.Services.AddScoped<IUserSettingsService, ServerUserSettingsService>();
builder.Services.AddScoped<IUserProfileReadService, ServerUserProfileReadService>();
builder.Services.AddScoped<IThemeReadService, ServerThemeReadService>();
// Badges (WU36) — L2 services (Feature 50). Synchronous inline award-checks; curation UI.
// Write service inherits read (CQRS-lite). Forwarding delegate ensures one instance per scope
// when either interface is injected.
builder.Services.AddScoped<IBadgeWriteService, ServerBadgeWriteService>();
builder.Services.AddScoped<IBadgeReadService>(sp => sp.GetRequiredService<IBadgeWriteService>());

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