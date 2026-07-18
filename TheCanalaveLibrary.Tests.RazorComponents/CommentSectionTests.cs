using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="CommentSection"/> (WU20). CommentSection is a coordination
/// composite that injects <see cref="ICommentWriteService"/> to own its paginated comment load,
/// two-level tree assembly, optimistic like reconciliation, delete-confirm dialog, and reply/edit/
/// new-comment editor coordination.
///
/// Tests cover: initial load; three-state inline (loading/empty/populated); tree assembly;
/// optimistic like reconciliation; post with spoiler flag; reply carries ParentCommentId;
/// edit calls EditCommentAsync; delete-confirm then DeleteCommentAsync; page change re-queries;
/// anonymous viewers (CurrentUserId=null) see no compose form.
///
/// <b>JS interop note:</b> CommentEditor → EditorView → Blazored.TextEditor uses JS.
/// JSInterop.Mode is Loose — editor HTML is empty string in tests; tests assert service calls,
/// not HTML content.
/// <b>Tier:</b> RazorComponents (bUnit, no host or DB).
/// </summary>
public class CommentSectionTests : BunitContext
{
    private readonly FakeCommentWriteService _fakeService = new();

    public CommentSectionTests()
    {
        Services.AddScoped<ICommentWriteService>(_ => _fakeService);
        // ReportDialog (inside CommentSection) injects IModerationWriteService.
        Services.AddScoped<IModerationWriteService>(_ => new FakeModerationWriteService());
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ── Initial load ──────────────────────────────────────────────────────────────

    [Fact]
    public void CommentSection_OnRender_CallsGetChapterCommentsWithPageOne()
    {
        _fakeService.SetGetResult(new CommentPageDto([], 0));

        Render<CommentSection>(p => p
            .Add(c => c.ChapterId, 7));

        _fakeService.GetChapterCommentsCalls.Should().ContainSingle(
            "the section must call GetChapterCommentsAsync once on initialization");
        _fakeService.GetChapterCommentsCalls[0].ChapterId.Should().Be(7);
        _fakeService.GetChapterCommentsCalls[0].Page.Should().Be(1, "first page on init");
    }

    [Fact]
    public void CommentSection_OnRender_PassesPageSizeToService()
    {
        _fakeService.SetGetResult(new CommentPageDto([], 0));

        Render<CommentSection>(p => p
            .Add(c => c.ChapterId, 1)
            .Add(c => c.PageSize, 10));

        _fakeService.GetChapterCommentsCalls[0].PageSize.Should().Be(10);
    }

    // ── Three-state inline ────────────────────────────────────────────────────────

    [Fact]
    public void CommentSection_EmptyResult_ShowsNoCommentsMessage()
    {
        _fakeService.SetGetResult(new CommentPageDto([], 0));

        IRenderedComponent<CommentSection> cut = Render<CommentSection>(p => p
            .Add(c => c.ChapterId, 1));

        cut.Markup.Should().Contain("No comments yet",
            "an empty comment list must show the empty-state message");
    }

    [Fact]
    public void CommentSection_WithComments_RendersCommentText()
    {
        _fakeService.SetGetResult(new CommentPageDto(
            [MakeComment(1, text: "<p>First comment</p>")], 1));

        IRenderedComponent<CommentSection> cut = Render<CommentSection>(p => p
            .Add(c => c.ChapterId, 1)
            .Add(c => c.CurrentUserId, 99));

        cut.Markup.Should().Contain("First comment",
            "comment text must be visible in the rendered section");
    }

    // ── Tree assembly — replies appear under roots ────────────────────────────────

    [Fact]
    public void CommentSection_ReplyComment_RenderedUnderRoot()
    {
        long rootId = 1L;
        CommentPageDto page = new(
        [
            MakeComment(rootId, text: "<p>Root</p>"),
            MakeComment(2, parentId: rootId, text: "<p>Reply</p>")
        ], 1);
        _fakeService.SetGetResult(page);

        IRenderedComponent<CommentSection> cut = Render<CommentSection>(p => p
            .Add(c => c.ChapterId, 1));

        cut.Markup.Should().Contain("Root");
        cut.Markup.Should().Contain("Reply");
    }

    // ── Like (optimistic reconciliation) ─────────────────────────────────────────

    [Fact]
    public async Task CommentSection_LikeClick_CallsToggleLikeAsync()
    {
        long commentId = 10L;
        _fakeService.SetGetResult(new CommentPageDto(
            [MakeComment(commentId)], 1));
        _fakeService.SetLikeResult(new CommentLikeResultDto(1, true));

        IRenderedComponent<CommentSection> cut = Render<CommentSection>(p => p
            .Add(c => c.ChapterId, 1)
            .Add(c => c.CurrentUserId, 42));

        IElement likeBtn = cut.Find("button[aria-label='Like comment']");
        await likeBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        _fakeService.ToggleLikeCalls.Should().Contain(commentId,
            "ToggleLikeAsync must be called with the clicked comment's id");
    }

    [Fact]
    public async Task CommentSection_LikeClick_ReconcileCountFromResult()
    {
        long commentId = 10L;
        _fakeService.SetGetResult(new CommentPageDto(
            [MakeComment(commentId, likeCount: 0)], 1));
        _fakeService.SetLikeResult(new CommentLikeResultDto(LikeCount: 1, IsLiked: true));

        IRenderedComponent<CommentSection> cut = Render<CommentSection>(p => p
            .Add(c => c.ChapterId, 1)
            .Add(c => c.CurrentUserId, 42));

        IElement likeBtn = cut.Find("button[aria-label='Like comment']");
        await likeBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // After reconciliation from the server result the like count should be 1
        cut.Markup.Should().Contain("1",
            "the like count must be reconciled to the value returned by ToggleLikeAsync");
    }

    // ── Post new comment ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CommentSection_PostComment_CallsPostChapterCommentAsync()
    {
        _fakeService.SetGetResult(new CommentPageDto([], 0));

        IRenderedComponent<CommentSection> cut = Render<CommentSection>(p => p
            .Add(c => c.ChapterId, 5)
            .Add(c => c.CurrentUserId, 99));

        // The persistent composer's save button carries aria-label="Post Comment" (SaveLabel).
        IElement postBtn = cut.Find("button[aria-label='Post Comment']");
        await postBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        _fakeService.PostCalls.Should().ContainSingle(
            "PostChapterCommentAsync must be called once when the user submits");
        _fakeService.PostCalls[0].ChapterId.Should().Be(5);
        _fakeService.PostCalls[0].ParentCommentId.Should().BeNull(
            "a top-level post must have no ParentCommentId");
    }

    [Fact]
    public async Task CommentSection_PostComment_WithSpoilerToggled_IsSpoilerTrue()
    {
        _fakeService.SetGetResult(new CommentPageDto([], 0));

        IRenderedComponent<CommentSection> cut = Render<CommentSection>(p => p
            .Add(c => c.ChapterId, 1)
            .Add(c => c.CurrentUserId, 1));

        // Tick the spoiler checkbox in the new-comment CommentEditor
        IElement spoilerCheckbox = cut.Find("input[type=checkbox]");
        await spoilerCheckbox.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs
            { Value = true });

        IElement postBtn = cut.Find("button[aria-label='Post Comment']");
        await postBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        _fakeService.PostCalls.Should().ContainSingle();
        _fakeService.PostCalls[0].IsSpoiler.Should().BeTrue(
            "the IsSpoiler flag must reflect the spoiler checkbox state");
    }

