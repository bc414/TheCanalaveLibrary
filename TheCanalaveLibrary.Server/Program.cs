using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Server;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
// Redis (write-behind cache, Layer 7) is post-MVP — no feature reads/writes IDistributedCache yet.
// Re-add builder.AddRedisDistributedCache("cache") when the first Redis-backed feature is built.

// Add services to the container.
builder.Services.AddRazorComponents()
    // DetailedErrors is Development-only by contract (logging.md §"Unhandled exceptions"):
    // prod clients get generic circuit errors; the server log carries the detail.
    .AddInteractiveServerComponents(options => options.DetailedErrors = builder.Environment.IsDevelopment())
    .AddInteractiveWebAssemblyComponents()
    // SerializeAllClaims: the default set is name + role claims only. WASM components also need
    // the custom claims baked in by ApplicationUserClaimsPrincipalFactory (Theme,
    // PrefersAnimatedSprites → ThemeContextProvider sprite resolution client-side).
    .AddAuthenticationStateSerialization(options => options.SerializeAllClaims = true);

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

// Telemetry correlation at the circuit dispatch boundary (WU-Observability): logger scope
// (CircuitId/UserId) + canalave.user.id trace tag around every inbound circuit activity.
// See canalave-conventions/logging.md §"Context Scopes".
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler, TelemetryCircuitHandler>();

// Error-handling UX seams (WU-ErrorHandling — cross-cutting.md §"Error Handling Strategy"):
// the transient toast channel (ToastHost in DeviceLayout is the single subscriber) and the
// localStorage draft store behind DraftAutosave. Both scoped per circuit; both also registered
// in the Client host so the components survive the L5 WASM flip unchanged.
builder.Services.AddScoped<IToastService, ToastService>();
builder.Services.AddScoped<DraftStore>();

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

// Data Protection keyring → Postgres (WU-DataProtection): auth cookies + antiforgery tokens
// survive process replacement (the droplet-redeploy logout footgun). SetApplicationName is
// mandatory — without it key isolation derives from the content-root path. Deliberately no
// ProtectKeysWith* (keys unencrypted in our own DB — accepted small-deployment trade-off).
// See security.md "Data Protection Keyring".
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>()
    .SetApplicationName("TheCanalaveLibrary");

// --- START: Corrected Identity Configuration ---

// 1. Add base authentication services and the cookie scheme
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies(); // This is the correct way to add the cookie handler

