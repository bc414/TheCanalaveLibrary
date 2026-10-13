using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// <see cref="RichTextContent.IsBlank"/> and every validation of a required rich-text field. Found by
/// the WU-ParityAndRemaining browser pass (2026-10-13): Post Comment with an untouched editor sent
/// Quill's empty markup, <c>&lt;p&gt;&lt;br&gt;&lt;/p&gt;</c>, which <c>string.IsNullOrWhiteSpace</c>
/// passed, and an empty comment box was posted to a public thread. The same check guarded messages,
/// chapters, blog posts and the story summary. Each case below fails with its validation reverted to
/// <c>IsNullOrWhiteSpace</c>. Tier: Unit (Core-only, directly constructed).
/// </summary>
public class RichTextBlankTests
{
    private const string EmptyEditor = "<p><br></p>";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n")]
    [InlineData("<p><br></p>")]
    [InlineData("<p><br></p><p><br></p><p><br></p>")]
    [InlineData("<p>&nbsp;</p>")]
    [InlineData("<p>   </p>")]
    [InlineData("<p><strong></strong><em> </em></p>")]
    [InlineData("<p><a href=\"https://example.com\"></a></p>")]
    [InlineData("<ul><li><br></li></ul>")]
    public void IsBlank_MarkupWithNoVisibleText_IsBlank(string? html) =>
        RichTextContent.IsBlank(html).Should().BeTrue();

    [Theory]
    [InlineData("<p>Great chapter!</p>")]
    [InlineData("plain text")]
    [InlineData("<p><br></p><p>x</p>")]
    [InlineData("<p>&amp;</p>")]
    [InlineData("<p><a href=\"https://example.com\">link</a></p>")]
    public void IsBlank_MarkupWithText_IsNotBlank(string html) =>
        RichTextContent.IsBlank(html).Should().BeFalse();

    public static TheoryData<string, Func<string, List<string>>> RequiredRichTextFields => new()
    {
        { "chapter comment", t => new PostChapterCommentDto { ChapterId = 1, CommentText = t }.CanSave() },
        { "blog-post comment", t => new PostBlogPostCommentDto { BlogPostId = 1, CommentText = t }.CanSave() },
        { "group comment", t => new PostGroupCommentDto { GroupId = 1, CommentText = t }.CanSave() },
        { "profile comment", t => new PostUserProfileCommentDto(1, null, t).CanSave() },
        { "comment edit", t => new UpdateCommentDto { CommentId = 1, CommentText = t }.CanSave() },
        { "profile blog post", t => new CreateProfileBlogPostDto { Title = "T", Content = t }.CanSave() },
        { "blog post edit", t => new UpdateBlogPostDto { BlogPostId = 1, Title = "T", Content = t }.CanSave() },
        { "group blog post", t => new CreateGroupBlogPostDto { GroupId = 1, Title = "T", Content = t }.CanSave() },
        { "group blog post edit", t => new UpdateGroupBlogPostDto { BlogPostId = 1, Title = "T", Content = t }.CanSave() },
        { "site announcement", t => new CreateSiteBlogPostDto { Title = "T", Content = t }.CanSave() },
        { "site announcement edit", t => new UpdateSiteBlogPostDto { BlogPostId = 1, Title = "T", Content = t }.CanSave() },
        { "chapter", t => new CreateChapterDto { StoryId = 1, ChapterText = t }.CanSave() },
        { "chapter edit", t => new UpdateChapterContentDto { ChapterContentId = 1, ChapterText = t }.CanSave() },
        { "new conversation", t => new StartConversationDto(2, "Subject", t).Validate(senderId: 1) },
        { "message reply", t => MessagingValidations.ValidateMessageBody(t) },
        { "story summary", t => ValidStory(t).CanSave() },
    };

    [Theory]
    [MemberData(nameof(RequiredRichTextFields))]
    public void RequiredRichTextField_AnEmptiedEditor_IsRefused(string field, Func<string, List<string>> validate)
    {
        validate(EmptyEditor).Should().ContainSingle(because: $"the {field} holds no text");
        validate("<p>Some text.</p>").Should().BeEmpty(because: $"the {field} fixture is otherwise valid");
    }

    private static CreateStoryDTO ValidStory(string longDescription) => new()
    {
        Title = "Valid Title",
        ShortDescription = "Valid short description",
        LongDescription = longDescription,
        StoryTags =
        [
            new StoryTagDTO { TagId = 1, TagTypeEnum = TagTypeEnum.Setting, Priority = TagPriority.Primary },
            new StoryTagDTO { TagId = 2, TagTypeEnum = TagTypeEnum.Genre, Priority = TagPriority.Primary }
        ]
    };
}
