using FluentAssertions;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// The CSP directive string (security.md §"Response Headers &amp; CSP") — pure string assembly,
/// unit-tested so a careless edit can't silently drop a Blazor-load-bearing directive.
/// </summary>
public sealed class CspPolicyTests
{
    [Fact]
    public void Build_ContainsEveryLoadBearingDirective()
    {
        string csp = CspPolicy.Build("test-nonce");

        csp.Should().Contain("default-src 'self'");
        // Blazor runtime requirement (both render modes).
        csp.Should().Contain("'wasm-unsafe-eval'");
        // The SignalR circuit is a WebSocket.
        csp.Should().Contain("connect-src 'self' ws: wss:");
        // The ImportMap inline script is authorized by the per-request nonce.
        csp.Should().Contain("'nonce-test-nonce'");
        // Quill's CDN — the only external origin.
        csp.Should().Contain("https://cdn.jsdelivr.net");
        csp.Should().Contain("object-src 'none'");
        csp.Should().Contain("base-uri 'self'");
        csp.Should().Contain("form-action 'self'");
        csp.Should().Contain("frame-ancestors 'none'");
    }

    [Fact]
    public void Build_NeverAllowsInlineOrEvalScript()
    {
        string csp = CspPolicy.Build("n");

        string scriptSrc = csp.Split(';').Single(d => d.TrimStart().StartsWith("script-src"));
        scriptSrc.Should().NotContain("'unsafe-inline'",
            "inline script is the XSS payload vector CSP exists to block");
        // 'wasm-unsafe-eval' is allowed; bare 'unsafe-eval' (full JS eval) is not.
        scriptSrc.Should().NotContain(" 'unsafe-eval'");
    }

    [Fact]
    public void Build_EmbedsTheExactNonceItWasGiven()
    {
        CspPolicy.Build("abc123==").Should().Contain("'nonce-abc123=='");
    }
}
