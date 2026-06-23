using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="ChapterText.CountWords"/> — dependency-free, no host/DB.
/// Tier: Unit (directly constructed, Core-only type — layer2-services.md
/// §"Word Count Is Computed Server-Side, On Save — From Stripped Text").
/// </summary>
public class ChapterTextTests
{
    // --- Null / empty guard ---

    [Fact]
    public void CountWords_NullInput_ReturnsZero()
        => ChapterText.CountWords(null).Should().Be(0);

    [Fact]
    public void CountWords_EmptyString_ReturnsZero()
        => ChapterText.CountWords("").Should().Be(0);

    [Fact]
    public void CountWords_WhitespaceOnly_ReturnsZero()
        => ChapterText.CountWords("   \t\n  ").Should().Be(0);

    // --- Plain text (no markup) ---

    [Theory]
    [InlineData("hello", 1)]
    [InlineData("hello world", 2)]
    [InlineData("  multiple   spaces  ", 2)]
    [InlineData("one two three four five", 5)]
    public void CountWords_PlainText_CountsWhitespaceSeparatedTokens(string text, int expected)
        => ChapterText.CountWords(text).Should().Be(expected);

    // --- HTML stripping ---

    [Fact]
    public void CountWords_TagsOnly_ReturnsZero()
        => ChapterText.CountWords("<p></p><strong></strong>").Should().Be(0);

    [Fact]
    public void CountWords_ParagraphWithText_StripsTagsAndCounts()
        => ChapterText.CountWords("<p>Hello world</p>").Should().Be(2);

    [Fact]
    public void CountWords_MultipleElements_CombinesTextAcrossTags()
        // Each closing/opening tag boundary collapses to whitespace; "one" + "two" + "three" = 3.
        => ChapterText.CountWords("<p>one</p><p>two</p><p>three</p>").Should().Be(3);

    [Fact]
    public void CountWords_NestedFormatting_StripsAllNesting()
        => ChapterText.CountWords("<p><strong>Bold</strong> and <em>italic</em></p>").Should().Be(3);

    [Fact]
    public void CountWords_Blockquote_CountsContent()
        => ChapterText.CountWords("<blockquote><p>A quoted phrase here</p></blockquote>").Should().Be(4);

    [Fact]
    public void CountWords_OrderedList_CountsItems()
        => ChapterText.CountWords("<ol><li>Item one</li><li>Item two</li></ol>").Should().Be(4);

    // --- Entity decoding ---

    [Fact]
    public void CountWords_AmpersandEntity_DecodedToSingleToken()
        // "&amp;" decodes to "&", which is not whitespace, so "rock &amp; roll" → "rock & roll" = 3.
        => ChapterText.CountWords("rock &amp; roll").Should().Be(3);

    [Fact]
    public void CountWords_NonBreakingSpace_TreatedAsWordBoundary()
        // "&nbsp;" is a non-breaking space (U+00A0); the implementation normalises it to a
        // regular space so it acts as a word boundary, not a word character.
        => ChapterText.CountWords("word&nbsp;word").Should().Be(2);

    [Fact]
    public void CountWords_LtGtEntities_DecodedAndNotCountedAsWords()
        // "&lt;b&gt;" decodes to "<b>" — if it appears alone, no word token.
        => ChapterText.CountWords("&lt;b&gt;").Should().Be(1); // "<b>" is a single token

    // --- Realistic chapter snippet ---

    [Fact]
    public void CountWords_RealisticChapterHtml_CountsReadableWords()
    {
        const string html =
            "<p>Ash looked at Pikachu. <strong>\"I choose you!\"</strong></p>" +
            "<p>The battle had begun.</p>";

        // Readable words: Ash, looked, at, Pikachu, "I, choose, you!", The, battle, had, begun.
        // = 11 tokens (punctuation attached to tokens, matches normal word-count convention)
        ChapterText.CountWords(html).Should().Be(11);
    }
}
