using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="MessageThread"/>'s archive control (WU-MsgArchive, 2026-07-26).
/// MessageThread is injection-free — the owning page holds the service — so these need no fake.
/// Pins: the label flips on <c>IsArchived</c>, the callback fires, the button is absent without a
/// delegate, and Busy disables it.
/// <para>
/// <b>JS interop note:</b> the reply composer renders EditorView (Quill.js), so JSInterop.Mode is
/// Loose, matching CommentItemTests.
/// </para>
/// <b>Tier:</b> RazorComponents (bUnit, no host or DB).
/// </summary>
public class MessageThreadTests : BunitContext
{
    public MessageThreadTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static ConversationThreadDto NewThread(bool isArchived) => new(
        ConversationId: 1,
        Subject: "Re: your chapter 4",
        OtherParticipant: new MessagingParticipantDto(
            UserId: 2, Username: "Ash", AvatarUrl: "/img/default-avatar.svg"),
        Messages: [],
        TotalMessageCount: 0,
        IsArchived: isArchived);

    private static IElement? TryFindButton(IRenderedComponent<MessageThread> cut, string text) =>
        cut.FindAll("button").FirstOrDefault(b => b.TextContent.Trim() == text);

    [Theory]
    [InlineData(false, "Archive")]
    [InlineData(true, "Unarchive")]
    public void ArchiveButton_LabelReflectsIsArchived(bool isArchived, string expectedLabel)
    {
        IRenderedComponent<MessageThread> cut = Render<MessageThread>(p => p
            .Add(c => c.Thread, NewThread(isArchived))
            .Add(c => c.IsArchived, isArchived)
            .Add(c => c.OnToggleArchive, () => { }));

        TryFindButton(cut, expectedLabel).Should().NotBeNull(
            $"an archived={isArchived} thread must offer \"{expectedLabel}\"");
    }

    [Fact]
    public void ArchiveButton_Click_InvokesCallback()
    {
        bool fired = false;

        IRenderedComponent<MessageThread> cut = Render<MessageThread>(p => p
            .Add(c => c.Thread, NewThread(isArchived: false))
            .Add(c => c.OnToggleArchive, () => fired = true));

        TryFindButton(cut, "Archive")!.Click();

        fired.Should().BeTrue();
    }

    [Fact]
    public void ArchiveButton_NoDelegate_NotRendered()
    {
        IRenderedComponent<MessageThread> cut = Render<MessageThread>(p => p
            .Add(c => c.Thread, NewThread(isArchived: false)));

        TryFindButton(cut, "Archive").Should().BeNull(
            "the control is optional — it stays dark unless the owner wires it (HasDelegate idiom)");
    }

    [Fact]
    public void ArchiveButton_BusyArchiving_IsDisabled()
    {
        IRenderedComponent<MessageThread> cut = Render<MessageThread>(p => p
            .Add(c => c.Thread, NewThread(isArchived: false))
            .Add(c => c.OnToggleArchive, () => { })
            .Add(c => c.BusyArchiving, true));

        TryFindButton(cut, "Archive")!.HasAttribute("disabled").Should().BeTrue(
            "the button must not accept a second click while the write is in flight");
    }

    [Fact]
    public void NoThread_RendersPlaceholderAndNoArchiveButton()
    {
        IRenderedComponent<MessageThread> cut = Render<MessageThread>(p => p
            .Add(c => c.OnToggleArchive, () => { }));

        cut.Markup.Should().Contain("Select a conversation");
        TryFindButton(cut, "Archive").Should().BeNull();
    }
}
