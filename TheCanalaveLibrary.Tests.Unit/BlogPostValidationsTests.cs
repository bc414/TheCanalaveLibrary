using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="BlogPostValidations"/> (WU31) — dependency-free, no host/DB.
/// Mirrors <see cref="CommentValidationsTests"/>.
/// Tier: Unit (directly constructed, Core-only types).
/// </summary>
public class BlogPostValidationsTests
{
    // --- CreateProfileBlogPostDto ---

    [Fact]
    public void Create_EmptyTitle_ReturnsError()
    {
        var dto = new CreateProfileBlogPostDto { Title = "", Content = "<p>Some content</p>" };
        dto.CanSave().Should().ContainSingle()
            .Which.Should().Contain("Title");
    }

    [Fact]
    public void Create_WhitespaceTitleOnly_ReturnsError()
    {
        var dto = new CreateProfileBlogPostDto { Title = "   ", Content = "<p>Some content</p>" };
        dto.CanSave().Should().ContainSingle()
            .Which.Should().Contain("Title");
    }

    [Fact]
    public void Create_TitleOver256Chars_ReturnsError()
    {
        var dto = new CreateProfileBlogPostDto
        {
            Title   = new string('A', 257),
            Content = "<p>Some content</p>"
        };
        dto.CanSave().Should().ContainSingle()
            .Which.Should().Contain("256");
    }

    [Fact]
    public void Create_TitleExactly256Chars_ReturnsNoErrors()
    {
        var dto = new CreateProfileBlogPostDto
        {
            Title   = new string('A', 256),
            Content = "<p>Some content</p>"
        };
        dto.CanSave().Should().BeEmpty();
    }

    [Fact]
    public void Create_EmptyContent_ReturnsError()
    {
        var dto = new CreateProfileBlogPostDto { Title = "Valid Title", Content = "" };
        dto.CanSave().Should().ContainSingle()
            .Which.Should().Contain("Content");
    }

    [Fact]
    public void Create_WhitespaceContentOnly_ReturnsError()
    {
        var dto = new CreateProfileBlogPostDto { Title = "Valid Title", Content = "   " };
        dto.CanSave().Should().ContainSingle()
            .Which.Should().Contain("Content");
    }

    [Fact]
    public void Create_ValidTitleAndContent_ReturnsNoErrors()
    {
        var dto = new CreateProfileBlogPostDto
        {
            Title   = "My Blog Post",
            Content = "<p>Hello world</p>"
        };
        dto.CanSave().Should().BeEmpty();
    }

    [Fact]
    public void Create_BothTitleAndContentEmpty_ReturnsTwoErrors()
    {
        var dto = new CreateProfileBlogPostDto { Title = "", Content = "" };
        dto.CanSave().Should().HaveCount(2);
    }

    // --- UpdateBlogPostDto ---

    [Fact]
    public void Update_EmptyTitle_ReturnsError()
    {
        var dto = new UpdateBlogPostDto { BlogPostId = 1, Title = "", Content = "<p>Content</p>" };
        dto.CanSave().Should().ContainSingle()
            .Which.Should().Contain("Title");
    }

    [Fact]
    public void Update_ValidDto_ReturnsNoErrors()
    {
        var dto = new UpdateBlogPostDto
        {
            BlogPostId  = 42,
            Title       = "Updated Post",
            Content     = "<p>Updated content</p>",
            IsPublished = true
        };
        dto.CanSave().Should().BeEmpty();
    }
}
