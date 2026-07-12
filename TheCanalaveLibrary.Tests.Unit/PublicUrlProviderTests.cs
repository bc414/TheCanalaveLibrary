using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="PublicUrlProvider"/> (WU-Seo). Pure string-concatenation, no host/DB
/// dependency — same shape as <see cref="OptimisticSpriteReadService"/>'s tests. Covers the
/// site-base/image-base split (audit/Seo.md "Two settings, both wired now") and the
/// null-image-falls-back-to-caller-supplied-default contract.
/// </summary>
public class PublicUrlProviderTests
{
    // ── AbsolutePageUrl ──────────────────────────────────────────────────────────

    [Fact]
    public void AbsolutePageUrl_PrependsSiteBase()
    {
        var sut = new PublicUrlProvider("https://thecanalavelibrary.com");

        sut.AbsolutePageUrl("/story/42/my-story").Should().Be("https://thecanalavelibrary.com/story/42/my-story");
    }

    [Fact]
    public void AbsolutePageUrl_TrailingSlashOnBase_DoesNotDoubleSlash()
    {
        var sut = new PublicUrlProvider("https://thecanalavelibrary.com/");

        sut.AbsolutePageUrl("/story/42").Should().Be("https://thecanalavelibrary.com/story/42");
    }

    [Fact]
    public void AbsolutePageUrl_RelativePathWithoutLeadingSlash_StillJoinsCorrectly()
    {
        var sut = new PublicUrlProvider("https://thecanalavelibrary.com");

        sut.AbsolutePageUrl("story/42").Should().Be("https://thecanalavelibrary.com/story/42");
    }

    // ── AbsoluteImageUrl: image base defaults to site base ──────────────────────

    [Fact]
    public void AbsoluteImageUrl_NoSeparateImageBase_UsesSiteBase()
    {
        var sut = new PublicUrlProvider("https://thecanalavelibrary.com");

        sut.AbsoluteImageUrl("/uploads/stories/42/cover.jpg", "/img/default-cover.svg")
            .Should().Be("https://thecanalavelibrary.com/uploads/stories/42/cover.jpg");
    }

    [Fact]
    public void AbsoluteImageUrl_SeparateImageBaseConfigured_UsesImageBaseNotSiteBase()
    {
        // The direct-R2/CDN seam (audit/Seo.md "Future"): og:image resolves against a distinct
        // host once ImageStorage:PublicBaseUrl is set, while og:url stays on the site base.
        var sut = new PublicUrlProvider(
            siteBaseUrl: "https://thecanalavelibrary.com",
            imageBaseUrl: "https://cdn.thecanalavelibrary.com");

        sut.AbsoluteImageUrl("/uploads/stories/42/cover.jpg", "/img/default-cover.svg")
            .Should().Be("https://cdn.thecanalavelibrary.com/uploads/stories/42/cover.jpg");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AbsoluteImageUrl_NullOrEmptyRelativePath_UsesFallback(string? relativePath)
    {
        var sut = new PublicUrlProvider("https://thecanalavelibrary.com");

        sut.AbsoluteImageUrl(relativePath, "/img/default-cover.svg")
            .Should().Be("https://thecanalavelibrary.com/img/default-cover.svg");
    }

    [Fact]
    public void AbsoluteImageUrl_FallbackAlsoResolvesAgainstImageBase()
    {
        var sut = new PublicUrlProvider(
            siteBaseUrl: "https://thecanalavelibrary.com",
            imageBaseUrl: "https://cdn.thecanalavelibrary.com");

        sut.AbsoluteImageUrl(null, "/img/default-cover.svg")
            .Should().Be("https://cdn.thecanalavelibrary.com/img/default-cover.svg");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhitespaceOrNullImageBase_FallsBackToSiteBase(string? imageBase)
    {
        var sut = new PublicUrlProvider("https://thecanalavelibrary.com", imageBase);

        sut.AbsoluteImageUrl("/uploads/x.jpg", "/img/default-cover.svg")
            .Should().Be("https://thecanalavelibrary.com/uploads/x.jpg");
    }
}
