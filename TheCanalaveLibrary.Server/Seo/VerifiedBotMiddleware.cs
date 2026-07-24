namespace TheCanalaveLibrary.Server;

/// <summary>
/// Stamps <see cref="ItemKey"/> = true into <c>HttpContext.Items</c> when the request comes from
/// a verified search crawler (Pattern B serving — WU-AccessGate; audit/Seo.md). Surfaced as
/// <c>IActiveUserContext.IsVerifiedBot</c>; gated pages serve full content + adult labels to
/// verified bots instead of the consent interstitial.
///
/// <b>Config-gated OFF by default (<c>Seo:TrustVerifiedBots</c>).</b> The verification signal is
/// Cloudflare's Verified Bots edge header — trustworthy only once the Phase-7 trust boundary
/// (ForwardedHeaders + origin lockdown, Program.cs Phase-7 note) guarantees requests can only
/// arrive via Cloudflare. Before that, any direct-to-origin caller could spoof the header and
/// read M content bot-style — which is why activation is a launch-readiness checklist line, not
/// a deploy default. Until enabled, crawlers get the interstitial (Pattern A), which carries the
/// indexable title/author/rating + adult labels — the deliberate interim, not a degraded state.
///
/// Pattern mirror: <see cref="SecurityHeadersMiddleware"/> (per-request value via Items).
/// </summary>
public class VerifiedBotMiddleware(RequestDelegate next, IConfiguration config)
{
    /// <summary>HttpContext.Items key ServerActiveUserContext reads.</summary>
    public const string ItemKey = "canalave:verified-bot";

    /// <summary>
    /// Cloudflare Enterprise Bot Management populates <c>cf-verified-bot</c> ("true"/"false");
    /// the Verified Bots category is also exposed via <c>cf-bot-score</c> plans. The exact header
    /// contract is re-checked at Phase-7 activation — until then this middleware is inert.
    /// </summary>
    private const string CloudflareVerifiedBotHeader = "cf-verified-bot";

    private readonly bool _enabled = config.GetValue<bool>("Seo:TrustVerifiedBots");

    public Task InvokeAsync(HttpContext context)
    {
        if (_enabled
            && context.Request.Headers.TryGetValue(CloudflareVerifiedBotHeader, out var value)
            && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
        {
            context.Items[ItemKey] = true;
        }

        return next(context);
    }
}
