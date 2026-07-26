using AngleSharp.Dom;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="BlogPostPage"/>'s spoiler content interstitial (WU-B2):
/// blur curtain + "⚠ Reveal spoiler" Control for non-authors on <c>HasSpoilers</c> posts;
/// completion-gated reveal (immediate when non-story-linked or
/// <c>BlogPostDto.ViewerHasCompletedStory</c>; <see cref="ConfirmDialog"/> otherwise);
/// author auto-reveal; anonymous viewers get the curtain.
///
/// <b>Not tested here:</b> visual blur appearance (human/browser sign-off); the mature
/// content-gate path (ContentGateTests, Integration); notification generation (Integration tier).
/// Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class BlogPostPageTests : BunitContext
{
    private const int AuthorId = 42;
    private const int ViewerId = 7;

    private readonly FakeBlogPostPageService _blogService = new();
    private readonly BunitAuthorizationContext _auth;

    public BlogPostPageTests()
    {
        // Page injections: IBlogPostWriteService (post load + like), IPollReadService (poll blocks),
        // IPublicUrlProvider (SocialMetaTags). CommentSection (inside the page) injects
        // ICommentWriteService.
        Services.AddScoped<IBlogPostWriteService>(_ => _blogService);
        Services.AddScoped<IPollReadService>(_ => new FakeEmptyPollReadService());
        Services.AddScoped<IPublicUrlProvider>(_ => new PublicUrlProvider("https://test.local"));
        Services.AddScoped<ICommentWriteService>(_ => new FakeCommentWriteService());
        // CommentSection nests ReportDialog (moderation write) + toast feedback — same
        // registration set GroupPageTests uses for its comment wall.
        Services.AddScoped<IModerationWriteService>(_ => new FakeModerationWriteService());
        Services.AddScoped<IToastService>(_ => new FakeToastService());
        // RichTextView uses JS interop.
        JSInterop.Mode = JSRuntimeMode.Loose;
        _auth = this.AddAuthorization();
    }

    // ── Factory helpers ───────────────────────────────────────────────────────────

    private static BlogPostDto MakePost(
        bool hasSpoilers = false,
        int? storyId = null,
        bool viewerHasCompletedStory = false) =>
        new(
            BlogPostId: 1,
            AuthorId: AuthorId,
            AuthorDisplayName: "Author",
            Title: "Spoilery Musings",
            Content: "<p>The secret ending is revealed here.</p>",
            Rating: Rating.E,
            HasSpoilers: hasSpoilers,
            StoryId: storyId,
            LinkedStoryTitle: storyId is null ? null : "The Linked Story",
            DateCreated: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            LastUpdatedDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            LikeCount: 0,
            IsLikedByCurrentUser: false,
            IsPublished: true,
            ViewerHasCompletedStory: viewerHasCompletedStory);

    private void AuthenticateAs(int userId) =>
        _auth.SetAuthorized($"user-{userId}")
             .SetClaims(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));

    private IRenderedComponent<BlogPostPage> RenderPage(BlogPostDto post)
    {
        _blogService.Post = post;
        return Render<BlogPostPage>(p => p.Add(c => c.BlogPostId, post.BlogPostId));
    }

    private static IElement RevealButton(IRenderedComponent<BlogPostPage> cut) =>
        cut.FindAll("button").Single(b => b.TextContent.Contains("Reveal spoiler"));

    private static bool CurtainShown(IRenderedComponent<BlogPostPage> cut) =>
        cut.FindAll("button").Any(b => b.TextContent.Contains("Reveal spoiler"));

    /// <summary>The ConfirmDialog's confirm button (label "Reveal", exact — distinct from the
    /// curtain's "⚠ Reveal spoiler" overlay button).</summary>
    private static IElement? DialogConfirmButton(IRenderedComponent<BlogPostPage> cut) =>
        cut.FindAll("button").FirstOrDefault(b => b.TextContent.Trim() == "Reveal");

    // ── Curtain visibility ────────────────────────────────────────────────────────

    [Fact]
    public void SpoilerPost_NonAuthor_RendersBlurCurtain()
    {
        AuthenticateAs(ViewerId);
        var cut = RenderPage(MakePost(hasSpoilers: true));

        CurtainShown(cut).Should().BeTrue("a HasSpoilers post must render spoiler-covered for non-authors");
        cut.Markup.Should().Contain("blur-md");
    }

    [Fact]
    public void SpoilerPost_AnonymousViewer_RendersBlurCurtain()
    {
        var cut = RenderPage(MakePost(hasSpoilers: true));

        CurtainShown(cut).Should().BeTrue("anonymous viewers get the curtain too");
    }

    [Fact]
    public void SpoilerPost_Author_SeesNoCurtain()
    {
        AuthenticateAs(AuthorId);
        var cut = RenderPage(MakePost(hasSpoilers: true));

        CurtainShown(cut).Should().BeFalse("the author wrote the spoiler — auto-reveal");
        cut.Markup.Should().Contain("secret ending");
    }

    [Fact]
    public void NonSpoilerPost_HasNoCurtain()
    {
        AuthenticateAs(ViewerId);
        var cut = RenderPage(MakePost(hasSpoilers: false));

        CurtainShown(cut).Should().BeFalse();
    }

    // ── Reveal flow ───────────────────────────────────────────────────────────────

    [Fact]
    public void NonStoryLinkedSpoiler_RevealClick_UnblursImmediately()
    {
        AuthenticateAs(ViewerId);
        var cut = RenderPage(MakePost(hasSpoilers: true, storyId: null));

        RevealButton(cut).Click();

        CurtainShown(cut).Should().BeFalse("nothing to completion-gate on — immediate reveal");
        cut.Markup.Should().NotContain("blur-md");
        cut.Markup.Should().NotContain("You haven't finished the linked story");
    }

    [Fact]
    public void StoryLinked_ViewerCompleted_RevealClick_UnblursWithoutDialog()
    {
        AuthenticateAs(ViewerId);
        var cut = RenderPage(MakePost(hasSpoilers: true, storyId: 5, viewerHasCompletedStory: true));

        RevealButton(cut).Click();

        CurtainShown(cut).Should().BeFalse("a viewer who finished the linked story reveals in one click");
        cut.Markup.Should().NotContain("You haven't finished the linked story");
    }

    [Fact]
    public void StoryLinked_ViewerNotCompleted_RevealClick_OpensConfirmDialog_StaysBlurred()
    {
        AuthenticateAs(ViewerId);
        var cut = RenderPage(MakePost(hasSpoilers: true, storyId: 5, viewerHasCompletedStory: false));

        RevealButton(cut).Click();

        cut.Markup.Should().Contain("You haven't finished the linked story",
            "not-completed viewers get the confirm step first");
        cut.Markup.Should().Contain("blur-md", "content stays covered until the dialog is confirmed");
    }

    [Fact]
    public void StoryLinked_ViewerNotCompleted_ConfirmingDialog_Reveals()
    {
        AuthenticateAs(ViewerId);
        var cut = RenderPage(MakePost(hasSpoilers: true, storyId: 5, viewerHasCompletedStory: false));

        RevealButton(cut).Click();
        DialogConfirmButton(cut)!.Click();

        CurtainShown(cut).Should().BeFalse("confirming the dialog reveals the content");
        cut.Markup.Should().Contain("secret ending");
    }

    [Fact]
    public void StoryLinked_ViewerNotCompleted_CancellingDialog_StaysCovered()
    {
        AuthenticateAs(ViewerId);
        var cut = RenderPage(MakePost(hasSpoilers: true, storyId: 5, viewerHasCompletedStory: false));

        RevealButton(cut).Click();
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Back").Click();

        CurtainShown(cut).Should().BeTrue("cancelling must keep the curtain in place");
        cut.Markup.Should().Contain("blur-md");
    }

    // ── Fakes (local to this page's injection needs) ─────────────────────────────

    private sealed class FakeBlogPostPageService : IBlogPostWriteService
    {
        public BlogPostDto? Post { get; set; }

        public Task<BlogPostDto?> GetByIdAsync(int blogPostId) => Task.FromResult(Post);

        public Task<GatedMetadataDto?> GetBlogPostGateAsync(int blogPostId) =>
            Task.FromResult<GatedMetadataDto?>(null);

        public Task<(BlogPostListingDto[] Items, int TotalCount)> GetByAuthorAsync(
            int authorId, int page, int pageSize, bool includeUnpublished = false) =>
            Task.FromResult((Array.Empty<BlogPostListingDto>(), 0));

        public Task<BlogPostEditDto?> GetForEditAsync(int blogPostId) =>
            Task.FromResult<BlogPostEditDto?>(null);

        public Task<(BlogPostListingDto[] Items, int TotalCount)> GetByGroupAsync(
            int groupId, int page, int pageSize) =>
            Task.FromResult((Array.Empty<BlogPostListingDto>(), 0));

        public Task<int> CreateProfileBlogPostAsync(CreateProfileBlogPostDto dto) =>
            throw new NotImplementedException();

        public Task UpdateBlogPostAsync(UpdateBlogPostDto dto) =>
            throw new NotImplementedException();

        public Task DeleteBlogPostAsync(int blogPostId) =>
            throw new NotImplementedException();

        public Task<BlogPostLikeResultDto> ToggleLikeAsync(int blogPostId) =>
            Task.FromResult(new BlogPostLikeResultDto(1, true));

        public Task<int> CreateGroupBlogPostAsync(CreateGroupBlogPostDto dto) =>
            throw new NotImplementedException();
    }

    private sealed class FakeEmptyPollReadService : IPollReadService
    {
        public Task<PollDto[]> GetSitePollsAsync(bool includeArchived) => Task.FromResult(Array.Empty<PollDto>());
        public Task<PollDto[]> GetPollsForBlogPostAsync(int blogPostId) => Task.FromResult(Array.Empty<PollDto>());
        public Task<PollDto?> GetPollAsync(int pollId) => Task.FromResult<PollDto?>(null);
    }
}
