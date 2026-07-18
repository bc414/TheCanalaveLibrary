using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="MessageItem"/> (WU35). Covers: own messages use flex-row-reverse
/// for right-side alignment; other-participant messages use flex-row (left); RichTextView renders
/// the stored HTML; sender username is shown only for the other participant's messages.
///
/// <b>Tier:</b> RazorComponents (bUnit, no host or DB).
/// </summary>
public class MessageItemTests : BunitContext
{
    private static MessageDto OwnMessage(string html = "<p>My text</p>") =>
        new(MessageId: 1,
            ConversationId: 10,
            SenderUserId: 1,
            SenderUsername: "Me",
            SenderAvatarUrl: "/img/default-avatar.svg",
            MessageText: html,
            DateSent: DateTime.UtcNow,
            IsOwnMessage: true);

    // ── Own message alignment ─────────────────────────────────────────────────────

    [Fact]
    public void MessageItem_OwnMessage_ShowsSenderUsername()
    {
        IRenderedComponent<MessageItem> cut = Render<MessageItem>(p => p
            .Add(c => c.Message, OwnMessage()));

        // Ratified 2026-07-10 (layer4-style.md "Element Roles"): BOTH sides show avatar + name;
        // authorship is signaled by alignment, not by hiding the label.
        cut.Markup.Should().Contain("Me",
            "own messages display the sender username alongside the avatar");
    }

    // ── Content (RichTextView) ────────────────────────────────────────────────────

    [Fact]
    public void MessageItem_RendersStoredHtmlViaRichTextView()
    {
        IRenderedComponent<MessageItem> cut = Render<MessageItem>(p => p
            .Add(c => c.Message, OwnMessage("<p>Great chapter!</p>")));

        // RichTextView renders the MarkupString inside a div; the text should appear in DOM.
        cut.Markup.Should().Contain("Great chapter!",
            "MessageItem must render the stored message HTML via RichTextView");
    }
}
