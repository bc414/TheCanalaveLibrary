namespace TheCanalaveLibrary.Core;

/// <summary>
/// Shared fallback constants for <see cref="IPublicUrlProvider.AbsoluteImageUrl"/> callers that
/// pass a fallback (WU-SweepRiders, tracker item E4). One constant, not one literal per call
/// site — the OG fallback was previously <c>/img/default-cover.svg</c> repeated across seven
/// SharedUI pages, which is how a fallback path drifts.
/// </summary>
public static class SeoDefaults
{
    /// <summary>
    /// Fallback <c>og:image</c>/<c>twitter:image</c> for content with no image of its own
    /// (a series, a group, a blog post, a coverless story or avatar-less profile). A raster,
    /// deliberately: social crawlers (Twitter/Facebook in particular) do not reliably rasterize
    /// SVG for card images, unlike the in-page <c>&lt;img&gt;</c> placeholders
    /// (<c>default-cover.svg</c>/<c>default-avatar.svg</c>), which stay SVG and untouched by this
    /// constant. See <c>audit/Seo.md</c> "Open" (now resolved) for the full crawler-compatibility
    /// rationale.
    /// </summary>
    public const string OgFallbackImagePath = "/img/og-default.png";
}
