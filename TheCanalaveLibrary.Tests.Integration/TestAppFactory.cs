using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Boots the real <c>TheCanalaveLibrary.Server</c> host (the actual <c>Program.cs</c>, not a hand-built
/// substitute) against a Testcontainers Postgres connection string, with the real claims-based
/// <see cref="ServerActiveUserContext"/> swapped for a settable <see cref="FakeActiveUserContext"/> —
/// see testing.md "Driving the content-rating filter." Everything else (both DbContexts, the real
/// service registrations, Identity) is wired exactly as production; this is the standard "swap one DI
/// registration" WebApplicationFactory pattern, not a parallel test-only composition root.
///
/// Each test method gets its own instance (xUnit constructs a fresh test-class instance per `[Fact]`),
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

        // Development, not a dedicated "Testing" environment — this is the same environment the real
        // dev workflow runs in (see run-server/SKILL.md), so Program.cs's `IsDevelopment()` branches
        // (dev-diagnostics endpoints, the DataSeeder call) behave identically to a real local run. The
        // DataSeeder is idempotent (checks for "TestUser" before inserting), so re-running it against
        // an already-seeded container on a later test in the same collection is a no-op.
        builder.UseEnvironment(Environments.Development);

        // Redirect uploads to a per-factory temp folder — LocalImageStorageService writes under
        // IWebHostEnvironment.WebRootPath, and tests must never touch the real wwwroot/uploads/.
        builder.UseWebRoot(WebRootPath);

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:canalavedb"] = connectionString
            });
        });

        builder.ConfigureServices(services =>
        {
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
