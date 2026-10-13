using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="MessagesPage"/> (F49 L3-Logic/L3.5, WU-MsgArchive, 2026-07-26) —
/// the first page-level coverage for the messaging cluster. Pins the Inbox|Archived tab split,
/// the on-demand archived fetch, the thread-header archive round trip, and the reply composer
/// emptying after a sent reply but not a refused one (WU-UserDeletion browser pass, 2026-10-13).
/// Semantic output and service-call correctness only — no CSS class assertions
/// (testing.md §"What belongs in RazorComponents").
/// <para>
/// <b>JS interop note:</b> the thread pane renders MessageComposer → EditorView (Quill.js), so
/// JSInterop.Mode is Loose, matching CommentSectionTests / BlogPostPageTests.
/// </para>
/// <b>Tier:</b> RazorComponents (bUnit, no host or DB).
/// </summary>
public class MessagesPageTests : BunitContext
{
    private readonly FakeMessagingWriteService _fakeService = new();
    private readonly FakeReportSubmissionService _reports = new();

    public MessagesPageTests()
    {
        Services.AddScoped<IMessagingWriteService>(_ => _fakeService);
        // The page hosts a ReportDialog for message reports (tracker B17).
        Services.AddScoped<IReportSubmissionService>(_ => _reports);
        JSInterop.Mode = JSRuntimeMode.Loose;
        this.AddAuthorization().SetAuthorized("TestUser");
    }

