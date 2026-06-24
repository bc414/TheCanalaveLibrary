using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;

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
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void TheFakeActiveUserContext_ReplacesTheRealClaimsBasedImplementation()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IActiveUserContext resolved = scope.ServiceProvider.GetRequiredService<IActiveUserContext>();

        resolved.Should().BeOfType<FakeActiveUserContext>();
    }
}
