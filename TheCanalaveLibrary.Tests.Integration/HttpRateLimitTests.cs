using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// The HTTP edge rate limiter through the real pipeline (security.md §"HTTP Edge Rate
/// Limiting"): per-IP fixed window (10/min) on POST /Account/*, and the "TagWrites" policy
/// (30/min) on the tag write endpoints. The requests deliberately carry garbage bodies — the
/// limiter middleware counts the request before antiforgery/model-binding rejects it, so the
/// tests never need a real form post, and 400s on the way to the 429 are expected. In the
/// TestServer every request shares one partition (no RemoteIpAddress → "unknown"), which is
/// exactly what a single-attacker window looks like.
///
/// <b>Own per-test host.</b> The rate limiter is stateful middleware whose per-partition windows
/// do not replenish within a run, so these tests cannot share the collection-wide host (a window
/// consumed by an earlier test would fail the next). Each test runs against its own fresh
/// <see cref="TestAppFactory"/> — the same isolation principle <see cref="WriteThrottleTests"/>
/// uses for the service-layer throttle. Identities are still seeded through the shared DB.
/// </summary>
[Collection("Postgres")]
public sealed class HttpRateLimitTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private readonly PostgresFixture _postgres = postgres;
    private TestAppFactory _factory = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _factory = new TestAppFactory(_postgres.ConnectionString);
    }

    public override Task DisposeAsync()
    {
        _factory.Dispose();
        return base.DisposeAsync();
    }

    /// <summary>Sets the isolated factory's own fake (its DI container is separate from the shared one).</summary>
    private void SetIsolatedActiveUser(FakeActiveUserContext ctx)
    {
        FakeActiveUserContext fake = _factory.Services.GetRequiredService<FakeActiveUserContext>();
        fake.UserId          = ctx.UserId;
        fake.IsAuthenticated = ctx.IsAuthenticated;
        fake.IsModerator     = ctx.IsModerator;
        fake.IsAdmin         = ctx.IsAdmin;
    }

    [Fact]
    public async Task AuthFormPosts_Return429WithRetryAfterAndABody_PastTheWindowLimit()
    {
        using HttpClient client = _factory.CreateClient();

        for (int i = 0; i < 10; i++)
        {
            using HttpResponseMessage allowed = await PostAsync(client, "/Account/Login");
            allowed.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests,
                $"attempt {i + 1} is within the 10/min window");
        }

        using HttpResponseMessage rejected = await PostAsync(client, "/Account/Login");

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        rejected.Headers.Contains("Retry-After").Should().BeTrue();
        // Body-less error responses get re-executed into /not-found by
        // UseStatusCodePagesWithReExecute — the body is load-bearing (security.md).
        (await rejected.Content.ReadAsStringAsync()).Should().NotBeEmpty();
    }

    [Fact]
    public async Task AuthWindow_OnlyCoversAccountPosts_GetsAndOtherRoutesStayUnlimited()
    {
        using HttpClient client = _factory.CreateClient();

        for (int i = 0; i < 15; i++)
        {
            using HttpResponseMessage get = await client.GetAsync("/Account/Login");
            get.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests, "GETs are not limited");
        }
    }

    [Fact]
    public async Task TagWrites_Return429_PastTheirPolicyWindow()
    {
        // Tag writes carry a RequireAuthorization() floor (endpoint-authz sweep 2026-07-18), so the
        // request must authenticate to reach the rate limiter at all — a moderator is the real
        // caller. The garbage body still 400s after the limiter counts the request, exactly as
        // before; only the auth gate is new.
        int modId = await SeedUserAsync("mod");
        SetIsolatedActiveUser(FakeActiveUserContext.Moderator(modId));

        using HttpClient client = _factory.CreateClient();

        for (int i = 0; i < 30; i++)
        {
            using HttpResponseMessage allowed = await PostAsync(client, "/api/tags/");
            allowed.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests,
                $"write {i + 1} is within the 30/min TagWrites window");
        }

        using HttpResponseMessage rejected = await PostAsync(client, "/api/tags/");

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task TagReads_AreNeverRateLimited()
    {
        using HttpClient client = _factory.CreateClient();

        for (int i = 0; i < 35; i++)
        {
            using HttpResponseMessage read = await client.GetAsync("/api/tags/directory");
            read.StatusCode.Should().Be(HttpStatusCode.OK, "the TagWrites policy is writes-only");
        }
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string path) =>
        client.PostAsync(path, new StringContent("{}", Encoding.UTF8, "application/json"));
}