    // The archived flag rides beside the DTO (fake-store shape) — ConversationSummaryDto itself
    // deliberately carries no IsArchived; scope implies it (see the DTO's doc comment).
    private static (ConversationSummaryDto Dto, bool Archived) NewConversation(
        int id, string username, bool isArchived = false, int unreadCount = 0) => (new ConversationSummaryDto(
            ConversationId: id,
            Subject: $"Subject {id}",
            OtherParticipant: new MessagingParticipantDto(
                UserId: id + 100, Username: username, AvatarUrl: "/img/default-avatar.svg"),
            LastMessagePreview: "preview text",
            LastMessageDate: new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc),
            UnreadCount: unreadCount), isArchived);

    private static ConversationThreadDto NewThread(int id, bool isArchived) => new(
        ConversationId: id,
        Subject: $"Subject {id}",
        OtherParticipant: new MessagingParticipantDto(
            UserId: id + 100, Username: "Ash", AvatarUrl: "/img/default-avatar.svg"),
        Messages: [],
        TotalMessageCount: 0,
        IsArchived: isArchived);

    // Button text isn't a CSS selector — locate by exact TextContent rather than a brittle
    // :contains() compound selector (testing.md; same idiom as GroupFolderManagementPageTests).
    private static IElement FindButton(IRenderedComponent<MessagesPage> cut, string text) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == text);

    // ── Tab split ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Inbox_IsDefaultTab_AndShowsOnlyNonArchivedConversations()
    {
        _fakeService.SetConversations(
            NewConversation(1, "Ash"),
            NewConversation(2, "Misty"),
            NewConversation(3, "Brock", isArchived: true));

        IRenderedComponent<MessagesPage> cut = Render<MessagesPage>();

        cut.Markup.Should().Contain("Ash").And.Contain("Misty");
        cut.Markup.Should().NotContain("Brock",
            "the default Inbox tab must exclude archived conversations");
    }

    [Fact]
    public void InitialLoad_DoesNotFetchArchived()
    {
        _fakeService.SetConversations(NewConversation(1, "Ash"));

        Render<MessagesPage>();

        _fakeService.GetConversationsCalls.Should().NotContain(ConversationScope.Archived,
            "the archived set grows without bound — it must not ride along on every page load");
    }

    [Fact]
    public void ArchivedTab_FetchesOnDemand_AndShowsOnlyArchivedConversations()
    {
        _fakeService.SetConversations(
            NewConversation(1, "Ash"),
            NewConversation(3, "Brock", isArchived: true));

        IRenderedComponent<MessagesPage> cut = Render<MessagesPage>();
        FindButton(cut, "Archived").Click();

        _fakeService.GetConversationsCalls.Should().Contain(ConversationScope.Archived,
            "opening the Archived tab is what triggers the archived-scope read");
        cut.Markup.Should().Contain("Brock");
        cut.Markup.Should().NotContain("Ash",
            "the Archived tab must show archived conversations only, not the merged set");
    }

    [Fact]
    public void ArchivedConversationWithUnread_StillRendersUnreadBadge()
    {
        _fakeService.SetConversations(
            NewConversation(3, "Brock", isArchived: true, unreadCount: 2));

        IRenderedComponent<MessagesPage> cut = Render<MessagesPage>();
        FindButton(cut, "Archived").Click();

        cut.Markup.Should().Contain("unread messages",
            "archiving mutes the global badge, not the per-conversation count — a reply to an "
            + "archived thread must remain discoverable (layer2-services.md §\"Sticky\")");
    }

    [Fact]
    public void ArchivedTab_WhenEmpty_ShowsArchivedSpecificEmptyCopy()
    {
        _fakeService.SetConversations(NewConversation(1, "Ash"));

        IRenderedComponent<MessagesPage> cut = Render<MessagesPage>();
        FindButton(cut, "Archived").Click();

        cut.Markup.Should().Contain("No archived conversations.");
    }

    /// <summary>
    /// Regression: opening an archived thread marks it read server-side, but the sidebar renders
    /// _archivedConversations while the Archived tab is active — so LoadThreadAsync must refresh
    /// that list too, or the just-read thread's unread badge sits stale until a tab toggle.
    /// (Found in post-WU review, 2026-07-26; missed by the original browser pass.)
    /// </summary>
    [Fact]
    public void OpeningArchivedThread_FromArchivedTab_ClearsItsSidebarUnreadBadge()
    {
        _fakeService.SetConversations(
            NewConversation(3, "Brock", isArchived: true, unreadCount: 2));
        _fakeService.SetThread(NewThread(3, isArchived: true));

        IRenderedComponent<MessagesPage> cut = Render<MessagesPage>();
        FindButton(cut, "Archived").Click();
        cut.Markup.Should().Contain("unread messages", "precondition: badge visible before opening");

        // Simulate clicking the conversation — same-page navigation delivers a new ConversationId
        // (bUnit v2 re-sets parameters on the same instance via cut.Render, per SeriesCreateEditPageTests).
        cut.Render(p => p.Add(c => c.ConversationId, 3));

        _fakeService.MarkReadCalls.Should().Contain(3);
        cut.Markup.Should().NotContain("unread messages",
            "the archived sidebar list must be refreshed after mark-read, not left stale");
    }

    // ── Archive round trip ────────────────────────────────────────────────────────

    [Fact]
    public void ArchiveButton_OnOpenThread_SetsArchivedAndNavigatesAway()
    {
        _fakeService.SetConversations(NewConversation(1, "Ash"));
        _fakeService.SetThread(NewThread(1, isArchived: false));

        IRenderedComponent<MessagesPage> cut = Render<MessagesPage>(p => p
            .Add(c => c.ConversationId, 1));

        FindButton(cut, "Archive").Click();

        _fakeService.SetArchivedCalls.Should().ContainSingle()
            .Which.Should().Be((1, true));

        Services.GetRequiredService<NavigationManager>().Uri
            .Should().EndWith("/messages",
                "archiving clears the pane and returns to the conversation-picker state");
    }

    // ── A full load's restored list (WU-PrivateMessageEmail browser pass, 2026-10-13) ────────────
    // A full page load restores Conversations from the prerender snapshot, and .NET 10 hands that
    // snapshot back on the page's first parameter set after initialization — the archive's return to
    // /messages. The page used to show the archived conversation in the Inbox until a reload, on the
    // circuit and on WASM (layer3-logic.md §"State Persistence").

    [Fact]
    public async Task RestoredFirstLoad_RendersTheSnapshot_WithoutFetchingTheInbox()
    {
        _fakeService.SetConversations(NewConversation(1, "Ash"));
        await this.RestoreDeclarativeStateAsync<MessagesPage>(
            ("Conversations", new[] { NewConversation(2, "Misty").Dto }));

        IRenderedComponent<MessagesPage> cut = Render<MessagesPage>();

        cut.Markup.Should().Contain("Misty", "the restored snapshot is what the interactive pass renders");
        _fakeService.GetConversationsCalls.Should().BeEmpty(
            "a restored list is not fetched again — this pins that the restore in these tests is real");
    }

    [Fact]
    public async Task Archive_AfterARestoredFirstLoad_LeavesTheConversationOutOfTheInbox()
    {
        var conversation = NewConversation(1, "Ash");
        _fakeService.SetConversations(conversation);
        _fakeService.SetThread(NewThread(1, isArchived: false));
        await this.RestoreDeclarativeStateAsync<MessagesPage>(("Conversations", new[] { conversation.Dto }));

        IRenderedComponent<MessagesPage> cut = Render<MessagesPage>(p => p
            .Add(c => c.ConversationId, 1));
        FindButton(cut, "Archive").Click();

        // The router's parameter set for /messages, which is what the handler's NavigateTo leads to.
        cut.Render(p => p.Add(c => c.ConversationId, (int?)null));

        cut.Markup.Should().Contain("Your inbox is empty.",
            "the archived conversation left the inbox; the restored snapshot must not put it back");
        cut.Markup.Should().NotContain("Ash");
    }

    [Fact]
    public void ArchivedThread_ButtonReadsUnarchive_AndClearsTheFlag()
    {
        _fakeService.SetConversations(NewConversation(1, "Ash", isArchived: true));
        _fakeService.SetThread(NewThread(1, isArchived: true));

        IRenderedComponent<MessagesPage> cut = Render<MessagesPage>(p => p
            .Add(c => c.ConversationId, 1));

        FindButton(cut, "Unarchive").Click();

        _fakeService.SetArchivedCalls.Should().ContainSingle()
            .Which.Should().Be((1, false));
    }

    [Fact]
    public void NoThreadSelected_RendersNoArchiveButton()
    {
        _fakeService.SetConversations(NewConversation(1, "Ash"));

        IRenderedComponent<MessagesPage> cut = Render<MessagesPage>();

        cut.FindAll("button").Should().NotContain(b => b.TextContent.Trim() == "Archive",
            "the archive control belongs to an open thread, not the empty pane");
    }

    // ── Reply composer reset (WU-UserDeletion browser pass, 2026-10-13) ───────────────

    // Quill keeps its content across renders, so the only way the reply box empties is an explicit
    // load of "" through EditorView.SetHtmlAsync (Blazored's loadQuillHTMLContent).
    private int ComposerClears() => JSInterop.Invocations
        .Count(i => i.Identifier == "QuillFunctions.loadQuillHTMLContent"
                    && i.Arguments.Count > 1 && Equals(i.Arguments[1], string.Empty));

    [Fact]
    public void SendReply_WhenSent_EmptiesTheReplyComposer()
    {
        _fakeService.SetConversations(NewConversation(1, "Ash"));
        _fakeService.SetThread(NewThread(1, isArchived: false));
        JSInterop.Setup<string>("QuillFunctions.getQuillHTML", _ => true).SetResult("<p>See you there.</p>");

        IRenderedComponent<MessagesPage> cut = Render<MessagesPage>(p => p
            .Add(c => c.ConversationId, 1));
        int before = ComposerClears();

        FindButton(cut, "Send Reply").Click();

        _fakeService.SendMessageCalls.Should().ContainSingle()
            .Which.Should().Be((1, "<p>See you there.</p>"));
        ComposerClears().Should().Be(before + 1,
            "a sent reply must leave the box empty: the text used to stay and a second click sent it again");
    }

    [Fact]
    public void SendReply_WhenRefused_KeepsTheText()
    {
        _fakeService.SetConversations(NewConversation(1, "Ash"));
        _fakeService.SetThread(NewThread(1, isArchived: false));
        _fakeService.SendMessageException = new MessagingValidationException(["Message is too long."]);
        JSInterop.Setup<string>("QuillFunctions.getQuillHTML", _ => true).SetResult("<p>Too long.</p>");

        IRenderedComponent<MessagesPage> cut = Render<MessagesPage>(p => p
            .Add(c => c.ConversationId, 1));
        int before = ComposerClears();

        FindButton(cut, "Send Reply").Click();

        cut.Markup.Should().Contain("Message is too long.");
        ComposerClears().Should().Be(before,
            "a refused message keeps its text so the sender can fix it and send again");
    }

    // ── Report a message (tracker B17) ────────────────────────────────────────────

    [Fact]
    public void ReportOnTheOtherPartysMessage_OpensThePagesReportDialog()
    {
        _reports.Reasons = [new ReportReasonDto(1, "Harassment", null)];
        MessageDto theirs = new(MessageId: 41, ConversationId: 1, SenderUserId: 101, SenderUsername: "Ash",
            SenderAvatarUrl: "/img/default-avatar.svg", MessageText: "<p>hi</p>",
            DateSent: DateTime.UtcNow, IsOwnMessage: false);
        _fakeService.SetConversations(NewConversation(1, "Ash"));
        _fakeService.SetThread(NewThread(1, isArchived: false) with { Messages = [theirs], TotalMessageCount = 1 });

        IRenderedComponent<MessagesPage> cut = Render<MessagesPage>(p => p
            .Add(c => c.ConversationId, 1));

        cut.Find("button[aria-label='Report this message']").Click();

        cut.Find("[role='dialog'][aria-label='Report Content']").Should().NotBeNull();
        cut.Markup.Should().Contain("Harassment", "the dialog loaded the report reasons");
    }
}
