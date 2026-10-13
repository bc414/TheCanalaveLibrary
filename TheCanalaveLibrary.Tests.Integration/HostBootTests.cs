using FluentAssertions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Cheap regression for the WU0-class failure mode: a service constructor dependency that the DI
/// container can't actually satisfy (the original symptom was <c>IDbContextFactory&lt;T&gt;</c> never
/// being registered) only surfaces when something forces the provider to validate, which doesn't
/// happen until a scope is actually created and a service resolved. Every other test class in this
/// project does that implicitly; this one exists so a future DI-wiring break has one obvious, fast,
/// purpose-named test to point at instead of being discovered as a confusing failure three tests away.
/// </summary>
[Collection("Postgres")]
public class HostBootTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    [Fact]
    public void TheHostBuilds_AndResolvesEveryStoryAndImageService()
    {
        Action act = () =>
        {
            using IServiceScope scope = Factory.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IActiveUserContext>();
            scope.ServiceProvider.GetRequiredService<IStoryReadService>();
            scope.ServiceProvider.GetRequiredService<IStoryWriteService>();
            scope.ServiceProvider.GetRequiredService<IImageStorageService>();
            scope.ServiceProvider.GetRequiredService<ImageOrphanSweeper>();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void TheSharedHost_RunsNoOrphanSweepWorker()
    {
        // Without TestAppFactory's removal the shared host would sweep its upload folder 5 s after
        // boot, while tests write to it (WU-BlobCleanup).
        Factory.Services.GetServices<IHostedService>()
            .Should().NotContain(s => s is ImageOrphanSweepWorker);
    }

    [Fact]
    public void NoStaticAssetEndpointServesAnUpload()
    {
        // Uploads are runtime data, served by UseStaticFiles from the web root (Local mode) or by
        // ImageEndpoints from the bucket (S3 mode). A file that sat under wwwroot/uploads/ at build time
        // used to become a MapStaticAssets endpoint, and the endpoint outlived the file: once the orphan
        // sweep or an inline delete erased it, its URL answered 200 with an empty body instead of 404
        // (WU-BlobCleanup browser pass, 2026-10-13). The Server csproj now keeps the folder out of the
        // build's static web assets. This test can fail only where wwwroot/uploads/ held a file when the
        // Server was built (any dev workbench that has run the server-only path), never on a clean
        // checkout. Literal routes only: S3 mode's /uploads/{**key} is not a static asset.
        EndpointDataSource endpoints = Factory.Services.GetRequiredService<EndpointDataSource>();

        endpoints.Endpoints.OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText ?? string.Empty)
            .Where(route => route.TrimStart('/').StartsWith("uploads/", StringComparison.OrdinalIgnoreCase)
                            && !route.Contains('{'))
            .Should().BeEmpty();
    }

    [Fact]
    public void TheFakeActiveUserContext_ReplacesTheRealClaimsBasedImplementation()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IActiveUserContext resolved = scope.ServiceProvider.GetRequiredService<IActiveUserContext>();

        resolved.Should().BeOfType<FakeActiveUserContext>();
    }
}
