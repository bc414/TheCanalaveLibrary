using System.Net;
using System.Text;
using FluentAssertions;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// The HTTP edge rate limiter through the real pipeline (security.md §"HTTP Edge Rate
/// Limiting"): per-IP fixed window (10/min) on POST /Account/*, and the "TagWrites" policy
/// (30/min) on the tag write endpoints. The requests deliberately carry garbage bodies — the
/// limiter middleware counts the request before antiforgery/model-binding rejects it, so the
/// tests never need a real form post, and 400s on the way to the 429 are expected. In the
/// TestServer every request shares one partition (no RemoteIpAddress → "unknown"), which is
/// exactly what a single-attacker window looks like.
/// </summary>
[Collection("Postgres")]
public sealed class HttpRateLimitTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task AuthFormPosts_Return429WithRetryAfterAndABody_PastTheWindowLimit()
    {
        using HttpClient client = Factory.CreateClient();

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
        using HttpClient client = Factory.CreateClient();

        for (int i = 0; i < 15; i++)
        {
            using HttpResponseMessage get = await client.GetAsync("/Account/Login");
            get.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests, "GETs are not limited");
        }
    }

    [Fact]
    public async Task TagWrites_Return429_PastTheirPolicyWindow()
    {
        using HttpClient client = Factory.CreateClient();

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
        using HttpClient client = Factory.CreateClient();

        for (int i = 0; i < 35; i++)
        {
            using HttpResponseMessage read = await client.GetAsync("/api/tags/directory");
            read.StatusCode.Should().Be(HttpStatusCode.OK, "the TagWrites policy is writes-only");
        }
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string path) =>
        client.PostAsync(path, new StringContent("{}", Encoding.UTF8, "application/json"));
}