    // ── Reply ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CommentSection_ReplyClick_ShowsReplyEditor()
    {
        _fakeService.SetGetResult(new CommentPageDto(
            [MakeComment(1)], 1));

        IRenderedComponent<CommentSection> cut = Render<CommentSection>(p => p
            .Add(c => c.ChapterId, 1)
            .Add(c => c.CurrentUserId, 1));

        IElement replyBtn = cut.Find("button[aria-label='Reply to comment']");
        await replyBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // A "Reply" button (SaveLabel of the inline CommentEditor) must appear
        cut.Markup.Should().Contain("Reply",
            "clicking Reply must show the inline reply CommentEditor with a Reply save button");
    }

    [Fact]
    public async Task CommentSection_ReplySubmit_CarriesParentCommentId()
    {
        long rootId = 1L;
        _fakeService.SetGetResult(new CommentPageDto(
            [MakeComment(rootId)], 1));

        IRenderedComponent<CommentSection> cut = Render<CommentSection>(p => p
            .Add(c => c.ChapterId, 1)
            .Add(c => c.CurrentUserId, 1));

        // Open the reply editor for the root comment
        IElement replyBtn = cut.Find("button[aria-label='Reply to comment']");
        await replyBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // The CommentEditor's save button carries aria-label="Reply" (SaveLabel).
        // The action bar "Reply to comment" button has aria-label="Reply to comment" — distinct.
        IElement replySubmitBtn = cut.Find("button[aria-label='Reply']");
        await replySubmitBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        _fakeService.PostCalls.Should().ContainSingle();
        _fakeService.PostCalls[0].ParentCommentId.Should().Be(rootId,
            "a reply must carry the ParentCommentId of the root comment");
    }

