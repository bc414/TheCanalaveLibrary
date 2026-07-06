namespace TheCanalaveLibrary.Server;

/// <summary>
/// Builds the Content-Security-Policy directive string — pure static so the policy is
/// unit-testable without a host. Directive rationale lives in security.md §"Response Headers
/// &amp; CSP"; the Blazor-specific allowances are:
/// <list type="bullet">
/// <item><c>'wasm-unsafe-eval'</c> — required by the Blazor runtime (both render modes).</item>
/// <item><c>ws: wss:</c> in connect-src — the SignalR circuit is a WebSocket.</item>
/// <item>the per-request nonce — authorizes the inline <c>&lt;script type="importmap"&gt;</c>
/// that <c>&lt;ImportMap/&gt;</c> renders (App.razor consumes the same nonce).</item>
/// <item><c>https://cdn.jsdelivr.net</c> — Quill.js + its stylesheet (SRI-pinned in App.razor);
/// the only CDN origin in use.</item>
/// <item><c>'unsafe-inline'</c> for STYLES only — Blazor's reconnect UI and Quill both inject
/// inline styles; never for scripts.</item>
/// </list>
/// </summary>
public static class CspPolicy
{
    public static string Build(string nonce) =>
        "default-src 'self'; " +
        $"script-src 'self' 'wasm-unsafe-eval' 'nonce-{nonce}' https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "img-src 'self' data:; " +
        "font-src 'self' data:; " +
        "connect-src 'self' ws: wss:; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";
}
