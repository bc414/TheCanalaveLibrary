using System.Security.Cryptography;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Adds the security response headers (security.md §"Response Headers &amp; CSP") to every
/// response, and mints the per-request CSP nonce that App.razor feeds to <c>&lt;ImportMap/&gt;</c>.
///
/// CSP environment gating: ENFORCED outside Development, Report-Only in Development — dev
/// tooling (hot reload, WebAssembly debugging) injects scripts that a strict policy would
/// break, and the integration-test host runs Development, so tests assert the Report-Only
/// header. All non-CSP headers are enforced in every environment. Headers are added via
/// OnStarting so they land even when a component starts streaming the response.
/// </summary>
public class SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment env)
{
    /// <summary>HttpContext.Items key App.razor reads the per-request nonce from.</summary>
    public const string NonceItemKey = "canalave:csp-nonce";

    private readonly bool _enforceCsp = !env.IsDevelopment();

    public Task InvokeAsync(HttpContext context)
    {
        string nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        context.Items[NonceItemKey] = nonce;

        context.Response.OnStarting(() =>
        {
            IHeaderDictionary headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers[_enforceCsp ? "Content-Security-Policy" : "Content-Security-Policy-Report-Only"] =
                CspPolicy.Build(nonce);
            return Task.CompletedTask;
        });

        return next(context);
    }
}
