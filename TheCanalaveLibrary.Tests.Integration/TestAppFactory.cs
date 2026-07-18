using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Boots the real <c>TheCanalaveLibrary.Server</c> host (the actual <c>Program.cs</c>, not a hand-built
/// substitute) against a Testcontainers Postgres connection string, with the real claims-based
/// <see cref="ServerActiveUserContext"/> swapped for a settable <see cref="FakeActiveUserContext"/> —
/// see testing.md "Driving the content-rating filter."
///
/// <b>DbContext wiring:</b> <c>Program.cs</c> resolves the connection string eagerly via
/// <c>builder.Configuration.GetConnectionString("canalavedb")</c> before the WebApplicationFactory's
/// <c>ConfigureAppConfiguration</c> callback can override it (a <see cref="WebApplicationBuilder"/>
/// quirk). Both DbContexts are therefore re-registered here via <c>ConfigureServices</c> using the
/// Testcontainers connection string directly. Everything else (Identity, real service registrations)
/// is wired exactly as production.
///
/// <b>One instance, shared collection-wide.</b> This host is built once per run and owned by
/// <see cref="PostgresFixture"/> (<see cref="PostgresFixture.Factory"/>); every test resolves from
/// it. Per-test isolation is <em>not</em> this factory's job: Respawn resets the DB before each
/// test, and <c>IntegrationTestBase.ResetSharedHostState</c> resets the shared host's in-memory
/// state (the mutable <see cref="FakeActiveUserContext"/> singleton back to
/// <see cref="FakeActiveUserContext.Anonymous"/>, plus the signal buffers). See testing.md
/// "Integration test host is shared collection-wide."
/// </summary>
public sealed class TestAppFactory(string connectionString) : WebApplicationFactory<Program>
{
    public string WebRootPath { get; } = Path.Combine(Path.GetTempPath(), "canalave-tests-webroot-" + Guid.NewGuid());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(WebRootPath);

        // "Development" environment so Program.cs's IsDevelopment() branches (dev-diagnostics
        // endpoints, DetailedErrors) behave as in a real local run. The DataSeeder also runs under
        // this environment but is pinned to DevSeed=None below, so it seeds nothing — tests seed
        // their own identities via IntegrationTestBase.SeedUserAsync.
        builder.UseEnvironment(Environments.Development);

        // Redirect uploads to a per-factory temp folder — LocalImageStorageService writes under
        // IWebHostEnvironment.WebRootPath, and tests must never touch the real wwwroot/uploads/.
        builder.UseWebRoot(WebRootPath);

        // Pin the seeder to None: under the Development environment the seeder runs on every factory
        // boot, and (with the factory now shared collection-wide — see PostgresFixture) once per run.
        // Tests seed their own identities via IntegrationTestBase.SeedUserAsync and never depend on
        // TestUser/AdminUser (testing.md forbids it); the ASP.NET role rows tests need as FK targets
        // come from ApplicationRoleConfiguration.HasData (Respawn-ignored), not the seeder, so None
        // is safe. Unlike the connection string (read eagerly by Program.cs — see the DbContext note
        // below), the seeder reads IConfiguration lazily at run time, so this override works.
        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?> { ["DevSeed"] = "None" }));

        builder.ConfigureServices(services =>
        {
            // Collapse Identity's deliberately-slow PBKDF2 to a single iteration. No test in this
            // suite verifies a password — auth is faked via TestAuthenticationHandler +
            // FakeActiveUserContext — so the hash cost is pure waste, paid by every
            // SeedUserAsync (userManager.CreateAsync). The stored hash records its own iteration
            // count, so this only affects cost, never correctness of the create path.
            services.Configure<PasswordHasherOptions>(o => o.IterationCount = 1);

            // Re-register both DbContexts against the Testcontainers database.
            // ConfigureAppConfiguration cannot reliably override the connection string because
            // Program.cs reads it via builder.Configuration.GetConnectionString() before the
            // WebApplicationFactory's callback fires (WebApplicationBuilder quirk). Removing and
            // re-adding the DbContextOptions descriptors is the reliable fix.
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions<ReadOnlyApplicationDbContext>>();

            services.AddDbContext<ApplicationDbContext>(options => options
                .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure())
                .UseSnakeCaseNamingConvention());

            // Mirrors production's scoped-factory registration (Program.cs) — read services create
            // per-method contexts via IDbContextFactory<ReadOnlyApplicationDbContext>; the factory
            // must be Scoped so created contexts resolve the scoped IActiveUserContext (the fake below).
            services.AddDbContextFactory<ReadOnlyApplicationDbContext>(options => options
                .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure())
                .UseSnakeCaseNamingConvention()
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking),
                ServiceLifetime.Scoped);

            // Swap the real claims-based IActiveUserContext for a settable test double.
            services.RemoveAll<IActiveUserContext>();
            services.AddSingleton<FakeActiveUserContext>();
            services.AddScoped<IActiveUserContext>(sp => sp.GetRequiredService<FakeActiveUserContext>());

            // Redirect the default authentication scheme to TestAuthenticationHandler, which
            // authenticates every request as whatever FakeActiveUserContext currently holds — no
            // test in this suite performs a real Identity cookie sign-in, so without this,
            // .RequireAuthorization()-gated endpoints hit via Factory.CreateClient() always 401
            // regardless of SetActiveUser. IdentityConstants.ApplicationScheme stays registered
            // (harmless, unused) — this only repoints which scheme is the default.
            services.AddAuthentication(o =>
            {
                o.DefaultScheme = TestAuthenticationHandler.SchemeName;
                o.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                o.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.SchemeName, _ => { });

            // Swap the real per-user write throttle for a pass-through — tests legitimately
            // hammer write services in loops (paging seeds, at-limit fills). WriteThrottleTests
            // re-registers the real ServerWriteRateLimitService to cover the throttle itself.
            services.RemoveAll<IWriteRateLimitService>();
            services.AddSingleton<IWriteRateLimitService, FakeWriteRateLimitService>();

            // Remove the timer/schedule-driven background workers: the flush workers' 5s cadence
            // would write mid-test racing the Respawn reset, and the mart worker's startup
            // bootstrap-rebuild would run DDL concurrently with tests. Tests act deterministically
            // by resolving the corresponding flusher/rebuilder and calling FlushAsync()/
            // RebuildAllAsync() directly (testing.md — deterministic flush).
            foreach (Type workerType in new[]
                     {
                         typeof(ReadingProgressFlushWorker), typeof(ViewCountFlushWorker),
                         typeof(DiscoveryMartWorker), typeof(UserActivityFlushWorker),
                         typeof(SiteDailyStatWorker), typeof(SpotlightGoLiveWorker),
                         typeof(PollEditNotificationWorker), typeof(NotificationCleanupWorker),
                         typeof(UserStatRecalculationWorker),
                     })
            {
                ServiceDescriptor? backgroundWorker = services.FirstOrDefault(d =>
                    d.ServiceType == typeof(IHostedService) && d.ImplementationType == workerType);
                if (backgroundWorker is not null) services.Remove(backgroundWorker);
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(WebRootPath))
        {
            try
            {
                Directory.Delete(WebRootPath, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup — a stray locked file under the temp webroot isn't worth failing
                // the test run over.
            }
        }
    }
}
