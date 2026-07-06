using FluentAssertions;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// SecurityHeadersMiddleware through the real pipeline (security.md §"Response Headers &amp; CSP").
/// The test host runs the Development environment (TestAppFactory), so CSP arrives as
/// Content-Security-Policy-Report-Only — the enforced header is production-only by design;
/// the switch itself is a one-line environment check verified by reading the middleware.
/// </summary>
[Collection("Postgres")]
public sealed class SecurityHeadersTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    [Theory]
    [InlineData("/")]
    [InlineData("/api/tags/directory")]
    public async Task EveryResponse_CarriesTheSecurityHeaderSet(string path)
    {
        using HttpClient client = Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(path);

        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().ContainSingle("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().ContainSingle("strict-origin-when-cross-origin");
        response.Headers.Contains("Permissions-Policy").Should().BeTrue();
        response.Headers.GetValues("Cross-Origin-Opener-Policy").Should().ContainSingle("same-origin");
    }

    [Fact]
    public async Task InDevelopment_TheFullCspIsReportOnly_AndCarriesThePerRequestNonce()
    {
        using HttpClient client = Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/");

        // The FULL policy is Report-Only in Development (dev tooling scripts must not break).
        // A separate enforced Content-Security-Policy IS present even in Development — the
        // framework emits "frame-ancestors 'none'" from AddInteractiveServerRenderMode's
        // ContentSecurityFrameAncestorsPolicy option — but it must never grow script directives.
        foreach (string enforced in response.Headers.TryGetValues("Content-Security-Policy", out var values)
                     ? values : [])
        {
            enforced.Should().NotContain("script-src",
                "the full script policy is Report-Only in Development; only the framework's frame-ancestors header is enforced");
            enforced.Should().Contain("frame-ancestors");
        }

        string csp = response.Headers.GetValues("Content-Security-Policy-Report-Only").Single();
        csp.Should().Contain("'wasm-unsafe-eval'").And.Contain("frame-ancestors 'none'");
        csp.Should().MatchRegex("'nonce-[A-Za-z0-9+/=]+'", "the nonce is minted per request");
    }

    [Fact]
    public async Task TheCspNonce_IsUniquePerRequest()
    {
        using HttpClient client = Factory.CreateClient();

        using HttpResponseMessage first = await client.GetAsync("/");
        using HttpResponseMessage second = await client.GetAsync("/");

        string NonceOf(HttpResponseMessage r) => System.Text.RegularExpressions.Regex
            .Match(r.Headers.GetValues("Content-Security-Policy-Report-Only").Single(), "'nonce-([^']+)'")
            .Groups[1].Value;

        NonceOf(first).Should().NotBe(NonceOf(second));
    }
}
