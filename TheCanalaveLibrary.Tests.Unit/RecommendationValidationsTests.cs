using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="RecommendationValidations"/> — dependency-free, no host/DB.
/// Key boundary: <see cref="RecommendationConstants.MinLength"/> = 500 characters (plain-text
/// after HTML stripping). Callers pass sanitized HTML; validation strips + counts.
/// Tier: Unit (directly constructed, Core-only types).
/// </summary>
public class RecommendationValidationsTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>Wraps <paramref name="text"/> in a paragraph tag (minimal sanitized HTML shape).</summary>
    private static string Html(string text) => $"<p>{text}</p>";

    /// <summary>Generates a plain-text string of exactly <paramref name="length"/> characters.</summary>
    private static string Chars(int length) => new('a', length);

    // ── RecommendationSubmitDto ---

    [Fact]
    public void Submit_ExactlyMinLength_ReturnsNoErrors()
    {
        var dto = new RecommendationSubmitDto(1, "raw");
        string sanitized = Html(Chars(RecommendationConstants.MinLength));
        dto.CanSave(sanitized).Should().BeEmpty();
    }

    [Fact]
    public void Submit_OneCharBelowMinLength_ReturnsError()
    {
        var dto = new RecommendationSubmitDto(1, "raw");
        string sanitized = Html(Chars(RecommendationConstants.MinLength - 1));
        dto.CanSave(sanitized).Should().ContainSingle()
            .Which.Should().Contain("500", "error must cite the minimum");
    }

    [Fact]
    public void Submit_AboveMinLength_ReturnsNoErrors()
    {
        var dto = new RecommendationSubmitDto(1, "raw");
        string sanitized = Html(Chars(RecommendationConstants.MinLength + 100));
        dto.CanSave(sanitized).Should().BeEmpty();
    }

    [Fact]
    public void Submit_EmptySanitizedHtml_ReturnsError()
    {
        var dto = new RecommendationSubmitDto(1, "raw");
        dto.CanSave(string.Empty).Should().ContainSingle();
    }

    [Fact]
    public void Submit_HtmlTagsOnly_ReturnsError()
    {
        var dto = new RecommendationSubmitDto(1, "raw");
        dto.CanSave("<p></p><strong></strong>").Should().ContainSingle();
    }

    [Fact]
    public void Submit_MinLengthInHtml_StrippedBeforeCounting()
    {
        // HTML tags themselves must not contribute to the count.
        // 450 chars of text wrapped in many tags still fails (< 500 plain-text chars).
        string lotsOfTags = string.Concat(Enumerable.Range(0, 50).Select(i => $"<p>{Chars(9)}</p>"));
        // 50 * 9 = 450 chars, 50 paragraphs of wrapping tags that don't count.
        var dto = new RecommendationSubmitDto(1, "raw");
        dto.CanSave(lotsOfTags).Should().ContainSingle("450 plain-text chars is below the 500-char minimum");
    }

    // ── UpdateRecommendationDto ---

    [Fact]
    public void Update_ExactlyMinLength_ReturnsNoErrors()
    {
        var dto = new UpdateRecommendationDto(1, "raw");
        string sanitized = Html(Chars(RecommendationConstants.MinLength));
        dto.CanSave(sanitized).Should().BeEmpty();
    }

    [Fact]
    public void Update_OneCharBelowMinLength_ReturnsError()
    {
        var dto = new UpdateRecommendationDto(1, "raw");
        string sanitized = Html(Chars(RecommendationConstants.MinLength - 1));
        dto.CanSave(sanitized).Should().ContainSingle()
            .Which.Should().Contain("500");
    }

    [Fact]
    public void Update_AboveMinLength_ReturnsNoErrors()
    {
        var dto = new UpdateRecommendationDto(1, "raw");
        string sanitized = Html(Chars(RecommendationConstants.MinLength + 50));
        dto.CanSave(sanitized).Should().BeEmpty();
    }
}
