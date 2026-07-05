using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="CommentItem"/> (WU20). CommentItem is a pure leaf: no service
/// injection; all affordances are EventCallback-driven (.HasDelegate idiom). Tests cover:
/// callback parameters; author rendering (link vs "[deleted user]"); edit-mode RichTextView↔
/// CommentEditor swap; spoiler blur + completion-gated reveal; like/edit/delete gating.
///
/// <b>JS interop note:</b> <see cref="CommentEditor"/> wraps <see cref="EditorView"/> (Quill.js) —
/// JSInterop.Mode is Loose so the editor renders without erroring in tests.
///
/// <b>Not tested here:</b> Tailwind visual styling (human sign-off for Stage 6);
/// actual editor HTML content (empty under loose JS mode — service-level tests cover persistence).
/// <b>Tier:</b> RazorComponents (bUnit, no host or DB).
/// </summary>
public class CommentItemTests : BunitContext
{
    public CommentItemTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ── Author block ──────────────────────────────────────────────────────────────

    [Fact]
    public void CommentItem_AuthorPresent_RendersUsernameLink()
    {
        IRenderedComponent<CommentItem> cut = Render<CommentItem>(p => p
            .Add(c => c.Comment, MakeComment(1, authorId: 42, username: "Misty")));

        cut.Markup.Should().Contain("/user/42", "a link to the author profile must appear");
        cut.Markup.Should().Contain("Misty", "the author username must appear in the link");
        cut.Markup.Should().NotContain("[deleted user]");
    }

    [Fact]
    public void CommentItem_DeletedAuthor_RendersDeletedUserFallback()
    {
        IRenderedComponent<CommentItem> cut = Render<CommentItem>(p => p
            .Add(c => c.Comment, MakeComment(1, authorId: null)));

        cut.Markup.Should().Contain("[deleted user]",
            "a null AuthorId must render the '[deleted user]' fallback");
        cut.Markup.Should().NotContain("/user/",
            "no profile link must be rendered for a deleted author");
    }

    // ── RichTextView vs CommentEditor swap ────────────────────────────────────────

    [Fact]
    public void CommentItem_WhenNotEditing_RendersCommentText()
    {
        IRenderedComponent<CommentItem> cut = Render<CommentItem>(p => p
            .Add(c => c.Comment, MakeComment(1, text: "<p>Hello world</p>"))
            .Add(c => c.IsEditing, false));

        cut.Markup.Should().Contain("Hello world",
            "the comment text must render through RichTextView when not editing");
    }

    [Fact]
    public void CommentItem_WhenIsEditing_ShowsCommentEditorSaveButton()
    {
        IRenderedComponent<CommentItem> cut = Render<CommentItem>(p => p
            .Add(c => c.Comment, MakeComment(1))
            .Add(c => c.IsEditing, true)
            .Add(c => c.OnEditSave,
                EventCallback.Factory.Create<string>(this, _ => { }))
            .Add(c => c.OnEditCancel,
                EventCallback.Factory.Create(this, () => { })));

        // CommentEditor with SaveLabel="Save" renders a "Save" button
        cut.Markup.Should().Contain("Save",
            "CommentEditor must render a Save button when IsEditing is true");
    }

    [Fact]
    public void CommentItem_WhenIsEditing_ActionBarHidden()
    {
        IRenderedComponent<CommentItem> cut = Render<CommentItem>(p => p
            .Add(c => c.Comment, MakeComment(1))
            .Add(c => c.IsEditing, true)
            .Add(c => c.OnEditSave,
                EventCallback.Factory.Create<string>(this, _ => { }))
            .Add(c => c.OnEditCancel,
                EventCallback.Factory.Create(this, () => { })));

        // The action-bar role="group" must not be in the DOM while editing
        cut.FindAll("[role=group]").Should().BeEmpty(
            "the action bar must be hidden while in edit mode");
    }

    // ── Like affordance (.HasDelegate gating) ─────────────────────────────────────

