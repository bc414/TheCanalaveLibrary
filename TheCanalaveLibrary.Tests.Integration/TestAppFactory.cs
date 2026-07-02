using Microsoft.AspNetCore.Hosting;
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
/// Each test method gets its own instance (xUnit constructs a fresh test-class instance per <c>[Fact]</c>),
/// so each test gets its own DI container and therefore its own <see cref="FakeActiveUserContext"/> —
/// no cross-test contamination from the mutable fake, even though it's registered as a singleton
/// within any one factory.
/// </summary>
public sealed class TestAppFactory(string connectionString) : WebApplicationFactory<Program>
{
    public string WebRootPath { get; } = Path.Combine(Path.GetTempPath(), "canalave-tests-webroot-" + Guid.NewGuid());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(WebRootPath);

        // "Development" environment so the DataSeeder runs (harmless — tests seed their own
        // identities via IntegrationTestBase.SeedUserAsync; Respawn wipes everything before
        // the next test).
        builder.UseEnvironment(Environments.Development);

        // Redirect uploads to a per-factory temp folder — LocalImageStorageService writes under
        // IWebHostEnvironment.WebRootPath, and tests must never touch the real wwwroot/uploads/.
        builder.UseWebRoot(WebRootPath);

        // Pin the seeder to Minimal (users + roles only): under the Development environment the
        // seeder runs on every factory boot — i.e. before EVERY test (Respawn wipes TestUser, so
        // its guard never trips). The Full showcase inventory would add seconds per test. Unlike
        // the connection string (read eagerly by Program.cs — see the DbContext note below), the
        // seeder reads IConfiguration lazily at run time, so this override works.
        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?> { ["DevSeed"] = "Minimal" }));

        builder.ConfigureServices(services =>
        {
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
