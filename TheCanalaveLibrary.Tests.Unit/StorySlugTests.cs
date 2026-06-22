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
}