// 2. Configure ApplicationCookie to handle 401/403 responses for Blazor
builder.Services.ConfigureApplicationCookie(options =>
{
    // Explicit cookie hardening (WU-Security): these match today's framework defaults but are
    // set deliberately so the posture is self-documenting and survives default drift
    // (security.md "Identity Hardening"). SameSite stays Lax — no cross-site POST flows exist.
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;

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

        // Per-ACCOUNT brute-force defense (WU-Security) — complements the per-IP rate limit on
        // the /Account/* form posts below; the two defend different axes (security.md
        // "Identity Hardening"). Login.razor passes lockoutOnFailure: true.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<ApplicationRole>() // Add the Role service
    .AddEntityFrameworkStores<ApplicationDbContext>() // Connect to your DbContext
    .AddApiEndpoints() // Add the new .NET 8 Identity API endpoints
    .AddDefaultTokenProviders() // Add token providers for email, 2FA, etc.
    // Overrides AddApiEndpoints's SignInManager<User> registration (last registration wins) so
    // CanSignInAsync enforces AccountStatus (Suspended/Banned) at the one choke point every
    // sign-in path (password/passkey/2FA/external) shares — WU38a, security.md "Account-Status
    // Enforcement".
    .AddSignInManager<CanalaveSignInManager>();

// Override the default IUserClaimsPrincipalFactory<User> (registered above via AddRoles, TryAddScoped)
// with one that also bakes ShowMatureContent/Theme/PrefersAnimatedSprites into the auth cookie's claims
// at sign-in — settled WU12, see ApplicationUserClaimsPrincipalFactory. Must come AFTER the Identity
// chain above: the last registration for a given service type wins on single-instance resolution.
builder.Services.AddScoped<IUserClaimsPrincipalFactory<User>, ApplicationUserClaimsPrincipalFactory>();

// --- END: Corrected Identity Configuration ---

// Email provider switch (WU-Email): "NoOp" (default — RegisterConfirmation.razor's on-page
// confirmation link, gated on `EmailSender is IdentityNoOpEmailSender`) or "Smtp" (real send via
// MailKit against whatever Email:Smtp points at — Mailpit under the Aspire AppHost's env
// injection, the chosen provider's SMTP endpoint in production). Same shape as the
// ImageStorage:Provider switch above. See cross-cutting.md "Identity & Auth".
string emailProvider = builder.Configuration["Email:Provider"] ?? "NoOp";
if (emailProvider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
    builder.Services.AddSingleton<IEmailSender<User>, SmtpEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender<User>, IdentityNoOpEmailSender>();
}

// HTTP edge rate limiting (WU-Security) — covers the surfaces that are plain HTTP today:
// per-IP window on the /Account/* auth form posts (credential-stuffing damping) and the
// "TagWrites" policy on /api/tags writes. User writes (comments, uploads, …) are throttled at
// the service layer instead — they travel over the SignalR circuit, which this middleware
// never sees. Per-IP partitioning only becomes meaningful in production once Phase 7's
// ForwardedHeaders work lands (behind Cloudflare every request shares a handful of proxy IPs).
// See security.md "HTTP Edge Rate Limiting".
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }
        // The body is load-bearing: UseStatusCodePagesWithReExecute re-executes BODY-LESS error
        // responses into /not-found with the original method (the TagEndpoints.cs trap).
        await context.HttpContext.Response.WriteAsync(
            "Too many requests - please slow down and try again shortly.", cancellationToken);
    };

    // Global limiter: tight per-IP window on auth form posts only; everything else unlimited.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        if (HttpMethods.IsPost(httpContext.Request.Method) &&
            httpContext.Request.Path.StartsWithSegments("/Account"))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                $"auth:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0, // reject immediately — never delay a brute-forcer politely
                });
        }
        return RateLimitPartition.GetNoLimiter("unlimited");
    });

    options.AddPolicy("TagWrites", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"tagwrites:{httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

// Add your custom development data seeder
builder.Services.AddScoped<DataSeeder>();

// Services for dependency injection for the server
builder.Services.AddScoped<IDeviceDetectionService, ServerDeviceDetectionService>();
builder.Services.AddScoped<IStoryReadService, ServerStoryReadService>();
builder.Services.AddScoped<IStoryWriteService, ServerStoryWriteService>();
// Story export (Feature 54, WU38c) — composes the read services; "export = what you can read".
// QuestPDF's Community license is set in PdfWriter's static ctor (covers app + test paths).
builder.Services.AddScoped<IExportService, ServerExportService>();
// Chapter import (Feature 63, WU38d) — stateless pipeline over the singleton sanitizer.
builder.Services.AddSingleton<IContentImportService, ServerContentImportService>();
// View-count signal buffer (Feature 45 L2, layer2-services.md "Signal Buffering") — the scoped
// recorder merges pings into the singleton buffer; the hosted worker batch-flushes into
// daily_story_stats. TestAppFactory removes the worker (tests flush via ViewCountFlusher).
builder.Services.AddScoped<IViewCountWriteService, ServerViewCountWriteService>();
builder.Services.AddSingleton<ViewCountBuffer>();
builder.Services.AddSingleton<ViewCountFlusher>();
builder.Services.AddHostedService<ViewCountFlushWorker>();
builder.Services.AddScoped<IDiscoveryDefaultsReadService, ServerDiscoveryDefaultsReadService>();
// Discovery marts (WU-Marts, layer8-data-marts.md): the scoped rebuilder does the raw-SQL
// staging-swap rebuilds; the hosted worker drives them daily (+ bootstrap when empty).
// TestAppFactory removes the worker (tests rebuild deterministically via the rebuilder).
builder.Services.AddScoped<DiscoveryMartRebuilder>();
builder.Services.AddHostedService<DiscoveryMartWorker>();
// SiteDailyStat worker (Feature 62, layer8-data-marts.md): unlike the marts above, this upserts
// one append-only ground-truth row per completed UTC day (no staging-table swap). TestAppFactory
// removes the hosted worker (tests upsert deterministically via SiteDailyStatAggregator).
builder.Services.AddScoped<SiteDailyStatAggregator>();
builder.Services.AddHostedService<SiteDailyStatWorker>();
builder.Services.AddScoped<ISiteDailyStatReadService, ServerSiteDailyStatReadService>();
builder.Services.AddScoped<ICoOccurrenceReadService, ServerCoOccurrenceReadService>();
builder.Services.AddScoped<ITreeSearchReadService, ServerTreeSearchReadService>();
// Singleton: OptimisticSpriteReadService is stateless — pure string builder, no host/disk deps.
// SpriteBaseUrl defaults to /sprites/themes (dev wwwroot); override in production for R2/CDN.
var spriteBaseUrl = builder.Configuration["Sprites:BaseUrl"] ?? "/sprites/themes";
builder.Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService(spriteBaseUrl));
builder.Services.AddScoped<ITagReadService, ServerTagReadService>();
builder.Services.AddScoped<ITagWriteService, ServerTagWriteService>();
// WU43 — write class serves both interfaces (mirrors ISeriesReadService/Write registration below).
builder.Services.AddScoped<ISavedTagSelectionReadService, ServerSavedTagSelectionWriteService>();
builder.Services.AddScoped<ISavedTagSelectionWriteService, ServerSavedTagSelectionWriteService>();
// Server-only write-time probe — checks File.Exists at mod-write time (never at render time).
// Post-MVP: replace with R2SpriteAssetProbe behind this same interface. See audit/Sprites.md L2.
builder.Services.AddSingleton<ISpriteAssetProbe, LocalSpriteAssetProbe>();
// Public URL resolution for Open Graph/Twitter meta tags (Seo/, WU-Seo) — singleton, stateless,
// same pattern as OptimisticSpriteReadService above. Site:PublicBaseUrl is the canonical origin
// (never NavigationManager.BaseUri server-side — see audit/Seo.md for why: this app sits behind
// Cloudflare in front of DigitalOcean droplets, and a request-derived base risks leaking an
// internal proxy/droplet host into a crawler-visible og:image). ImageStorage:PublicBaseUrl
// defaults to the site base — today every image is same-origin through the app regardless of
// storage provider (see ImageEndpoints.cs); this setting is the wired-but-unset seam for a future
// direct-R2/CDN image-serving migration. Set both in production per the Phase-7 config contract.
var sitePublicBaseUrl = builder.Configuration["Site:PublicBaseUrl"] ?? "https://localhost:7248";
var imagePublicBaseUrl = builder.Configuration["ImageStorage:PublicBaseUrl"];
builder.Services.AddSingleton<IPublicUrlProvider>(
    new PublicUrlProvider(sitePublicBaseUrl, imagePublicBaseUrl));