    [Fact]
    public async Task CommentItem_LikeClick_InvokesOnToggleLikeWithCommentId()
    {
        long? received = null;
        IRenderedComponent<CommentItem> cut = Render<CommentItem>(p => p
            .Add(c => c.Comment, MakeComment(99))
            .Add(c => c.OnToggleLike,
                EventCallback.Factory.Create<long>(this, id => { received = id; })));

        IElement likeBtn = cut.Find("button[aria-label]");
        await likeBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        received.Should().Be(99, "OnToggleLike must be raised with the comment's CommentId");
    }

    [Fact]
    public void CommentItem_WhenOnToggleLikeNotWired_NoLikeButton()
    {
        IRenderedComponent<CommentItem> cut = Render<CommentItem>(p => p
            .Add(c => c.Comment, MakeComment(1))
            // OnToggleLike intentionally not wired
        );

        // No interactive like button — only non-interactive span or absent
        cut.FindAll("button[aria-label='Like comment']").Should().BeEmpty(
            "the like button must not render when OnToggleLike is not wired");
        cut.FindAll("button[aria-label='Unlike comment']").Should().BeEmpty();
    }

    [Fact]
    public void CommentItem_WhenOnToggleLikeNotWired_ReadOnlyCountShownWhenPositive()
    {
        IRenderedComponent<CommentItem> cut = Render<CommentItem>(p => p
            .Add(c => c.Comment, MakeComment(1, likeCount: 5))
            // OnToggleLike not wired → read-only span
        );

        cut.Markup.Should().Contain("5",
            "a non-zero LikeCount must still be visible as a read-only display");
    }

    // ── Reply affordance ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CommentItem_ReplyClick_InvokesOnReplyWithCommentId()
    {
        long? received = null;
        IRenderedComponent<CommentItem> cut = Render<CommentItem>(p => p
            .Add(c => c.Comment, MakeComment(77))
            .Add(c => c.OnReply,
                EventCallback.Factory.Create<long>(this, id => { received = id; })));

        IElement replyBtn = cut.Find("button[aria-label='Reply to comment']");
        await replyBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        received.Should().Be(77, "OnReply must be raised with the comment's CommentId");
    }

    [Fact]
    public void CommentItem_WhenOnReplyNotWired_NoReplyButton()
    {
        IRenderedComponent<CommentItem> cut = Render<CommentItem>(p => p
            .Add(c => c.Comment, MakeComment(1)));

        cut.FindAll("button[aria-label='Reply to comment']").Should().BeEmpty(
            "the Reply button must not render when OnReply is not wired");
    }

    // ── Edit affordance (IsOwnComment + .HasDelegate) ─────────────────────────────

    [Fact]
    public async Task CommentItem_EditClick_IsOwnComment_InvokesOnEditWithCommentId()
    {
        long? received = null;
        IRenderedComponent<CommentItem> cut = Render<CommentItem>(p => p
            .Add(c => c.Comment, MakeComment(55))
            .Add(c => c.IsOwnComment, true)
            .Add(c => c.OnEdit,
                EventCallback.Factory.Create<long>(this, id => { received = id; })));

        IElement editBtn = cut.Find("button[aria-label='Edit comment']");
        await editBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        received.Should().Be(55, "OnEdit must be raised with the comment's CommentId");
    }

    [Fact]
    public void CommentItem_EditButton_AbsentWhenNotOwnComment()
    {
        IRenderedComponent<CommentItem> cut = Render<CommentItem>(p => p
            .Add(c => c.Comment, MakeComment(1))
            .Add(c => c.IsOwnComment, false)
            .Add(c => c.OnEdit,
                EventCallback.Factory.Create<long>(this, _ => { })));

        cut.FindAll("button[aria-label='Edit comment']").Should().BeEmpty(
            "the Edit button must not render when IsOwnComment is false");
    }

    // ── Delete affordance (IsOwnComment + .HasDelegate) ───────────────────────────

    [Fact]
    public async Task CommentItem_DeleteClick_IsOwnComment_InvokesOnDeleteWithCommentId()
    {
        long? received = null;
        IRenderedComponent<CommentItem> cut = Render<CommentItem>(p => p
            .Add(c => c.Comment, MakeComment(88))
            .Add(c => c.IsOwnComment, true)
            .Add(c => c.OnDelete,
                EventCallback.Factory.Create<long>(this, id => { received = id; })));

        IElement deleteBtn = cut.Find("button[aria-label='Delete comment']");
        await deleteBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        received.Should().Be(88, "OnDelete must be raised with the comment's CommentId");
    }

