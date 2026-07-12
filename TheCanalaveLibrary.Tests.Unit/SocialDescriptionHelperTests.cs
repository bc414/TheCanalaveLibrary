using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="SocialDescriptionHelper"/> (WU-Seo). Pure text transform, no host/DB
/// dependency. Covers HTML stripping (LongDescription/BlogPostDto.Content are sanitized HTML, not
/// plain text), entity decoding, whitespace collapsing, and word-boundary truncation.
/// </summary>
public class SocialDescriptionHelperTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Clean_NullOrWhitespaceInput_ReturnsNull(string? input)
    {
        SocialDescriptionHelper.Clean(input).Should().BeNull();
    }

    [Fact]
    public void Clean_TagsOnlyInput_ReturnsNull()
    {
        SocialDescriptionHelper.Clean("<p></p><br/>").Should().BeNull();
    }

    [Fact]
    public void Clean_PlainTextWithNoTags_IsUnchanged()
    {
        SocialDescriptionHelper.Clean("A gym leader's last stand.").Should().Be("A gym leader's last stand.");
    }

    [Fact]
    public void Clean_StripsHtmlTags()
    {
        SocialDescriptionHelper.Clean("<p>A story about <strong>rivalry</strong>.</p>")
            .Should().Be("A story about rivalry .");
    }

    [Fact]
    public void Clean_DecodesHtmlEntities()
    {
        SocialDescriptionHelper.Clean("Team Rocket&#39;s plan &amp; the aftermath")
            .Should().Be("Team Rocket's plan & the aftermath");
    }

    [Fact]
    public void Clean_CollapsesRepeatedWhitespace()
    {
        SocialDescriptionHelper.Clean("Line one\n\n\nLine   two")
            .Should().Be("Line one Line two");
    }

    [Fact]
    public void Clean_ShortInput_NotTruncated()
    {
        string input = "A short blurb.";

        SocialDescriptionHelper.Clean(input).Should().Be(input);
    }

    [Fact]
    public void Clean_ExactlyAtMaxLength_NotTruncated()
    {
        string input = new string('a', 50);

        SocialDescriptionHelper.Clean(input, maxLength: 50).Should().Be(input);
    }

    [Fact]
    public void Clean_LongInput_TruncatesAtWordBoundaryWithEllipsis()
    {
        string input = "The quick brown fox jumps over the lazy dog and keeps running far into the distance";

        string? result = SocialDescriptionHelper.Clean(input, maxLength: 30);

        result.Should().NotBeNull();
        result!.Length.Should().BeLessThanOrEqualTo(31); // 30 + ellipsis char
        result.Should().EndWith("…");
        result.Should().NotContain("  ");
        // Must not cut mid-word: strip the ellipsis and confirm it's a prefix of a full word list.
        string withoutEllipsis = result[..^1].TrimEnd();
        input.Should().StartWith(withoutEllipsis);
    }

    [Fact]
    public void Clean_LongInputWithNoSpaces_HardTruncatesAtMaxLength()
    {
        string input = new string('a', 300);

        string? result = SocialDescriptionHelper.Clean(input, maxLength: 200);

        result.Should().NotBeNull();
        result!.Should().Be(new string('a', 200) + "…");
    }

    [Fact]
    public void Clean_DefaultMaxLength_Is200()
    {
        SocialDescriptionHelper.DefaultMaxLength.Should().Be(200);
    }
}
