using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="CommentSection"/> in the group context (WU32).
/// Covers: initial load calls GetGroupCommentsAsync; post dispatches PostGroupCommentAsync;
/// no-spoiler-toggle for groups; exactly-one-set guard rejects zero or two ids.
/// Mirrors the chapter/blog-post tests in CommentSectionTests — only the dispatch branch differs.
/// Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class CommentSectionGroupTests : BunitContext
{
    private readonly FakeCommentWriteService _fakeService = new();

    public CommentSectionGroupTests()
    {
        Services.AddScoped<ICommentWriteService>(_ => _fakeService);
        // ReportDialog (inside CommentSection) injects IModerationWriteService.
        Services.AddScoped<IModerationWriteService>(_ => new FakeModerationWriteService());
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ── Initial load ──────────────────────────────────────────────────────────────

    [Fact]
    public void CommentSection_GroupContext_OnRender_CallsGetGroupCommentsAsync()
    {
        _fakeService.SetGetResult(new CommentPageDto([], 0));

        Render<CommentSection>(p => p
            .Add(c => c.GroupId, 99));

        _fakeService.GetGroupCommentsCalls.Should().ContainSingle(
            "the section must call GetGroupCommentsAsync once on initialization");
        _fakeService.GetGroupCommentsCalls[0].GroupId.Should().Be(99);
        _fakeService.GetGroupCommentsCalls[0].Page.Should().Be(1, "first page on init");
    }

    // ── Post dispatch ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task CommentSection_GroupContext_PostComment_CallsPostGroupCommentAsync()
    {
        _fakeService.SetGetResult(new CommentPageDto([], 0));

        IRenderedComponent<CommentSection> cut = Render<CommentSection>(p => p
            .Add(c => c.GroupId, 55)
            .Add(c => c.CurrentUserId, 1));

        // Locate and submit the new-comment form via the "Post Comment" button.
        cut.Find("button[aria-label='Post Comment']").Click();
        // Wait for the async post to resolve (fake completes synchronously via Task.FromResult).
        cut.WaitForState(() => _fakeService.PostGroupCalls.Count > 0, TimeSpan.FromSeconds(2));

        _fakeService.PostGroupCalls.Should().ContainSingle(
            "submitting the comment form in group context must call PostGroupCommentAsync");
        _fakeService.PostGroupCalls[0].GroupId.Should().Be(55);
    }

    // ── No spoiler toggle in group context ────────────────────────────────────────

    [Fact]
    public void CommentSection_GroupContext_ComposerHasNoSpoilerToggle()
    {
        _fakeService.SetGetResult(new CommentPageDto([], 0));

        IRenderedComponent<CommentSection> cut = Render<CommentSection>(p => p
            .Add(c => c.GroupId, 1)
            .Add(c => c.CurrentUserId, 1));

        // "Contains spoilers" checkbox should not appear (spoiler toggle is chapter-only).
        cut.Markup.Should().NotContain("Contains spoilers",
            "spoiler toggle must not render in the group comment context");
    }

    // ── Exactly-one-set guard ─────────────────────────────────────────────────────

    [Fact]
    public void CommentSection_NoneSet_ThrowsInvalidOperation()
    {
        _fakeService.SetGetResult(new CommentPageDto([], 0));
        Action act = () => Render<CommentSection>(); // no GroupId / ChapterId / BlogPostId
        act.Should().Throw<InvalidOperationException>("exactly one target must be set");
    }

    [Fact]
    public void CommentSection_TwoSet_ThrowsInvalidOperation()
    {
        _fakeService.SetGetResult(new CommentPageDto([], 0));
        Action act = () => Render<CommentSection>(p => p
            .Add(c => c.GroupId, 1)
            .Add(c => c.ChapterId, 2)); // two set at once
        act.Should().Throw<InvalidOperationException>();
    }
}