// Image storage provider switch (WU-S3Garage): "Local" (default — wwwroot/uploads, static
// files) or "S3" (Garage in dev via the Aspire AppHost's env injection, Cloudflare R2 in prod)
// behind the same frozen IImageStorageService. Read eagerly like the connection string above —
// fine in production/Aspire where env vars exist at boot; the integration-test host always gets
// Local (see TestAppFactory's config-eagerness note). S3 mode also maps the /uploads serving
// route below. Wire-format constraints that keep Garage and R2 interchangeable live in
// S3ImageStorageService.CreateClient.
// Per-user write throttle (WU-Security) — the transport-agnostic enforcement point L2 write
// services and ImageUploadProcessor call. Singleton: one token-bucket table for the process,
// same lifetime reasoning as ServerHtmlSanitizationService. See security.md "Write Throttling".
builder.Services.AddSingleton<IWriteRateLimitService, ServerWriteRateLimitService>();
// Shared upload hardening step (sniff + re-encode) both storage impls consume — scoped because
// it reads the scoped IActiveUserContext for the per-user upload throttle.
builder.Services.AddScoped<ImageUploadProcessor>();
string imageStorageProvider = builder.Configuration["ImageStorage:Provider"] ?? "Local";
bool useS3ImageStorage = imageStorageProvider.Equals("S3", StringComparison.OrdinalIgnoreCase);
if (useS3ImageStorage)
{
    builder.Services.Configure<S3ImageStorageOptions>(
        builder.Configuration.GetSection(S3ImageStorageOptions.SectionName));
    // One client for the process — AmazonS3Client is thread-safe and holds the connection pool.
    builder.Services.AddSingleton<Amazon.S3.IAmazonS3>(sp => S3ImageStorageService.CreateClient(
        sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<S3ImageStorageOptions>>().Value));
    builder.Services.AddScoped<IImageStorageService, S3ImageStorageService>();
}
else
{
    builder.Services.AddScoped<IImageStorageService, LocalImageStorageService>();
}
// Configuration happens once at construction and Sanitize() is thread-safe thereafter, so this is a
// singleton rather than scoped (see ServerHtmlSanitizationService). First call site: WU17 chapter
// write service. Future: WU19 comments, WU29 recommendations, WU31 blog posts, WU35 messaging.
builder.Services.AddSingleton<IHtmlSanitizationService, ServerHtmlSanitizationService>();
// Chapters (WU17/WU26) — L2 read/write services + reading-progress tracker.
builder.Services.AddScoped<IChapterReadService, ServerChapterReadService>();
builder.Services.AddScoped<IChapterWriteService, ServerChapterWriteService>();
builder.Services.AddScoped<IReadingProgressWriteService, ServerReadingProgressWriteService>();
// Reading-progress signal buffer (Feature 44 L2, layer2-services.md "Signal Buffering") — the
// scoped writer above merges pings into this singleton buffer; the hosted worker batch-flushes.
// TestAppFactory removes the worker so integration tests flush deterministically via the flusher.
builder.Services.AddSingleton<ReadingProgressBuffer>();
builder.Services.AddSingleton<ReadingProgressFlusher>();
builder.Services.AddHostedService<ReadingProgressFlushWorker>();
// User-activity signal buffer (WU-SiteDailyStat, Feature 62 L2, layer2-services.md "Signal
// Buffering") — authenticated-only pings feed User.LastActiveUtc, sourcing Feature 62's
// active_users + the profile "last seen" display. TestAppFactory removes the worker (tests flush
// deterministically via UserActivityFlusher).
builder.Services.AddScoped<IUserActivityWriteService, ServerUserActivityWriteService>();
builder.Services.AddSingleton<UserActivityBuffer>();
builder.Services.AddSingleton<UserActivityFlusher>();
builder.Services.AddHostedService<UserActivityFlushWorker>();
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
// Series (WU41) — L2 read/write services (Feature 9).
builder.Services.AddScoped<ISeriesReadService, ServerSeriesWriteService>();
builder.Services.AddScoped<ISeriesWriteService, ServerSeriesWriteService>();
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

