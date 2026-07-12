namespace TheCanalaveLibrary.Core;

/// <summary>
/// Stateless, pure string-concatenation implementation of <see cref="IPublicUrlProvider"/> — the
/// single shared implementation used on both Server and Client, exactly like
/// <see cref="OptimisticSpriteReadService"/>. Each host constructs it with a different base:
/// Server reads <c>Site:PublicBaseUrl</c>/<c>ImageStorage:PublicBaseUrl</c> from configuration
/// (canonical — what a social crawler's single unauthenticated GET sees, per
/// <c>audit/Seo.md</c>); Client constructs it from <c>NavigationManager.BaseUri</c> (interactive
/// re-render only — crawlers never reach the Client host, so exactness there doesn't matter).
///
/// <para><paramref name="imageBaseUrl"/> defaults to <paramref name="siteBaseUrl"/> when null or
/// whitespace — today every image is served same-origin through the app even in S3/R2 mode (see
/// <c>Server/Images/ImageEndpoints.cs</c>); a distinct image base is the seam a future
/// direct-R2/CDN image-serving migration would set.</para>
/// </summary>
public sealed class PublicUrlProvider(string siteBaseUrl, string? imageBaseUrl = null) : IPublicUrlProvider
{
    private readonly string _siteBaseUrl = siteBaseUrl.TrimEnd('/');
    private readonly string _imageBaseUrl = (string.IsNullOrWhiteSpace(imageBaseUrl) ? siteBaseUrl : imageBaseUrl).TrimEnd('/');

    public string AbsolutePageUrl(string relativePath) => Combine(_siteBaseUrl, relativePath);

    public string AbsoluteImageUrl(string? relativePath, string fallbackRelativePath) =>
        Combine(_imageBaseUrl, string.IsNullOrEmpty(relativePath) ? fallbackRelativePath : relativePath);

    private static string Combine(string baseUrl, string relativePath) =>
        $"{baseUrl}/{relativePath.TrimStart('/')}";
}
