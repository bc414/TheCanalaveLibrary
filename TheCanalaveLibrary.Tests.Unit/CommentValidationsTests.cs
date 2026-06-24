using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="CommentValidations"/> — dependency-free, no host/DB.
/// Tier: Unit (directly constructed, Core-only type — mirrors <c>ChapterTextTests</c> pattern).
/// </summary>
public class CommentValidationsTests
{
    // --- PostChapterCommentDto ---

    [Fact]
    public void PostComment_EmptyText_ReturnsError()
    {
        var dto = new PostChapterCommentDto { ChapterId = 1, CommentText = "" };
        dto.CanSave().Should().ContainSingle()
            .Which.Should().Contain("empty");
    }

    [Fact]
    public void PostComment_WhitespaceOnly_ReturnsError()
    {
        var dto = new PostChapterCommentDto { ChapterId = 1, CommentText = "   \t\n" };
        dto.CanSave().Should().ContainSingle()
            .Which.Should().Contain("empty");
    }

    [Fact]
    public void PostComment_ValidText_ReturnsNoErrors()
    {
        var dto = new PostChapterCommentDto { ChapterId = 1, CommentText = "<p>Great chapter!</p>" };
        dto.CanSave().Should().BeEmpty();
    }

    [Fact]
    public void PostComment_SpoilerFlag_DoesNotAffectValidation()
    {
        // IsSpoiler doesn't change the validation result — either way valid text is valid.
        var dto = new PostChapterCommentDto { ChapterId = 1, CommentText = "<p>Spoiler text</p>", IsSpoiler = true };
        dto.CanSave().Should().BeEmpty();
    }

    // --- UpdateCommentDto ---

    [Fact]
    public void UpdateComment_EmptyText_ReturnsError()
    {
        var dto = new UpdateCommentDto { CommentId = 1, CommentText = "" };
        dto.CanSave().Should().ContainSingle()
            .Which.Should().Contain("empty");
    }

    [Fact]
    public void UpdateComment_WhitespaceOnly_ReturnsError()
    {
        var dto = new UpdateCommentDto { CommentId = 1, CommentText = "  " };
        dto.CanSave().Should().ContainSingle()
            .Which.Should().Contain("empty");
    }

    [Fact]
    public void UpdateComment_ValidText_ReturnsNoErrors()
    {
        var dto = new UpdateCommentDto { CommentId = 1, CommentText = "<p>Edited text</p>" };
        dto.CanSave().Should().BeEmpty();
    }
}
