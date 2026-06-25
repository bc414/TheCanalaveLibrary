using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="ConversationListItem"/> (WU35). Covers: unread count badge
/// renders when <c>UnreadCount &gt; 0</c> and is absent when zero; archived indicator
/// renders only when <c>IsArchived</c> is true; IsSelected changes the highlight class.
///
/// <b>Tier:</b> RazorComponents (bUnit, no host or DB).
/// </summary>
public class ConversationListItemTests : TestContext
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
        IRenderedComponent<ConversationListItem> cut = RenderComponent<ConversationListItem>(p => p
            .Add(c => c.Conversation, MakeConversation(unreadCount: 3)));

        // Badge has aria-label "N unread messages"
        cut.Markup.Should().Contain("unread messages",
            "unread badge must render when UnreadCount > 0");
        cut.Markup.Should().Contain("3");
    }

    [Fact]
    public void ConversationListItem_UnreadCountZero_NoBadge()
    {
        IRenderedComponent<ConversationListItem> cut = RenderComponent<ConversationListItem>(p => p
            .Add(c => c.Conversation, MakeConversation(unreadCount: 0)));

        cut.Markup.Should().NotContain("unread messages",
            "unread badge must not render when UnreadCount is 0");
    }

    // ── Archived indicator ────────────────────────────────────────────────────────

    [Fact]
    public void ConversationListItem_IsArchivedTrue_ShowsArchivedLabel()
    {
        IRenderedComponent<ConversationListItem> cut = RenderComponent<ConversationListItem>(p => p
            .Add(c => c.Conversation, MakeConversation(isArchived: true)));

        cut.Markup.Should().Contain("Archived",
            "an archived conversation must show the Archived indicator");
    }

    [Fact]
    public void ConversationListItem_IsArchivedFalse_NoArchivedLabel()
    {
        IRenderedComponent<ConversationListItem> cut = RenderComponent<ConversationListItem>(p => p
            .Add(c => c.Conversation, MakeConversation(isArchived: false)));

        cut.Markup.Should().NotContain("Archived",
            "a non-archived conversation must not show the Archived indicator");
    }

    // ── IsSelected highlight ──────────────────────────────────────────────────────

    [Fact]
    public void ConversationListItem_WhenSelected_HasPrimaryBorderClass()
    {
        IRenderedComponent<ConversationListItem> cut = RenderComponent<ConversationListItem>(p => p
            .Add(c => c.Conversation, MakeConversation())
            .Add(c => c.IsSelected, true));

        // Selected items use border-[--color-primary]
        cut.Markup.Should().Contain("border-[--color-primary]",
            "the selected item must have the primary-coloured border");
    }

    [Fact]
    public void ConversationListItem_WhenNotSelected_NoPrimaryBorderClass()
    {
        IRenderedComponent<ConversationListItem> cut = RenderComponent<ConversationListItem>(p => p
            .Add(c => c.Conversation, MakeConversation())
            .Add(c => c.IsSelected, false));

        cut.Markup.Should().NotContain("border-[--color-primary]",
            "an unselected item must not use the primary border class");
    }

    // ── Link target ───────────────────────────────────────────────────────────────

    [Fact]
    public void ConversationListItem_RendersLinkToConversation()
    {
        IRenderedComponent<ConversationListItem> cut = RenderComponent<ConversationListItem>(p => p
            .Add(c => c.Conversation, MakeConversation()));

        IElement anchor = cut.Find("a");
        anchor.GetAttribute("href").Should().Contain("/messages/1",
            "the card must link to /messages/{ConversationId}");
    }
}
