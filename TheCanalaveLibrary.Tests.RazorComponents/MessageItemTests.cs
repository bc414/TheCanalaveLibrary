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
public class MessageItemTests : TestContext
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

    private static MessageDto OtherMessage(string html = "<p>Their text</p>") =>
        new(MessageId: 2,
            ConversationId: 10,
            SenderUserId: 99,
            SenderUsername: "Misty",
            SenderAvatarUrl: "/img/default-avatar.svg",
            MessageText: html,
            DateSent: DateTime.UtcNow,
            IsOwnMessage: false);

    // ── Own message alignment ─────────────────────────────────────────────────────

    [Fact]
    public void MessageItem_OwnMessage_UsesFlexRowReverse()
    {
        IRenderedComponent<MessageItem> cut = RenderComponent<MessageItem>(p => p
            .Add(c => c.Message, OwnMessage()));

        // Outer flex container should use flex-row-reverse for right-side bubble alignment.
        cut.Markup.Should().Contain("flex-row-reverse",
            "own messages must use flex-row-reverse for right-side alignment");
    }

    [Fact]
    public void MessageItem_OwnMessage_DoesNotShowSenderUsername()
    {
        IRenderedComponent<MessageItem> cut = RenderComponent<MessageItem>(p => p
            .Add(c => c.Message, OwnMessage()));

        // Own messages don't repeat the username (it's you).
        cut.Markup.Should().NotContain("Me",
            "own messages must not display the sender username label");
    }

    // ── Other-participant message alignment ───────────────────────────────────────

    [Fact]
    public void MessageItem_OtherMessage_DoesNotUseFlexRowReverse()
    {
        IRenderedComponent<MessageItem> cut = RenderComponent<MessageItem>(p => p
            .Add(c => c.Message, OtherMessage()));

        cut.Markup.Should().NotContain("flex-row-reverse",
            "other-participant messages must not use flex-row-reverse");
    }

    [Fact]
    public void MessageItem_OtherMessage_ShowsSenderUsername()
    {
        IRenderedComponent<MessageItem> cut = RenderComponent<MessageItem>(p => p
            .Add(c => c.Message, OtherMessage()));

        cut.Markup.Should().Contain("Misty",
            "other-participant messages must display the sender's username");
    }

    // ── Content (RichTextView) ────────────────────────────────────────────────────

    [Fact]
    public void MessageItem_RendersStoredHtmlViaRichTextView()
    {
        IRenderedComponent<MessageItem> cut = RenderComponent<MessageItem>(p => p
            .Add(c => c.Message, OwnMessage("<p>Great chapter!</p>")));

        // RichTextView renders the MarkupString inside a div; the text should appear in DOM.
        cut.Markup.Should().Contain("Great chapter!",
            "MessageItem must render the stored message HTML via RichTextView");
    }

    [Fact]
    public void MessageItem_OtherMessage_RendersStoredHtml()
    {
        IRenderedComponent<MessageItem> cut = RenderComponent<MessageItem>(p => p
            .Add(c => c.Message, OtherMessage("<p>Looking forward to it!</p>")));

        cut.Markup.Should().Contain("Looking forward to it!",
            "other-participant messages must also render HTML via RichTextView");
    }
}