// Security headers + per-request CSP nonce on every response (WU-Security). CSP is enforced
// outside Development, Report-Only in Development — see security.md "Response Headers & CSP".
app.UseMiddleware<SecurityHeadersMiddleware>();

// This must come before MapRazorComponents
app.UseStaticFiles();
// After UseStaticFiles (assets exempt), before UseAntiforgery — see security.md.
app.UseRateLimiter();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    // Keeps the WebSocket-compression frame-ancestors setting consistent with the
    // SecurityHeadersMiddleware's frame-ancestors 'none' / X-Frame-Options DENY (security.md).
    .AddInteractiveServerRenderMode(options => options.ContentSecurityFrameAncestorsPolicy = "'none'")
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(
        typeof(TheCanalaveLibrary.Client.WasmClientAssemblyIdentifier).Assembly,
        typeof(TheCanalaveLibrary.SharedUI.SharedUIAssemblyIdentifier).Assembly
    );

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.MapStoryEndpoints();
app.MapTagEndpoints();
app.MapExportEndpoints();

// S3 mode only: stored /uploads/… URLs resolve from the bucket instead of wwwroot. Local mode
// must NOT map this — without a configured IAmazonS3 the handler can't resolve, and static
// files already serve those paths.
if (useS3ImageStorage)
{
    app.MapImageServingEndpoints();
}

app.Run();

// Top-level statements generate an internal `Program` class by default — making it `public partial`
// is the standard ASP.NET Core pattern that lets `WebApplicationFactory<Program>` (a different
// assembly, TheCanalaveLibrary.Tests.Integration) reference this type. See testing.md.
public partial class Program;