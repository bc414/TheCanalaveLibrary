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
/// the on-demand archived fetch, and the thread-header archive round trip.
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

    public MessagesPageTests()
    {
        Services.AddScoped<IMessagingWriteService>(_ => _fakeService);
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
}