    [Fact]
    public void CommentItem_DeleteButton_AbsentWhenNotOwnComment()
    {
        IRenderedComponent<CommentItem> cut = Render<CommentItem>(p => p
            .Add(c => c.Comment, MakeComment(1))
            .Add(c => c.IsOwnComment, false)
            .Add(c => c.OnDelete,
                EventCallback.Factory.Create<long>(this, _ => { })));

        cut.FindAll("button[aria-label='Delete comment']").Should().BeEmpty(
            "the Delete button must not render when IsOwnComment is false");
    }

    // ── Spoiler blur + completion-gated reveal (§5.9.1) ───────────────────────────

    [Fact]
    public void CommentItem_SpoilerComment_InitiallyBlurred()
    {
        IRenderedComponent<CommentItem> cut = Render<CommentItem>(p => p
            .Add(c => c.Comment, MakeComment(1, isSpoiler: true, text: "<p>Secret ending</p>"))
            .Add(c => c.UserHasCompletedStory, false));

        // The reveal button must be present before the content is revealed
        cut.FindAll("button[aria-label='Reveal spoiler']").Should().HaveCount(1,
            "a spoiler comment must show the 'Reveal spoiler' button initially");
        // The actual comment text must not be directly readable (it's behind blur)
        // We verify the reveal button is present, not that blur is visually applied (visual sign-off)
    }

    [Fact]
    public async Task CommentItem_RevealClick_WhenUserHasCompleted_RevealsImmediately()
    {
        IRenderedComponent<CommentItem> cut = Render<CommentItem>(p => p
            .Add(c => c.Comment, MakeComment(1, isSpoiler: true, text: "<p>Secret ending</p>"))
            .Add(c => c.UserHasCompletedStory, true));

        IElement revealBtn = cut.Find("button[aria-label='Reveal spoiler']");
        await revealBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cut.FindAll("button[aria-label='Reveal spoiler']").Should().BeEmpty(
            "the reveal button must disappear after revealing when UserHasCompletedStory is true");
        cut.Markup.Should().Contain("Secret ending",
            "the comment text must be visible after revealing");
    }

    [Fact]
    public async Task CommentItem_RevealClick_WhenUserHasNotCompleted_OpensSpoilerDialog()
    {
        IRenderedComponent<CommentItem> cut = Render<CommentItem>(p => p
            .Add(c => c.Comment, MakeComment(1, isSpoiler: true))
            .Add(c => c.UserHasCompletedStory, false));

        IElement revealBtn = cut.Find("button[aria-label='Reveal spoiler']");
        await revealBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // ConfirmDialog should now be open — its markup appears in the DOM
        cut.Markup.Should().Contain("haven't finished",
            "clicking Reveal when not completed must open the spoiler ConfirmDialog");
    }

    [Fact]
    public void CommentItem_NonSpoilerComment_RendersContentDirectly()
    {
        IRenderedComponent<CommentItem> cut = Render<CommentItem>(p => p
            .Add(c => c.Comment, MakeComment(1, isSpoiler: false, text: "<p>Normal comment</p>")));

        cut.FindAll("button[aria-label='Reveal spoiler']").Should().BeEmpty(
            "non-spoiler comments must never show the Reveal button");
        cut.Markup.Should().Contain("Normal comment");
    }

    // ── Helper ────────────────────────────────────────────────────────────────────

    private static CommentDto MakeComment(
        long commentId,
        long? parentId = null,
        int? authorId = 1,
        string? username = "TestUser",
        bool isSpoiler = false,
        bool isLiked = false,
        int likeCount = 0,
        string text = "<p>Test comment.</p>") =>
        new(commentId, parentId, authorId, authorId is null ? null : username,
            null, text, DateTime.UtcNow, likeCount, isSpoiler, isLiked);
}