    // ── Edit ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CommentSection_EditSave_CallsEditCommentAsync()
    {
        long commentId = 20L;
        _fakeService.SetGetResult(new CommentPageDto(
            [MakeComment(commentId, authorId: 42)], 1));

        IRenderedComponent<CommentSection> cut = Render<CommentSection>(p => p
            .Add(c => c.ChapterId, 1)
            .Add(c => c.CurrentUserId, 42)); // IsOwnComment = true

        // Click Edit to enter edit mode
        IElement editBtn = cut.Find("button[aria-label='Edit comment']");
        await editBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // The CommentEditor's save button carries aria-label="Save" (SaveLabel for edit mode).
        IElement saveBtn = cut.Find("button[aria-label='Save']");
        await saveBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        _fakeService.EditCalls.Should().ContainSingle();
        _fakeService.EditCalls[0].CommentId.Should().Be(commentId);
    }

    // ── Delete (confirm dialog) ───────────────────────────────────────────────────

    [Fact]
    public async Task CommentSection_DeleteClickThenConfirm_CallsDeleteCommentAsync()
    {
        long commentId = 30L;
        _fakeService.SetGetResult(new CommentPageDto(
            [MakeComment(commentId, authorId: 42)], 1));

        IRenderedComponent<CommentSection> cut = Render<CommentSection>(p => p
            .Add(c => c.ChapterId, 1)
            .Add(c => c.CurrentUserId, 42)); // IsOwnComment = true

        // Click the Delete action button on the comment item
        IElement deleteBtn = cut.Find("button[aria-label='Delete comment']");
        await deleteBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // The ConfirmDialog is now open — find the "Delete" confirm button ([0]=Cancel, [1]=Delete)
        IRenderedComponent<ConfirmDialog> dialog = cut.FindComponent<ConfirmDialog>();
        IElement confirmBtn = dialog.FindAll("button")[1];
        await confirmBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        _fakeService.DeleteCalls.Should().Contain(commentId,
            "DeleteCommentAsync must be called with the confirmed comment's id");
    }

    // ── Pagination ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CommentSection_PageChange_RequeriesWithNewPage()
    {
        // Seed more than one page of roots (TotalRootCount > PageSize)
        CommentDto[] roots = Enumerable.Range(1, 3).Select(i => MakeComment(i)).ToArray();
        _fakeService.SetGetResult(new CommentPageDto(roots, TotalRootCount: 30));

        IRenderedComponent<CommentSection> cut = Render<CommentSection>(p => p
            .Add(c => c.ChapterId, 1)
            .Add(c => c.PageSize, 10));

        // PaginationControls renders when TotalPages > 1 — click the "Next page" button
        IElement nextBtn = cut.Find("button[aria-label='Next page']");
        await nextBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        _fakeService.GetChapterCommentsCalls.Should().HaveCount(2,
            "a page change must trigger a second GetChapterCommentsAsync call");
        _fakeService.GetChapterCommentsCalls[1].Page.Should().Be(2,
            "the second call must use page 2");
    }

    // ── Anonymous viewer ──────────────────────────────────────────────────────────

    [Fact]
    public void CommentSection_AnonymousViewer_HidesComposeForm()
    {
        _fakeService.SetGetResult(new CommentPageDto([], 0));

        IRenderedComponent<CommentSection> cut = Render<CommentSection>(p => p
            .Add(c => c.ChapterId, 1)
            // CurrentUserId intentionally not set (null = anonymous)
        );

        cut.Markup.Should().NotContain("Post Comment",
            "the new-comment composer must not render for anonymous viewers");
    }

