using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="IMessagingWriteService"/> (WU35, Feature 49).
///
/// <b>Covered:</b> StartConversation (creates correct rows); validation guards (empty subject,
/// self-message); AllowPrivateMessages gate (UsersOnly allows, Nobody blocks, Following requires
/// a follow edge); SendMessage (appends + non-participant guard + sanitize-once-on-save);
/// unread count watermark (own messages excluded, MarkConversationReadAsync clears it);
/// archive toggle.
///
/// <b>Added WU-MsgArchive (2026-07-26):</b> inbox ordering (newest-first, message-less LAST — the
/// guard on the C#→SQL sort move), <c>includeArchived</c> filtering, and the sticky-archive
/// invariant that an archived conversation keeps its per-conversation unread count while dropping
/// out of the global badge.
///
/// <b>Seeding:</b> each test class call in InitializeAsync seeds a sender (_senderId) and a
/// recipient (_recipientId); per-test variants seed additional users inline.
/// FK parent rows: no stories are needed; Conversation + ConversationParticipant + PrivateMessage
/// rows are created exclusively through <see cref="IMessagingWriteService"/>.
///
/// Tier: Integration (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class MessagingWriteServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _senderId;
    private int _recipientId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _senderId = await SeedUserAsync("Sender");
        _recipientId = await SeedUserAsync("Recipient");
        SetActiveUser(_senderId);
    }

    // ── StartConversationAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task StartConversation_CreatesConversationPlusTwoParticipantsPlusFirstMessage()
    {
        int convId = await CallStartAsync("Hello", "<p>Hi there!</p>");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Conversation? conv = await db.Conversations.FindAsync(convId);
        conv.Should().NotBeNull();
        conv!.Subject.Should().Be("Hello");

        int participantCount = await db.ConversationParticipants
            .CountAsync(p => p.ConversationId == convId);
        participantCount.Should().Be(2, "sender and recipient are both participants");

        int messageCount = await db.PrivateMessages
            .CountAsync(m => m.ConversationId == convId);
        messageCount.Should().Be(1, "first message is inserted on StartConversation");
    }

    [Fact]
    public async Task StartConversation_ReturnsNewConversationId()
    {
        int convId = await CallStartAsync("Re: Fic Recs", "<p>Have you read X?</p>");
        convId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task StartConversation_SelfMessage_ThrowsValidationException()
    {
        // Sender tries to message themselves — _senderId is already the active user.
        Func<Task> act = async () => await CallStartAsync(
            "Self-talk", "<p>Just me.</p>", recipientId: _senderId);

        await act.Should().ThrowAsync<MessagingValidationException>();
    }

    [Fact]
    public async Task StartConversation_EmptySubject_ThrowsValidationException()
    {
        Func<Task> act = async () => await CallStartAsync(
            subject: "", messageHtml: "<p>Hello</p>");

        await act.Should().ThrowAsync<MessagingValidationException>();
    }

    [Fact]
    public async Task StartConversation_EmptyBody_ThrowsValidationException()
    {
        Func<Task> act = async () => await CallStartAsync(
            subject: "Hi", messageHtml: "");

        await act.Should().ThrowAsync<MessagingValidationException>();
    }

    // ── AllowPrivateMessages gate ─────────────────────────────────────────────────

    [Fact]
    public async Task StartConversation_UsersOnly_Allows()
    {
        // Default is UsersOnly — any authenticated user may message.
        // (SeedUserAsync leaves the default PrivacySettings.AllowPrivateMessages = UsersOnly.)
        Func<Task> act = async () => await CallStartAsync("Hello", "<p>Test</p>");
        await act.Should().NotThrowAsync<MessagingPermissionException>();
    }

    [Fact]
    public async Task StartConversation_Nobody_ThrowsPermissionException()
    {
        await SetRecipientPrivacyAsync(_recipientId, SocialInteractionPermission.Nobody);

        Func<Task> act = async () => await CallStartAsync("Hello", "<p>Test</p>");
        await act.Should().ThrowAsync<MessagingPermissionException>();
    }

    [Fact]
    public async Task StartConversation_FollowingGate_ThrowsWhenRecipientDoesNotFollowSender()
    {
        await SetRecipientPrivacyAsync(_recipientId, SocialInteractionPermission.Following);
        // No follow edge seeded — recipient does not follow sender.

        Func<Task> act = async () => await CallStartAsync("Hello", "<p>Test</p>");
        await act.Should().ThrowAsync<MessagingPermissionException>();
    }

    [Fact]
    public async Task StartConversation_FollowingGate_AllowsWhenRecipientFollowsSender()
    {
        await SetRecipientPrivacyAsync(_recipientId, SocialInteractionPermission.Following);
        // Seed the follow edge: recipient follows sender.
        await SeedFollowAsync(followerId: _recipientId, followedId: _senderId);

        Func<Task> act = async () => await CallStartAsync("Hello", "<p>Test</p>");
        await act.Should().NotThrowAsync();
    }

    // ── SendMessageAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SendMessage_AppendsMessageToThread()
    {
        int convId = await CallStartAsync("Subject", "<p>First message</p>");

        MessageDto sent = await CallSendAsync(convId, "<p>Reply here</p>");

        sent.ConversationId.Should().Be(convId);
        sent.IsOwnMessage.Should().BeTrue();

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        int count = await db.PrivateMessages.CountAsync(m => m.ConversationId == convId);
        count.Should().Be(2, "start creates 1 message; send appends 1 more");
    }

    [Fact]
    public async Task SendMessage_NonParticipant_ThrowsKeyNotFoundException()
    {
        int convId = await CallStartAsync("Subject", "<p>First</p>");

        // Third user — not a participant in this conversation.
        int outsider = await SeedUserAsync("Outsider");
        SetActiveUser(outsider);

        Func<Task> act = async () => await CallSendAsync(convId, "<p>Hack attempt</p>");
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SendMessage_ScriptTag_IsStrippedBySanitizer()
    {
        int convId = await CallStartAsync("Subject", "<p>Hello</p>");

        MessageDto sent = await CallSendAsync(convId, "<p>Text</p><script>alert('xss')</script>");

        sent.MessageText.Should().NotContain("<script>");
        sent.MessageText.Should().Contain("Text");
    }

    // ── Unread watermark ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUnreadCount_OwnMessages_AreNotCountedAsUnread()
    {
        // Sender starts and sends — their own messages should not generate unread for themselves.
        await CallStartAsync("Subject", "<p>My own first message</p>");

        int count = await CallGetUnreadCountAsync();
        count.Should().Be(0, "own messages never count towards the sender's unread total");
    }

    [Fact]
    public async Task GetUnreadCount_RecipientSeesUnreadAfterReceivingMessage()
    {
        int convId = await CallStartAsync("Subject", "<p>Message from sender</p>");

        // Switch to recipient — they received a message and haven't read it.
        SetActiveUser(_recipientId);

        int count = await CallGetUnreadCountAsync();
        count.Should().Be(1, "recipient has one unread conversation");
    }

    [Fact]
    public async Task MarkConversationReadAsync_ClearsUnreadCountForViewer()
    {
        int convId = await CallStartAsync("Subject", "<p>Message</p>");

        SetActiveUser(_recipientId);
        // Verify unread is non-zero before marking read.
        int before = await CallGetUnreadCountAsync();
        before.Should().Be(1);

        await CallMarkReadAsync(convId);

        int after = await CallGetUnreadCountAsync();
        after.Should().Be(0, "MarkConversationReadAsync advances LastReadTimestamp past all messages");
    }

    // ── SetArchivedAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SetArchivedAsync_TogglesIsArchivedOnSenderParticipantRow()
    {
        int convId = await CallStartAsync("Subject", "<p>First</p>");

        await CallSetArchivedAsync(convId, archived: true);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ConversationParticipant? row = await db.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == convId && p.UserId == _senderId);

        row.Should().NotBeNull();
        row!.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task SetArchivedAsync_NonParticipant_ThrowsKeyNotFoundException()
    {
        int convId = await CallStartAsync("Subject", "<p>First</p>");

        int outsider = await SeedUserAsync("Outsider");
        SetActiveUser(outsider);

        Func<Task> act = async () => await CallSetArchivedAsync(convId, archived: true);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── GetConversationsAsync — ordering + archived filtering ─────────────────────

    /// <summary>
    /// Regression guard for the 2026-07-26 (WU-MsgArchive) move of the inbox sort from C# into
    /// SQL. The contract is newest-message-first with message-less conversations LAST. Postgres
    /// defaults to NULLS FIRST for ORDER BY ... DESC, so a single-key sort would silently promote
    /// the message-less conversation to the top — this test is what catches that.
    /// </summary>
    [Fact]
    public async Task GetConversations_OrdersNewestFirst_WithMessagelessConversationsLast()
    {
        int convOlder = await CallStartAsync("Older", "<p>a</p>");
        int convNewer = await CallStartAsync("Newer", "<p>b</p>");

        int convEmpty;
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Pin the timestamps rather than relying on wall-clock ordering between two rapid
            // service calls — the assertion must not race on clock resolution.
            PrivateMessage older = await db.PrivateMessages
                .FirstAsync(m => m.ConversationId == convOlder);
            older.DateSent = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            PrivateMessage newer = await db.PrivateMessages
                .FirstAsync(m => m.ConversationId == convNewer);
            newer.DateSent = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

            // A conversation with zero messages can't be produced through the service
            // (StartConversationAsync always writes a first message), so seed it inline.
            // FK parents: Conversation is parentless; both participant rows point at users
            // seeded in InitializeAsync.
            Conversation empty = new()
            {
                Subject = "No messages yet",
                DateCreated = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
            };
            db.Conversations.Add(empty);
            await db.SaveChangesAsync();

            convEmpty = empty.ConversationId;
            db.ConversationParticipants.AddRange(
                new ConversationParticipant { ConversationId = convEmpty, UserId = _senderId },
                new ConversationParticipant { ConversationId = convEmpty, UserId = _recipientId });
            await db.SaveChangesAsync();
        }

        IReadOnlyList<ConversationSummaryDto> list = await CallGetConversationsAsync();

        list.Select(c => c.ConversationId).Should().Equal(
            [convNewer, convOlder, convEmpty],
            "newest message first, and a conversation with no messages sorts last — not first");
    }

    [Fact]
    public async Task GetConversations_ScopesAreDisjoint_ActiveAndArchivedSplitTheSet()
    {
        int convActive = await CallStartAsync("Active", "<p>a</p>");
        int convArchived = await CallStartAsync("Archived", "<p>b</p>");
        await CallSetArchivedAsync(convArchived, archived: true);

        IReadOnlyList<ConversationSummaryDto> inbox = await CallGetConversationsAsync();
        inbox.Select(c => c.ConversationId).Should().Equal([convActive],
            "the default (Active) scope hides archived conversations");

        IReadOnlyList<ConversationSummaryDto> archived =
            await CallGetConversationsAsync(ConversationScope.Archived);
        archived.Select(c => c.ConversationId).Should().Equal([convArchived],
            "the Archived scope returns archived rows ONLY — never the merged set (this is what "
            + "backs the Archived tab without over-fetching the inbox)");
    }

    /// <summary>
    /// Pins the ID-first read path's bounded preview prefix (WU-MsgReadPath): the hydration step
    /// fetches SUBSTRING(message_text, 1, 2048), not the whole body — this test both exercises
    /// the Substring SQL translation against real Postgres (an untranslatable expression would
    /// throw here) and asserts the preview contract survives a body far larger than the prefix.
    /// </summary>
    [Fact]
    public async Task GetConversations_PreviewIsBounded_ForVeryLongMessages()
    {
        // ~9 KB body: 1500 six-char words. Well past the 2048-char fetch prefix.
        string longBody = "<p>" + string.Concat(Enumerable.Repeat("wordy ", 1500)) + "</p>";
        await CallStartAsync("Long", longBody);

        IReadOnlyList<ConversationSummaryDto> list = await CallGetConversationsAsync();

        string? preview = list.Single().LastMessagePreview;
        preview.Should().NotBeNull();
        preview!.Length.Should().BeLessThanOrEqualTo(101,
            "the preview contract is ≤100 plain-text chars plus the ellipsis");
        preview.Should().StartWith("wordy wordy").And.EndWith("…");
    }

    /// <summary>
    /// The 2048-char fetch prefix can bisect an HTML tag, leaving a fragment like
    /// <c>&lt;a hre</c> at the end. MakePreview drops an unterminated trailing fragment; without
    /// that guard the stripped text would leak raw markup into the listing. This test lands the
    /// cut inside a long anchor tag deliberately — the WU-MsgReadPath long-message test cuts
    /// mid-word and never exercises this branch.
    /// </summary>
    [Fact]
    public async Task GetConversations_Preview_DropsTagFragmentLeftByPrefixTruncation()
    {
        // Short visible text, then an anchor whose href alone runs past the 2048-char prefix, so
        // the cut necessarily lands inside the tag rather than between tags.
        string body = "<p>Visible lead text.</p><p><a href=\"https://example.com/"
                      + new string('x', 2500) + "\">link</a></p>";
        await CallStartAsync("Bisected", body);

        IReadOnlyList<ConversationSummaryDto> list = await CallGetConversationsAsync();

        string? preview = list.Single().LastMessagePreview;
        preview.Should().NotBeNull();
        preview.Should().StartWith("Visible lead text.");
        preview.Should().NotContain("<", "an unterminated tag fragment must never reach the preview");
        preview.Should().NotContain("href", "raw attribute text must never reach the preview");
    }

    /// <summary>
    /// Documents an accepted consequence of the bounded prefix (WU-MsgReadPath): plain-text yield
    /// depends on markup density, so a link-dense body can produce a preview shorter than the
    /// 100-char cap even though the full message has far more text. Deliberate trade — a listing
    /// excerpt is not a contract to always fill 100 chars. Pinned so the behavior is a recorded
    /// decision rather than a surprise if someone later measures preview lengths.
    /// </summary>
    [Fact]
    public async Task GetConversations_Preview_MayBeShorterThanCap_WhenMarkupDense()
    {
        // ~40 links, each ~60 raw chars yielding 2 plain chars ("Aa" + space).
        string body = "<p>" + string.Concat(Enumerable.Range(0, 40).Select(i =>
            $"<a href=\"https://example.com/some/fairly/long/path/segment/{i:D4}\">Aa</a> ")) + "</p>";
        await CallStartAsync("Dense", body);

        IReadOnlyList<ConversationSummaryDto> list = await CallGetConversationsAsync();

        string? preview = list.Single().LastMessagePreview;
        preview.Should().NotBeNull();
        preview.Should().NotContain("http", "hrefs are stripped, not previewed");
        preview!.Trim().Should().StartWith("Aa");
        // The point of the test: markup density, not message length, bounds the yield.
        preview.Length.Should().BeLessThan(101);
    }

    /// <summary>
    /// Archiving mutes the global badge but must NOT suppress the per-conversation unread count —
    /// that count is what keeps a reply to an archived thread discoverable inside the Archived
    /// tab. See layer2-services.md §"Conversation Archiving Is Sticky".
    /// </summary>
    [Fact]
    public async Task ArchivedConversation_KeepsUnreadCount_ButIsExcludedFromGlobalBadge()
    {
        int convId = await CallStartAsync("Subject", "<p>First</p>");

        // Recipient archives, then the sender replies — the archived side must still see it.
        SetActiveUser(_recipientId);
        await CallSetArchivedAsync(convId, archived: true);

        SetActiveUser(_senderId);
        await CallSendAsync(convId, "<p>Are you still there?</p>");

        SetActiveUser(_recipientId);

        int badge = await CallGetUnreadCountAsync();
        badge.Should().Be(0, "archived conversations are muted from the global unread badge");

        IReadOnlyList<ConversationSummaryDto> archived =
            await CallGetConversationsAsync(ConversationScope.Archived);
        archived.Single(c => c.ConversationId == convId).UnreadCount
            .Should().BeGreaterThan(0,
                "the per-conversation count survives archiving — a new message must not vanish "
                + "silently just because the thread was archived");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<ConversationSummaryDto>> CallGetConversationsAsync(
        ConversationScope conversationScope = ConversationScope.Active)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IMessagingWriteService svc = scope.ServiceProvider.GetRequiredService<IMessagingWriteService>();
        return await svc.GetConversationsAsync(conversationScope);
    }

    private async Task<int> CallStartAsync(
        string subject, string messageHtml, int? recipientId = null)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IMessagingWriteService svc = scope.ServiceProvider.GetRequiredService<IMessagingWriteService>();
        return await svc.StartConversationAsync(
            new StartConversationDto(recipientId ?? _recipientId, subject, messageHtml));
    }

    private async Task<MessageDto> CallSendAsync(int conversationId, string messageHtml)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IMessagingWriteService svc = scope.ServiceProvider.GetRequiredService<IMessagingWriteService>();
        return await svc.SendMessageAsync(conversationId, messageHtml);
    }

    private async Task CallMarkReadAsync(int conversationId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IMessagingWriteService svc = scope.ServiceProvider.GetRequiredService<IMessagingWriteService>();
        await svc.MarkConversationReadAsync(conversationId);
    }

    private async Task CallSetArchivedAsync(int conversationId, bool archived)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IMessagingWriteService svc = scope.ServiceProvider.GetRequiredService<IMessagingWriteService>();
        await svc.SetArchivedAsync(conversationId, archived);
    }

    private async Task<int> CallGetUnreadCountAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IMessagingWriteService svc = scope.ServiceProvider.GetRequiredService<IMessagingWriteService>();
        return await svc.GetUnreadConversationCountAsync();
    }

    /// <summary>
    /// Updates <paramref name="userId"/>'s <c>PrivacySettings.AllowPrivateMessages</c> to
    /// <paramref name="permission"/> via <see cref="ApplicationDbContext"/>.
    /// </summary>
    private async Task SetRecipientPrivacyAsync(int userId, SocialInteractionPermission permission)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        User user = await db.Users.FindAsync(userId) ?? throw new InvalidOperationException("User not found.");
        user.PrivacySettings.AllowPrivateMessages = permission;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds a <see cref="FollowedUser"/> row so <paramref name="followerId"/> follows
    /// <paramref name="followedId"/>. Used for the Following-tier gate test.
    /// </summary>
    private async Task SeedFollowAsync(int followerId, int followedId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.FollowedUsers.Add(new FollowedUser
        {
            UserId = followerId,
            FollowedUserId = followedId,
            DateFollowed = DateTime.UtcNow,
            ReceiveAlerts = false
        });
        await db.SaveChangesAsync();
    }
}
