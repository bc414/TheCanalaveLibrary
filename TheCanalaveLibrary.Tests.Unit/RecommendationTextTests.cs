using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="RecommendationText.CountPlainTextLength"/> — dependency-free, no host/DB.
/// Tier: Unit (directly constructed, Core-only type — mirrors <c>ChapterTextTests</c> pattern).
/// </summary>
public class RecommendationTextTests
{
    // --- Null / empty guard ---

    [Fact]
    public void CountPlainTextLength_NullInput_ReturnsZero()
        => RecommendationText.CountPlainTextLength(null).Should().Be(0);

    [Fact]
    public void CountPlainTextLength_EmptyString_ReturnsZero()
        => RecommendationText.CountPlainTextLength("").Should().Be(0);

    [Fact]
    public void CountPlainTextLength_WhitespaceOnly_ReturnsZero()
        => RecommendationText.CountPlainTextLength("   \t\n  ").Should().Be(0);

    // --- Plain text (no markup) ---

    [Theory]
    [InlineData("hello", 5)]
    [InlineData("hello world", 11)]
    [InlineData("  leading and trailing  ", 20)]
    public void CountPlainTextLength_PlainText_ReturnsTrimmedLength(string text, int expected)
        => RecommendationText.CountPlainTextLength(text).Should().Be(expected);

    // --- HTML stripping ---

    [Fact]
    public void CountPlainTextLength_TagsOnly_ReturnsZero()
        => RecommendationText.CountPlainTextLength("<p></p><strong></strong>").Should().Be(0);

    [Fact]
    public void CountPlainTextLength_ParagraphWithText_StripsTagsAndCountsChars()
    {
        // "<p>Hello world</p>" strips to "Hello world" = 11 chars.
        RecommendationText.CountPlainTextLength("<p>Hello world</p>").Should().Be(11);
    }

    [Fact]
    public void CountPlainTextLength_MultipleElements_CollapsesBoundaryToEmpty()
    {
        // Tags stripped to empty; "one" + "two" = 6 chars (no whitespace between when tags removed).
        int result = RecommendationText.CountPlainTextLength("<p>one</p><p>two</p>");
        result.Should().Be(6, "tag removal yields no gap between runs");
    }

    // --- Entity decoding ---

    [Fact]
    public void CountPlainTextLength_AmpersandEntity_DecodedToSingleChar()
        // "&amp;" decodes to "&" (1 char), not 5 chars.
        => RecommendationText.CountPlainTextLength("&amp;").Should().Be(1);

    [Fact]
    public void CountPlainTextLength_NonBreakingSpace_TreatedAsRegularSpace()
        // "&nbsp;" (U+00A0) is normalised to a regular space by the implementation.
        => RecommendationText.CountPlainTextLength("a&nbsp;b").Should().Be(3);
}
