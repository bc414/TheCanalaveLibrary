using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

public class StorySlugTests
{
    [Theory]
    [InlineData("My Awesome Story", "my-awesome-story")]
    [InlineData("  Leading And Trailing Spaces  ", "leading-and-trailing-spaces")]
    [InlineData("Gen 4/5: Sinnoh & Unova!", "gen-4-5-sinnoh-unova")]
    [InlineData("ALL CAPS TITLE", "all-caps-title")]
    [InlineData("multiple---dashes", "multiple-dashes")]
    public void Slugify_ProducesExpectedSlug(string title, string expectedSlug)
    {
        StorySlug.Slugify(title).Should().Be(expectedSlug);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    public void Slugify_OfTitleWithNoAlphanumerics_ReturnsEmptyString(string title)
    {
        // The "story" fallback for an empty base slug is GenerateUniqueSlugAsync's job
        // (Server/Stories/ServerStoryWriteService.cs), not Slugify's — this asserts the pure
        // transform's actual boundary so that fallback isn't silently duplicated or lost.
        StorySlug.Slugify(title).Should().BeEmpty();
    }

    // The third segment of /story/{id}/{segment} is a route, not a slug, when it is the editor's
    // literal or a chapter number (WU-StoryLifecycle browser pass, 2026-09-30: the canonical-slug
    // redirect treated "edit" as a stale slug and 301'd the editor of every published story).
    [Theory]
    [InlineData("edit")]
    [InlineData("Edit")]
    [InlineData("EDIT")]
    [InlineData("1")]
    [InlineData("1984")]
    public void IsReservedRouteSegment_TheEditorLiteralAndChapterNumbers_AreRoutes(string segment)
    {
        StorySlug.IsReservedRouteSegment(segment).Should().BeTrue();
    }

    [Theory]
    [InlineData("my-story")]
    [InlineData("edit-2")]
    [InlineData("editor")]
    [InlineData("1984-2")]
    [InlineData("chapter")]
    [InlineData("99999999999")] // overflows int: the router's int constraint rejects it, so it IS a slug
    public void IsReservedRouteSegment_OrdinarySlugs_AreNot(string segment)
    {
        StorySlug.IsReservedRouteSegment(segment).Should().BeFalse();
    }
}
