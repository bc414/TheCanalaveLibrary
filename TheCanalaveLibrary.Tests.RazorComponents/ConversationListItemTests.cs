using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="ConversationListItem"/> (WU35). Covers: unread count badge
/// renders when <c>UnreadCount &gt; 0</c> and is absent when zero; no per-row archived marker
/// is rendered (WU-MsgArchive); IsSelected changes the highlight class.
///
/// <b>Tier:</b> RazorComponents (bUnit, no host or DB).
/// </summary>
public class ConversationListItemTests : BunitContext
{
    private static ConversationSummaryDto MakeConversation(int unreadCount = 0, bool isArchived = false)
        => new(
            ConversationId: 1,
            Subject: "Test conversation",
            OtherParticipant: new MessagingParticipantDto(
                UserId: 2, Username: "Ash", AvatarUrl: "/img/default-avatar.svg"),
            LastMessagePreview: "Looking forward to it!",
            LastMessageDate: DateTime.UtcNow.AddMinutes(-5),
            UnreadCount: unreadCount,
            IsArchived: isArchived);

    // ── Unread badge ──────────────────────────────────────────────────────────────

    [Fact]
    public void ConversationListItem_UnreadCountGreaterThanZero_RendersBadge()
    {
        IRenderedComponent<ConversationListItem> cut = Render<ConversationListItem>(p => p
            .Add(c => c.Conversation, MakeConversation(unreadCount: 3)));

        // Badge has aria-label "N unread messages"
        cut.Markup.Should().Contain("unread messages",
            "unread badge must render when UnreadCount > 0");
        cut.Markup.Should().Contain("3");
    }

    [Fact]
    public void ConversationListItem_UnreadCountZero_NoBadge()
    {
        IRenderedComponent<ConversationListItem> cut = Render<ConversationListItem>(p => p
            .Add(c => c.Conversation, MakeConversation(unreadCount: 0)));

        cut.Markup.Should().NotContain("unread messages",
            "unread badge must not render when UnreadCount is 0");
    }

    // ── Archived rows ─────────────────────────────────────────────────────────────
    // The per-row "Archived" chip was retired in WU-MsgArchive: MessagesPage splits Inbox and
    // Archived into tabs, so every row in the archived list is archived and a chip says nothing.
    // These two tests pin that deletion so it isn't reintroduced by reflex, and pin the unread
    // badge staying visible for archived rows — the property that keeps sticky archiving honest
    // (a reply to an archived thread must remain discoverable). See layer2-services.md
    // §"Conversation Archiving Is Sticky".

    [Fact]
    public void ConversationListItem_IsArchivedTrue_RendersNoArchivedChip()
    {
        IRenderedComponent<ConversationListItem> cut = Render<ConversationListItem>(p => p
            .Add(c => c.Conversation, MakeConversation(isArchived: true)));

        cut.Markup.Should().NotContain("Archived",
            "the per-row archived chip was deliberately retired (WU-MsgArchive) — the "
            + "Inbox|Archived tab split already conveys the state");
    }

    [Fact]
    public void ConversationListItem_ArchivedWithUnread_StillRendersUnreadBadge()
    {
        IRenderedComponent<ConversationListItem> cut = Render<ConversationListItem>(p => p
            .Add(c => c.Conversation, MakeConversation(unreadCount: 2, isArchived: true)));

        cut.Markup.Should().Contain("unread messages",
            "archiving mutes the global badge, not the per-conversation count — a reply to an "
            + "archived thread must stay discoverable inside the Archived tab");
    }

    // ── Link target ───────────────────────────────────────────────────────────────

    [Fact]
    public void ConversationListItem_RendersLinkToConversation()
    {
        IRenderedComponent<ConversationListItem> cut = Render<ConversationListItem>(p => p
            .Add(c => c.Conversation, MakeConversation()));

        IElement anchor = cut.Find("a");
        anchor.GetAttribute("href").Should().Contain("/messages/1",
            "the card must link to /messages/{ConversationId}");
    }
}
