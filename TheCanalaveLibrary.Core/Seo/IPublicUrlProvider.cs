namespace TheCanalaveLibrary.Core;

/// <summary>
/// Resolves origin-relative paths (page routes, stored image paths) to absolute URLs for contexts
/// that must be canonical regardless of the serving request — chiefly Open Graph/Twitter meta tags
/// (<see cref="Seo.SocialMetaTags"/> — SharedUI), which social-media crawlers read from the raw
/// prerendered HTML and which must never echo an internal proxy/droplet host. See
/// <c>render-and-layout.md</c> §"Social Meta Tags (Open Graph)" and <c>audit/Seo.md</c> for why the
/// base is always configured, never derived from the current request.
/// </summary>
public interface IPublicUrlProvider
{
    /// <summary>
    /// Resolves a route (e.g. <c>/story/42/my-story</c>) to the canonical absolute page URL.
    /// </summary>
    string AbsolutePageUrl(string relativePath);

    /// <summary>
    /// Resolves a stored image path (e.g. a <c>CoverArtRelativeUrl</c>) to an absolute URL,
    /// substituting <paramref name="fallbackRelativePath"/> when <paramref name="relativePath"/>
    /// is null or empty. Resolved against the image base, which defaults to the same base as
    /// <see cref="AbsolutePageUrl"/> unless a separate image/CDN base is configured — see
    /// <c>audit/Seo.md</c> "Two settings, both wired now."
    /// </summary>
    string AbsoluteImageUrl(string? relativePath, string fallbackRelativePath);
}
