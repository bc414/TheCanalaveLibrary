using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="BlogPostCard"/>'s spoiler snippet suppression (WU-B2): the
/// body-derived <c>ContentSnippet</c> would leak exactly what <c>HasSpoilers</c> hides on the
/// post page, so spoiler posts replace it with a muted placeholder line in every listing
/// (profile Blog tab, group feed).
/// Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class BlogPostCardTests : BunitContext
{
    private static BlogPostListingDto MakeListing(bool hasSpoilers) =>
        new(
            BlogPostId: 1,
            Title: "Musings",
            ContentSnippet: "The secret ending is revealed here.",
            DateCreated: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            Rating: Rating.E,
            HasSpoilers: hasSpoilers);

    [Fact]
    public void SpoilerPost_HidesSnippet_ShowsPlaceholder()
    {
        var cut = Render<BlogPostCard>(p => p.Add(c => c.Post, MakeListing(hasSpoilers: true)));

        cut.Markup.Should().NotContain("secret ending",
            "the body-derived snippet must never render for a HasSpoilers post");
        cut.Markup.Should().Contain("Content hidden — contains spoilers");
        cut.Markup.Should().Contain("⚠ Spoilers", "the meta badge stays");
    }

    [Fact]
    public void NonSpoilerPost_ShowsSnippet()
    {
        var cut = Render<BlogPostCard>(p => p.Add(c => c.Post, MakeListing(hasSpoilers: false)));

        cut.Markup.Should().Contain("secret ending");
        cut.Markup.Should().NotContain("Content hidden");
    }
}
