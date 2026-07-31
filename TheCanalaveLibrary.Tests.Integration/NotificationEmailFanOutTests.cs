using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// End-to-end coverage for the write-behind notification email fan-out (WU-NotifEmail, tracker B1):
/// create-core enqueues → <see cref="NotificationEmailFlusher"/> resolves eligibility, composes, and
/// hands the batch to <see cref="IMailTransport"/>.
///
/// <para><b>Driven through the flusher, never the worker.</b> <see cref="NotificationEmailWorker"/>
/// is removed from the test host; tests call <c>FlushAsync</c> directly so nothing races a 30-second
/// timer — the same deterministic-flush rule the reading-progress and view-count buffers follow
/// (<c>testing.md</c>).</para>
///
/// <para><b>Sends land in <see cref="RecordingMailTransport"/></b>, registered in place of the real
/// SMTP transport by <see cref="TestAppFactory"/>. Both it and the buffer are cleared per test by
/// <c>IntegrationTestBase</c>'s reset.</para>
///
/// <para><c>NewFollowerOnYou</c> is the workhorse type here: it seeds
/// <c>DefaultEmailEnabled = true</c>, has a real production sender, and resolves a User target, so
/// one type exercises eligibility, enrichment, and composition together.</para>
/// </summary>
[Collection("Postgres")]
public class NotificationEmailFanOutTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _actorId;
    private int _recipientId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _actorId = await SeedUserAsync("email-actor");
        _recipientId = await SeedUserAsync("email-recipient");
        SetActiveUser(_actorId);
    }

    // ── The happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatingANotification_SendsOneEmailToTheRecipient()
    {
        await NotifyNewFollowerAsync();

        int sent = await FlushAsync();

        sent.Should().Be(1);
        OutgoingMail mail = Transport.Sent.Should().ContainSingle().Subject;
        mail.UserId.Should().Be(_recipientId);
        mail.ToAddress.Should().Be(await EmailOfAsync(_recipientId));
        mail.Kind.Should().Be($"Notification.{NotificationTypeEnum.NewFollowerOnYou}");
    }

    [Fact]
    public async Task SentEmail_CarriesBothRfc8058UnsubscribeHeaders()
    {
        await NotifyNewFollowerAsync();
        await FlushAsync();

        OutgoingMail mail = Transport.Sent.Single();
        mail.Headers.Should().NotBeNull();
        // Both headers together, or Gmail/Yahoo bulk-sender rules treat the message as having no
        // one-click unsubscribe at all — the URL alone reads as a legacy hint.
        mail.Headers!.Should().ContainKey("List-Unsubscribe");
        mail.Headers["List-Unsubscribe-Post"].Should().Be("List-Unsubscribe=One-Click");
        mail.Headers["List-Unsubscribe"].Should().StartWith("<").And.EndWith(">");
    }

    [Fact]
    public async Task SentEmail_UsesAbsoluteLinksFromThePublicUrlProvider()
    {
        await NotifyNewFollowerAsync();
        await FlushAsync();

        string baseUrl = Factory.Services.GetRequiredService<IPublicUrlProvider>().AbsolutePageUrl("");
        OutgoingMail mail = Transport.Sent.Single();

        // A worker has no HttpContext, so a relative link in mail would be dead on arrival.
        mail.HtmlBody.Should().Contain(baseUrl.TrimEnd('/') + "/unsubscribe/");
        mail.Headers!["List-Unsubscribe"].Should().Contain(baseUrl.TrimEnd('/'));
    }

    [Fact]
    public async Task FlushingTwice_DoesNotResendTheSameNotification()
    {
        await NotifyNewFollowerAsync();

        await FlushAsync();
        int second = await FlushAsync();

        second.Should().Be(0, "a drained id is gone from the buffer; nothing re-queues it");
        Transport.Sent.Should().HaveCount(1);
    }

    // ── Eligibility gates ────────────────────────────────────────────────────────

    [Fact]
    public async Task NoEmail_WhenTheRecipientsAddressIsUnconfirmed()
    {
        await SetEmailConfirmedAsync(_recipientId, false);

        await NotifyNewFollowerAsync();
        int sent = await FlushAsync();

        sent.Should().Be(0, "an unverified address may not belong to the account holder");
        Transport.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task NoEmail_WhenTheUserTurnedTheTypeOff()
    {
        // Sparse override against a DefaultEmailEnabled = true type.
        await SetSettingAsAsync(_recipientId, NotificationTypeEnum.NewFollowerOnYou, emailEnabled: false);

        await NotifyNewFollowerAsync();
        int sent = await FlushAsync();

        sent.Should().Be(0);
        Transport.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Email_WhenTheUserTurnedOnATypeThatDefaultsOff()
    {
        // NewVouchOnYou seeds DefaultEmailEnabled = false. The override must win in BOTH
        // directions, or the sparse model silently degrades to "defaults only."
        await SetSettingAsAsync(_recipientId, NotificationTypeEnum.NewVouchOnYou, emailEnabled: true);

        await NotifyNewVouchAsync();
        int sent = await FlushAsync();

        sent.Should().Be(1);
        Transport.Sent.Single().Kind.Should().Be($"Notification.{NotificationTypeEnum.NewVouchOnYou}");
    }

    [Fact]
    public async Task NoEmail_ForATypeThatDefaultsOffAndWasNeverOverridden()
    {
        await NotifyNewVouchAsync();

        int sent = await FlushAsync();

        sent.Should().Be(0);
    }

    [Fact]
    public async Task NoEmail_WhenTheRecipientAlreadyReadTheNotificationInApp()
    {
        await NotifyNewFollowerAsync();
        await MarkAllReadForAsync(_recipientId);

        int sent = await FlushAsync();

        sent.Should().Be(0, "eligibility is resolved at drain time, so an in-app read wins the race");
    }

    [Theory]
    [InlineData(AccountStatusEnum.Suspended)]
    [InlineData(AccountStatusEnum.Banned)]
    public async Task Email_IsStillSentToRestrictedAccounts(AccountStatusEnum status)
    {
        // Deliberate, and load-bearing: AccountWarning/AccountSuspended/AccountBanned all seed
        // DefaultEmailEnabled = true and are exactly the notifications a user who cannot sign in
        // must receive. A future pass that "hardens" the flusher by suppressing mail to restricted
        // accounts would break moderation communication — this test is the tripwire for it.
        await SetAccountStatusAsync(_recipientId, status);

        await NotifyNewFollowerAsync();
        int sent = await FlushAsync();

        sent.Should().Be(1);
    }

    // ── Create-core invariants carry through ─────────────────────────────────────

    [Fact]
    public async Task NothingIsEnqueued_WhenCreateCoreDropsTheNotification()
    {
        // Drop-self: the actor notifying themself produces no row, so no email either. Hooking the
        // fan-out at create-core rather than at each semantic method is what buys this for free.
        await NotifyNewFollowerAsync(recipientId: _actorId, followerId: _actorId);

        Buffer.Count.Should().Be(0);
        (await FlushAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DuplicateNotification_ProducesOnlyOneEmail()
    {
        // Cross-existing dedup suppresses the second row; the buffer therefore never sees it.
        await NotifyNewFollowerAsync();
        await NotifyNewFollowerAsync();

        int sent = await FlushAsync();

        sent.Should().Be(1);
    }

    // ── Failure posture ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ConnectionFailure_RestoresTheBatchAndLeavesNotificationsIntact()
    {
        await NotifyNewFollowerAsync();
        Transport.FailNextBatchWith = new InvalidOperationException("SMTP unreachable");

        Func<Task> flush = async () => await FlushAsync();
        await flush.Should().ThrowAsync<InvalidOperationException>();

        // The batch is back on the queue...
        Buffer.Count.Should().Be(1, "a connection-level failure must not consume the batch");

        // ...the in-app notification is untouched (mail is a side-channel)...
        (await NotificationCountForAsync(_recipientId)).Should().Be(1);

        // ...and the next cycle succeeds.
        (await FlushAsync()).Should().Be(1);
    }

    // ── One-click unsubscribe ────────────────────────────────────────────────────

    [Fact]
    public async Task UnsubscribePost_TurnsTheTypeOffAndStopsFurtherEmail()
    {
        await NotifyNewFollowerAsync();
        await FlushAsync();

        string unsubscribeUrl = Transport.Sent.Single().Headers!["List-Unsubscribe"].Trim('<', '>');

        using HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsync(new Uri(unsubscribeUrl).PathAndQuery, null);
        response.IsSuccessStatusCode.Should().BeTrue();

        (await EffectiveEmailEnabledAsync(_recipientId, NotificationTypeEnum.NewFollowerOnYou))
            .Should().BeFalse();

        // And it actually takes effect on the next notification of that type.
        Transport.Clear();
        await MarkAllReadForAsync(_recipientId);
        await NotifyNewFollowerAsync();
        (await FlushAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UnsubscribePost_IsIdempotent()
    {
        // Mail clients and corporate link scanners both re-POST these URLs.
        await NotifyNewFollowerAsync();
        await FlushAsync();
        string path = new Uri(Transport.Sent.Single().Headers!["List-Unsubscribe"].Trim('<', '>')).PathAndQuery;

        using HttpClient client = Factory.CreateClient();
        (await client.PostAsync(path, null)).IsSuccessStatusCode.Should().BeTrue();
        (await client.PostAsync(path, null)).IsSuccessStatusCode.Should().BeTrue();

        (await EffectiveEmailEnabledAsync(_recipientId, NotificationTypeEnum.NewFollowerOnYou))
            .Should().BeFalse();
    }

    [Fact]
    public async Task UnsubscribeGet_DoesNotChangeAnything()
    {
        // Link scanners follow every GET in a message. A GET that mutated state would unsubscribe
        // users who never clicked.
        await NotifyNewFollowerAsync();
        await FlushAsync();
        string path = new Uri(Transport.Sent.Single().Headers!["List-Unsubscribe"].Trim('<', '>')).PathAndQuery;

        using HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync(path);

        response.IsSuccessStatusCode.Should().BeTrue();
        (await EffectiveEmailEnabledAsync(_recipientId, NotificationTypeEnum.NewFollowerOnYou))
            .Should().BeTrue("the confirmation page must not act on its own");
    }

    [Fact]
    public async Task UnsubscribePost_RejectsATamperedToken()
    {
        using HttpClient client = Factory.CreateClient();

        HttpResponseMessage response = await client.PostAsync("/unsubscribe/obviously-not-a-token", null);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private RecordingMailTransport Transport =>
        Factory.Services.GetRequiredService<RecordingMailTransport>();

    private NotificationEmailBuffer Buffer =>
        Factory.Services.GetRequiredService<NotificationEmailBuffer>();

    private async Task<int> FlushAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<NotificationEmailFlusher>().FlushAsync();
    }

    private async Task NotifyNewFollowerAsync(int? recipientId = null, int? followerId = null)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        INotificationWriteService svc = scope.ServiceProvider.GetRequiredService<INotificationWriteService>();
        await svc.NotifyNewFollowerAsync(recipientId ?? _recipientId, followerId ?? _actorId);
    }

    private async Task NotifyNewVouchAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        INotificationWriteService svc = scope.ServiceProvider.GetRequiredService<INotificationWriteService>();
        await svc.NotifyNewVouchAsync(_recipientId, _actorId);
    }

    private async Task MarkAllReadForAsync(int userId)
    {
        int? previous = Factory.Services.GetRequiredService<FakeActiveUserContext>().UserId;
        SetActiveUser(userId);
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<INotificationWriteService>().MarkAllAsReadAsync();
        }
        SetActiveUser(previous ?? _actorId);
    }

    /// <summary>
    /// Writes a sparse override row directly. Deliberately NOT via <c>SetSettingAsync</c>: that
    /// method is self-scoped to the active user, and these tests need to set the *recipient's*
    /// preference while the *actor* is the one generating notifications.
    /// </summary>
    private async Task SetSettingAsAsync(int userId, NotificationTypeEnum type, bool emailEnabled)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserNotificationSettings.Add(new UserNotificationSetting
        {
            UserId = userId,
            NotificationTypeId = type,
            EmailEnabled = emailEnabled,
            Collapsed = false
        });
        await db.SaveChangesAsync();
    }

    private async Task<bool> EffectiveEmailEnabledAsync(int userId, NotificationTypeEnum type)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        UserNotificationSetting? row = await db.UserNotificationSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.NotificationTypeId == type);
        if (row is not null) return row.EmailEnabled;

        return await db.NotificationTypes
            .Where(t => t.NotificationTypeId == type)
            .Select(t => t.DefaultEmailEnabled)
            .FirstAsync();
    }

    private async Task SetEmailConfirmedAsync(int userId, bool confirmed)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.EmailConfirmed, confirmed));
    }

    private async Task SetAccountStatusAsync(int userId, AccountStatusEnum status)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, status));
    }

    private async Task<string> EmailOfAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Email).FirstAsync())!;
    }

    private async Task<int> NotificationCountForAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Notifications.AsNoTracking().CountAsync(n => n.RecipientUserId == userId);
    }
}