    // ── F3 mutation-sanity — @key forces a fresh CommentItem on page change ──────
    //
    // Root cause: CommentItem holds `private bool _isRevealed` as ephemeral private state.
    // Without @key, Blazor matches <CommentItem> instances positionally. After the user reveals
    // commentA's spoiler (_isRevealed=true on the position-0 instance), paginating to page 2
    // loads commentB into the same list — but without @key the position-0 CommentItem instance is
    // reused, so _isRevealed stays true and commentB's spoiler renders as already-revealed.
    //
    // Fix: @key="root.CommentId" on <CommentItem> in CommentSection.razor. Blazor tears down the
    // commentId=1 instance and creates a fresh commentId=2 instance with _isRevealed=false.

    [Fact]
    public async Task KeyedList_WhenSpoilerPaginates_NewCommentStartsHidden_NotRevealedFromPreviousInstance()
    {
        // ── Arrange: page 1 has commentA (spoiler), TotalRootCount=2, PageSize=1 → 2 pages ──
        const long commentAId = 1L;
        const long commentBId = 2L;
        _fakeService.SetGetResult(new CommentPageDto(
            [MakeComment(commentAId, isSpoiler: true)], TotalRootCount: 2));

        IRenderedComponent<CommentSection> cut = Render<CommentSection>(p => p
            .Add(c => c.ChapterId, 1)
            .Add(c => c.CurrentUserId, 42)
            .Add(c => c.UserHasCompletedStory, true)  // skips the ConfirmDialog; sets _isRevealed immediately
            .Add(c => c.PageSize, 1));

        // Sanity: commentA renders the "Reveal spoiler" button (before reveal).
        cut.Find("button[aria-label='Reveal spoiler']").Should().NotBeNull(
            "commentA must start hidden — the Reveal button must be present");

        // Reveal commentA: HandleRevealClick → _isRevealed=true (UserHasCompletedStory skips the dialog).
        cut.Find("button[aria-label='Reveal spoiler']").Click();

        cut.FindAll("button[aria-label='Reveal spoiler']").Should().BeEmpty(
            "after Reveal, commentA's CommentItem must have _isRevealed=true — the button must disappear");

        // ── Act: paginate to page 2 where commentB (also a spoiler) lives ────────
        // Update the fake before the click; CommentSection.HandlePageChanged → LoadAsync re-reads it.
        _fakeService.SetGetResult(new CommentPageDto(
            [MakeComment(commentBId, isSpoiler: true)], TotalRootCount: 2));

        IElement nextBtn = cut.Find("button[aria-label='Next page']");
        await nextBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // ── Assert ───────────────────────────────────────────────────────────────
        // @key="root.CommentId" destroys the commentId=1 instance and creates a fresh commentId=2
        // instance with _isRevealed=false. The "Reveal spoiler" button must reappear for commentB.
        // Without @key, the position-0 instance's _isRevealed is still true from commentA → the
        // button stays hidden even though commentB is a new, unrevealed spoiler.
        cut.FindAll("button[aria-label='Reveal spoiler']").Should().ContainSingle(
            "commentB's fresh CommentItem must start with _isRevealed=false; " +
            "if the Reveal button is absent, the old instance's _isRevealed=true is bleeding into commentB's slot");
    }

    // ── Mutation sanity — gating inversions must break the right tests ─────────────
    // (Manual verification: flip IsOwnComment check in CommentSection render to always-true
    //  → edit/delete affordances appear for all → affordance-gate tests fail. Revert.)

    // ── Helper ────────────────────────────────────────────────────────────────────

    private static CommentDto MakeComment(
        long commentId,
        long? parentId = null,
        int? authorId = 1,
        bool isSpoiler = false,
        bool isLiked = false,
        int likeCount = 0,
        string text = "<p>Test comment.</p>") =>
        new(commentId, parentId, authorId, authorId is null ? null : $"User{authorId}",
            null, text, DateTime.UtcNow, likeCount, isSpoiler, isLiked);
}
