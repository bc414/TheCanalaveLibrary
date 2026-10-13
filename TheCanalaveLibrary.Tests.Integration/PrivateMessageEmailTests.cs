using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// End-to-end coverage for private-message email (owner ruling D18, WU-PrivateMessageEmail 2026-10-13):
/// a send enqueues → <see cref="OutboundEmailFlusher"/> hands the message ids to
/// <see cref="PrivateMessageEmailComposer"/>, which mails <b>one email per unread run</b> and stamps
/// <c>ConversationParticipant.LastNotifiedTimestamp</c> after the send. Also the preference
/// (service, endpoints) and the messaging one-click unsubscribe route.
///
/// <para><b>Seeding:</b> a sender and a recipient per test (<c>SeedUserAsync</c>, which sets
/// <c>EmailConfirmed</c>; <c>EmailOnNewMessage</c> is on by its initializer). <b>FK parents:</b> the
/// users only — conversations and messages are created through <see cref="IMessagingWriteService"/>,
/// and <c>FakeWriteRateLimitService</c> allows loops. Flushed through the flusher from a fresh scope,
/// never the worker (removed from the host), so nothing races the 30 s timer. The buffer and
/// <see cref="RecordingMailTransport"/> are cleared per test by the base reset.</para>
/// Tier: Integration (Testcontainers Postgres).
/// </summary>
[Collection("Postgres")]
public class PrivateMessageEmailTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _senderId;
    private int _recipientId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _senderId = await SeedUserAsync("pm-sender");
        _recipientId = await SeedUserAsync("pm-recipient");
    }

    // ── The happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task StartingAConversation_MailsTheRecipientOnce_WithTheUnsubscribePairAndAThreadLink()
    {
        int conversationId = await StartAsync();

        (await FlushAsync()).Should().Be(1);

        OutgoingMail mail = Transport.Sent.Should().ContainSingle().Subject;
        mail.UserId.Should().Be(_recipientId);
        mail.Kind.Should().Be(PrivateMessageEmailComposer.MailKind);
        mail.Subject.Should().Contain("pm-sender");
        mail.Headers!["List-Unsubscribe-Post"].Should().Be("List-Unsubscribe=One-Click");
        mail.Headers["List-Unsubscribe"].Should().StartWith("<").And.EndWith(">").And.Contain("/unsubscribe/messages/");

        string baseUrl = Factory.Services.GetRequiredService<IPublicUrlProvider>().AbsolutePageUrl("").TrimEnd('/');
        mail.HtmlBody.Should().Contain($"href=\"{baseUrl}/messages/{conversationId}\"");
        Transport.Sent.Should().NotContain(m => m.UserId == _senderId, "the sender has nothing unread");
    }

    [Fact]
    public async Task TheMail_CarriesNeitherTheMessageNorTheSubject()
    {
        await StartAsync(subject: "Secret plans 4f9c", html: "<p>The password is swordfish-7d1e</p>");

        await FlushAsync();

        OutgoingMail mail = Transport.Sent.Single();
        (mail.Subject + mail.HtmlBody).Should().NotContain("Secret plans 4f9c").And.NotContain("swordfish-7d1e");
    }

    // ── One email per unread run ─────────────────────────────────────────────────

    [Fact]
    public async Task ABurst_SendsOneEmail()
    {
        int conversationId = await StartAsync();
        await SendAsync(conversationId, _senderId);
        await FlushAsync();

        for (int i = 0; i < 3; i++)
            await SendAsync(conversationId, _senderId);
        await FlushAsync();

        Transport.Sent.Should().ContainSingle("five messages in one unread run are one email");
    }

    [Fact]
    public async Task ReadingThenANewMessage_MailsAgain()
    {
        int conversationId = await StartAsync();
        await FlushAsync();

        await MarkReadAsync(conversationId, _recipientId);
        await SendAsync(conversationId, _senderId);
        await FlushAsync();

        Transport.Sent.Should().HaveCount(2).And.OnlyContain(m => m.UserId == _recipientId);
    }

    [Fact]
    public async Task AStandingUnreadState_IsNeverMailedAgain()
    {
        int conversationId = await StartAsync();
        await FlushAsync();
        long messageId = await NewestMessageIdAsync(conversationId);

        // The same message drained again (a restore, a duplicate enqueue): the run is already notified.
        Buffer.Enqueue([new OutboundEmailWork(OutboundEmailKind.PrivateMessage, messageId)]);
        (await FlushAsync()).Should().Be(0);

        Transport.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task ARecipientWhoReadsBeforeTheDrain_IsNotMailed_AndTheRunStaysUnstamped()
    {
        int conversationId = await StartAsync();
        await MarkReadAsync(conversationId, _recipientId);

        (await FlushAsync()).Should().Be(0);

        Transport.Sent.Should().BeEmpty();
        (await ParticipantAsync(conversationId, _recipientId)).LastNotifiedTimestamp.Should().BeNull();
    }

    [Fact]
    public async Task TheStamp_IsTheNewestCoveredMessagesDateSent()
    {
        int conversationId = await StartAsync();
        await SendAsync(conversationId, _senderId);

        await FlushAsync();

        DateTime newest = await NewestDateSentAsync(conversationId);
        (await ParticipantAsync(conversationId, _recipientId)).LastNotifiedTimestamp.Should().Be(newest);
        (await ParticipantAsync(conversationId, _senderId)).LastNotifiedTimestamp.Should().BeNull();
    }

    [Fact]
    public async Task ADeletedSender_StillCountsAsTheOtherParty_AndReadsAsAMember()
    {
        int conversationId = await StartAsync();
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            // What account deletion's SET NULL leaves behind, without deleting the user mid-test.
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PrivateMessages
                .Where(m => m.ConversationId == conversationId)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.SenderUserId, (int?)null));
        }

        (await FlushAsync()).Should().Be(1, "a NULL sender is never the recipient (the badge's null-aware predicate)");

        Transport.Sent.Single().Subject.Should().EndWith(PrivateMessageEmailBodies.UnknownSender);
    }

    // ── Gates ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoEmail_WhenTheRecipientTurnedThemOff()
    {
        await AsUserAsync(_recipientId, svc => svc.SetMyNewMessageEmailEnabledAsync(false));
        await StartAsync();

        (await FlushAsync()).Should().Be(0);
        Transport.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task NoEmail_WhenTheRecipientsAddressIsUnconfirmed()
    {
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users
                .Where(u => u.Id == _recipientId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.EmailConfirmed, false));
        }
        await StartAsync();

        (await FlushAsync()).Should().Be(0);
        Transport.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task AnArchivedConversation_NeverMails_AndAfterUnarchivingTheNextMessageDoes()
    {
        int conversationId = await StartAsync();
        await AsUserAsync(_recipientId, svc => svc.SetArchivedAsync(conversationId, true));

        (await FlushAsync()).Should().Be(0, "archive mutes the badge, and the mail too");

        await AsUserAsync(_recipientId, svc => svc.SetArchivedAsync(conversationId, false));
        (await FlushAsync()).Should().Be(0, "unarchiving alone sends nothing");
        await SendAsync(conversationId, _senderId);
        (await FlushAsync()).Should().Be(1);

        Transport.Sent.Single().UserId.Should().Be(_recipientId);
    }

    // ── Failure posture ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AConnectionFailure_RestoresTheItemUnstamped_AndTheRetryMailsOnce()
    {
        int conversationId = await StartAsync();
        Transport.FailNextBatchWith = new InvalidOperationException("SMTP unreachable");

        Func<Task> flush = FlushAsync;
        await flush.Should().ThrowAsync<InvalidOperationException>();

        Buffer.Count.Should().Be(1);
        (await ParticipantAsync(conversationId, _recipientId)).LastNotifiedTimestamp.Should().BeNull(
            "the run is stamped only after the mail leaves");

        (await FlushAsync()).Should().Be(1);
        (await FlushAsync()).Should().Be(0);
        Transport.Sent.Should().ContainSingle();
        (await ParticipantAsync(conversationId, _recipientId)).LastNotifiedTimestamp.Should().NotBeNull();
    }

    [Fact]
    public async Task ANotificationAndAMessage_LeaveInOneFlush()
    {
        await StartAsync();
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<INotificationWriteService>()
                .NotifyNewFollowerAsync(_recipientId, _senderId);
        }

        (await FlushAsync()).Should().Be(2, "one batch, one connection, both kinds");

        Transport.Sent.Select(m => m.Kind).Should().BeEquivalentTo(
            [PrivateMessageEmailComposer.MailKind, $"Notification.{NotificationTypeEnum.NewFollowerOnYou}"]);
    }

    // ── The preference: service, endpoints ───────────────────────────────────────

    [Fact]
    public async Task ThePreference_RoundTripsThroughTheService()
    {
        (await AsUserAsync(_recipientId, svc => svc.GetMyNewMessageEmailEnabledAsync())).Should().BeTrue("on by default");

        await AsUserAsync(_recipientId, svc => svc.SetMyNewMessageEmailEnabledAsync(false));
        (await AsUserAsync(_recipientId, svc => svc.GetMyNewMessageEmailEnabledAsync())).Should().BeFalse();

        await AsUserAsync(_recipientId, svc => svc.SetMyNewMessageEmailEnabledAsync(true));
        (await AsUserAsync(_recipientId, svc => svc.GetMyNewMessageEmailEnabledAsync())).Should().BeTrue();
    }

    [Fact]
    public async Task ThePreference_RefusesAnAnonymousCaller()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());
        using IServiceScope scope = Factory.Services.CreateScope();
        IMessagingWriteService svc = scope.ServiceProvider.GetRequiredService<IMessagingWriteService>();

        Func<Task> get = svc.GetMyNewMessageEmailEnabledAsync;
        Func<Task> set = () => svc.SetMyNewMessageEmailEnabledAsync(false);

        await get.Should().ThrowAsync<InvalidOperationException>();
        await set.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ThePreferenceWrite_DoesNotLeaveTheCircuitsTrackedUserStale()
    {
        // One scope models one circuit: the send tracks the sender's User (FindAsync), then the
        // set-based preference write changes that row. A later tracked read must see the new value
        // (layer2-services.md "Set-based writes leave the circuit's tracked copies stale").
        int conversationId = await StartAsync();
        SetActiveUser(_senderId);
        using IServiceScope scope = Factory.Services.CreateScope();
        IMessagingWriteService svc = scope.ServiceProvider.GetRequiredService<IMessagingWriteService>();
        ApplicationDbContext writeDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await svc.SendMessageAsync(conversationId, "<p>tracked</p>");
        await svc.SetMyNewMessageEmailEnabledAsync(false);

        (await writeDb.Users.FindAsync(_senderId))!.EmailOnNewMessage.Should().BeFalse();
    }

    [Fact]
    public async Task ThePreferenceEndpoints_RoundTripForTheSignedInUser()
    {
        SetActiveUser(_recipientId);
        HttpClient client = Factory.CreateClient();

        (await client.GetStringAsync("/api/messaging/email-preference")).Should().Be("true");

        HttpResponseMessage put = await client.PutAsync("/api/messaging/email-preference?enabled=false", null);
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetStringAsync("/api/messaging/email-preference")).Should().Be("false");
    }

    [Fact]
    public async Task ThePreferenceEndpoints_Answer401ToAnAnonymousCaller()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());
        HttpClient client = Factory.CreateClient();

        (await client.GetAsync("/api/messaging/email-preference")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PutAsync("/api/messaging/email-preference?enabled=false", null))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── One-click unsubscribe (security.md §"One-click unsubscribe tokens") ────────

    [Fact]
    public async Task TheUnsubscribeLink_TurnsMessageEmailOff_AndOnlyThePostDoesIt()
    {
        int conversationId = await StartAsync();
        await FlushAsync();
        string path = new Uri(Transport.Sent.Single().Headers!["List-Unsubscribe"].Trim('<', '>')).PathAndQuery;
        HttpClient client = Factory.CreateClient();

        HttpResponseMessage get = await client.GetAsync(path);
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        (await EmailOnNewMessageAsync(_recipientId)).Should().BeTrue("a GET is what link scanners follow; it changes nothing");

        HttpResponseMessage post = await client.PostAsync(path, null);
        post.StatusCode.Should().Be(HttpStatusCode.OK);
        (await EmailOnNewMessageAsync(_recipientId)).Should().BeFalse();

        (await client.PostAsync(path, null)).StatusCode.Should().Be(HttpStatusCode.OK, "a re-POST is idempotent");

        await MarkReadAsync(conversationId, _recipientId);
        await SendAsync(conversationId, _senderId);
        (await FlushAsync()).Should().Be(0, "unsubscribed: no further message email");
    }

    [Fact]
    public async Task TheUnsubscribeRoute_RefusesATamperedOrForeignPurposeToken_WithTheSamePage()
    {
        HttpClient client = Factory.CreateClient();
        string notificationToken = Factory.Services.GetRequiredService<UnsubscribeTokenService>()
            .CreateToken(_recipientId, NotificationTypeEnum.NewFollowerOnYou);
        string messagingToken = Factory.Services.GetRequiredService<MessagingUnsubscribeTokenService>()
            .CreateToken(_recipientId);
        string goneUserToken = Factory.Services.GetRequiredService<MessagingUnsubscribeTokenService>()
            .CreateToken(int.MaxValue);

        (await client.PostAsync("/unsubscribe/messages/obviously-not-a-token", null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsync(MessagingUnsubscribeTokenService.PathFor(notificationToken), null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "a notification link never turns message email off");
        (await client.PostAsync(UnsubscribeTokenService.PathFor(messagingToken), null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "a message link never touches a notification setting");
        (await client.PostAsync(MessagingUnsubscribeTokenService.PathFor(goneUserToken), null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "a deleted account answers the same page, never a 500");

        (await EmailOnNewMessageAsync(_recipientId)).Should().BeTrue();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private RecordingMailTransport Transport => Factory.Services.GetRequiredService<RecordingMailTransport>();

    private OutboundEmailBuffer Buffer => Factory.Services.GetRequiredService<OutboundEmailBuffer>();

    private async Task<int> FlushAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OutboundEmailFlusher>().FlushAsync();
    }

    private async Task<int> StartAsync(string subject = "Hello", string html = "<p>Hi there</p>")
    {
        SetActiveUser(_senderId);
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMessagingWriteService>()
            .StartConversationAsync(new StartConversationDto(_recipientId, subject, html));
    }

    private async Task SendAsync(int conversationId, int asUserId)
    {
        SetActiveUser(asUserId);
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMessagingWriteService>()
            .SendMessageAsync(conversationId, "<p>another</p>");
    }

    private Task MarkReadAsync(int conversationId, int asUserId) =>
        AsUserAsync(asUserId, svc => svc.MarkConversationReadAsync(conversationId));

    private async Task AsUserAsync(int userId, Func<IMessagingWriteService, Task> act)
    {
        SetActiveUser(userId);
        using IServiceScope scope = Factory.Services.CreateScope();
        await act(scope.ServiceProvider.GetRequiredService<IMessagingWriteService>());
    }

    private async Task<T> AsUserAsync<T>(int userId, Func<IMessagingWriteService, Task<T>> act)
    {
        SetActiveUser(userId);
        using IServiceScope scope = Factory.Services.CreateScope();
        return await act(scope.ServiceProvider.GetRequiredService<IMessagingWriteService>());
    }

    private async Task<ConversationParticipant> ParticipantAsync(int conversationId, int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ConversationParticipants
            .AsNoTracking()
            .SingleAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId);
    }

    private async Task<long> NewestMessageIdAsync(int conversationId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PrivateMessages
            .Where(m => m.ConversationId == conversationId)
            .MaxAsync(m => m.MessageId);
    }

    private async Task<DateTime> NewestDateSentAsync(int conversationId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().PrivateMessages
            .Where(m => m.ConversationId == conversationId)
            .MaxAsync(m => m.DateSent);
    }

    private async Task<bool> EmailOnNewMessageAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users
            .Where(u => u.Id == userId)
            .Select(u => u.EmailOnNewMessage)
            .SingleAsync();
    }
}
