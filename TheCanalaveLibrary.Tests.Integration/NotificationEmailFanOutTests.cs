using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// End-to-end coverage for the write-behind notification email fan-out (WU-NotifEmail, tracker B1):
/// create-core enqueues → <see cref="OutboundEmailFlusher"/> hands the notification ids to
/// <see cref="NotificationEmailComposer"/>, which resolves eligibility and composes, and sends the batch
/// to <see cref="IMailTransport"/> (the pipeline was notification-only until WU-PrivateMessageEmail,
/// 2026-10-13; the private-message side is <c>PrivateMessageEmailTests</c>).
///
/// <para><b>Driven through the flusher, never the worker.</b> <see cref="OutboundEmailWorker"/>
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

    // ── Mid-batch SMTP death and cancellation (WU-NotificationCorrectness, R5) ────

    [Fact]
    public async Task MidBatchDeath_RestoresOnlyTheUnsent_AndTheRetryDuplicatesNothing()
    {
        int[] recipients = [_recipientId, await SeedUserAsync("email-r2"), await SeedUserAsync("email-r3")];
        foreach (int r in recipients)
            await NotifyNewFollowerAsync(recipientId: r);

        // The connection dies after the first message went out.
        Transport.FailAfter(1, new IOException("connection reset"));
        Func<Task> flush = async () => await FlushAsync();
        await flush.Should().ThrowAsync<MailBatchInterruptedException>();

        Transport.Sent.Should().HaveCount(1);
        Buffer.Count.Should().Be(2, "only the two unsent notifications go back; the sent one must not");

        (await FlushAsync()).Should().Be(2);
        Transport.Sent.Select(m => m.UserId).Should().BeEquivalentTo(recipients,
            "every recipient got exactly one email: none lost to the death, none sent twice by the retry");
    }

    [Fact]
    public async Task CancellationMidBatch_ThrowsTheCancellation_AndRestoresTheUnsent()
    {
        int[] recipients = [_recipientId, await SeedUserAsync("email-c2"), await SeedUserAsync("email-c3")];
        foreach (int r in recipients)
            await NotifyNewFollowerAsync(recipientId: r);

        // Shutdown cancels the send between the first and second message.
        Transport.FailAfter(1, new OperationCanceledException());
        Func<Task> flush = async () => await FlushAsync();
        (await flush.Should().ThrowAsync<OperationCanceledException>())
            .Which.Should().NotBeOfType<MailBatchInterruptedException>(
                "the worker's shutdown path must see a cancellation, not the batch wrapper");

        Buffer.Count.Should().Be(2, "a cancelled flush used to lose its whole drained batch");
        Transport.Sent.Should().HaveCount(1);
    }

    // ── A deferred recipient never holds back the batch (review fixes, 2026-10-13) ──
    // Before them, a 4xx to one recipient aborted the batch; the restored mail led the retried tail
    // and aborted every later batch the same way, so one full mailbox stalled all notification email.

    [Fact]
    public async Task DeferredRecipient_DoesNotHoldBackTheRest_AndIsRetriedUntilTheMailboxRecovers()
    {
        int full = await SeedUserAsync("email-full");
        int[] others = [_recipientId, await SeedUserAsync("email-d2")];
        await NotifyNewFollowerAsync(recipientId: full);
        foreach (int r in others)
            await NotifyNewFollowerAsync(recipientId: r);
        long fullId = await NotificationIdForAsync(full);
        Transport.DeferUser(full);

        (await FlushAsync()).Should().Be(2, "the deferred mail is not counted as taken");
        Transport.Sent.Select(m => m.UserId).Should().BeEquivalentTo(others);
        Buffer.Count.Should().Be(1, "only the deferred mail is queued again");
        Buffer.DeferralsOf(NotificationItem(fullId)).Should().Be(1);

        // Later mail still flows while the deferred one keeps being retried beside it.
        int later = await SeedUserAsync("email-d3");
        await NotifyNewFollowerAsync(recipientId: later);
        (await FlushAsync()).Should().Be(1);
        Transport.Sent.Select(m => m.UserId).Should().Contain(later);
        Buffer.Count.Should().Be(1);
        Buffer.DeferralsOf(NotificationItem(fullId)).Should().Be(2);

        // The mailbox recovers: the retry sends it once, and its deferral count is forgotten.
        Transport.AcceptUser(full);
        (await FlushAsync()).Should().Be(1);
        Transport.Sent.Count(m => m.UserId == full).Should().Be(1);
        Buffer.Count.Should().Be(0);
        Buffer.DeferralsOf(NotificationItem(fullId)).Should().Be(0, "a sent mail leaves no deferral state behind");
    }

    [Fact]
    public async Task DeferredRecipient_IsDroppedAtMaxDeferrals_AndTheInAppNotificationStands()
    {
        await NotifyNewFollowerAsync();
        Transport.DeferUser(_recipientId);

        for (int attempt = 1; attempt < OutboundEmailBuffer.MaxDeferrals; attempt++)
        {
            (await FlushAsync()).Should().Be(0);
            Buffer.Count.Should().Be(1, $"deferral {attempt} of {OutboundEmailBuffer.MaxDeferrals} requeues the mail");
        }

        (await FlushAsync()).Should().Be(0);
        Buffer.Count.Should().Be(0, "a mailbox that never recovers is given up on, not retried for the process's life");
        Transport.Sent.Should().BeEmpty();
        (await NotificationCountForAsync(_recipientId)).Should().Be(1, "mail is a side-channel; the in-app row stands");
    }

    [Fact]
    public async Task AnAbortedBatch_RequeuesTheMailItDeferredBeforeTheAbort()
    {
        int full = await SeedUserAsync("email-af1");
        int[] recipients = [full, _recipientId];
        foreach (int r in recipients)
            await NotifyNewFollowerAsync(recipientId: r);
        Transport.DeferUser(full);

        // Both mails are processed (one sent, one deferred), then the batch stops — so the deferred
        // one sits inside ProcessedCount, and only DeferredIndices says it was not sent.
        Transport.FailAfter(2, new IOException("connection reset"));
        Func<Task> flush = async () => await FlushAsync();
        await flush.Should().ThrowAsync<MailBatchInterruptedException>();

        Transport.Sent.Should().ContainSingle().Which.UserId.Should().Be(_recipientId);
        Buffer.Count.Should().Be(1, "the deferred mail goes back even though the batch then stopped");

        Transport.AcceptUser(full);
        (await FlushAsync()).Should().Be(1);
        Transport.Sent.Select(m => m.UserId).Should().BeEquivalentTo(recipients);
    }

    [Fact]
    public async Task DrainAll_StopsWhenOnlyDeferredMailIsLeft_InsteadOfRetryingItAtOnce()
    {
        int full = await SeedUserAsync("email-dd1");
        await NotifyNewFollowerAsync(recipientId: full);
        await NotifyNewFollowerAsync();
        long fullId = await NotificationIdForAsync(full);
        Transport.DeferUser(full);

        int stranded = await DrainAllAsync(CancellationToken.None);

        stranded.Should().Be(1, "the mail the server keeps deferring is reported, not burned through");
        Transport.Sent.Should().ContainSingle().Which.UserId.Should().Be(_recipientId);
        Buffer.DeferralsOf(NotificationItem(fullId)).Should().Be(2,
            "one deferral in the batch that also sent, one in the batch that settled nothing — then it stops");
    }

    // ── Shutdown drain (WU-NotificationCorrectness, R6) ──────────────────────────

    [Fact]
    public async Task DrainAll_DrainsPastTheFirstBatch_ToEmpty()
    {
        // 450 ids that resolve to nothing (drained, judged ineligible, gone) ahead of one real
        // notification, so the real one sits in the third batch of 200. One FlushAsync — the old
        // shutdown path — would never reach it.
        EnqueueMissingIds(450);
        await NotifyNewFollowerAsync();

        int stranded = await DrainAllAsync(CancellationToken.None);

        stranded.Should().Be(0);
        Buffer.Count.Should().Be(0);
        Transport.Sent.Should().ContainSingle().Which.UserId.Should().Be(_recipientId);
    }

    [Fact]
    public async Task DrainAll_StopsAtAFailedBatch_WithoutSpinning_AndReportsWhatItStranded()
    {
        EnqueueMissingIds(450);
        await NotifyNewFollowerAsync();
        Transport.FailNextBatchWith = new InvalidOperationException("SMTP unreachable");

        int stranded = await DrainAllAsync(CancellationToken.None);

        stranded.Should().Be(1, "the failed batch's one real notification is restored and reported");
        Buffer.Count.Should().Be(1);
        Transport.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task DrainAll_WithAnExpiredBudget_SendsNothing_AndReportsTheQueue()
    {
        await NotifyNewFollowerAsync();
        using var expired = new CancellationTokenSource();
        await expired.CancelAsync();

        int stranded = await DrainAllAsync(expired.Token);

        stranded.Should().Be(1);
        Buffer.Count.Should().Be(1);
        Transport.Sent.Should().BeEmpty();
    }

    // ── D19: an override of one field never freezes the other (the point of the ruling) ──

    [Fact]
    public async Task CollapsedOnlyOverride_DoesNotPinEmail_AgainstALaterDefaultFlip()
    {
        // The recipient follows a story, and collapses NewChapterOnFollowedStory in the panel. The
        // settings page submits BOTH effective values, so email arrives as its current default (on).
        int storyId = await SeedStoryAsync(_actorId);
        await SeedFollowAsync(_recipientId, storyId);

        SetActiveUser(_recipientId);
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<INotificationWriteService>()
                .SetSettingAsync(NotificationTypeEnum.NewChapterOnFollowedStory, emailEnabled: true, collapsed: true);
        }

        UserNotificationSetting? row = await SettingRowAsync(_recipientId, NotificationTypeEnum.NewChapterOnFollowedStory);
        row.Should().NotBeNull("Collapsed is overridden");
        row!.EmailEnabled.Should().BeNull("an email value the user never chose is not pinned");
        row.Collapsed.Should().BeTrue();

        // Retune the type: email for new chapters now defaults OFF. notification_types survives the
        // Respawn reset (TablesToIgnore), so the default is restored in finally.
        await SetDefaultEmailEnabledAsync(NotificationTypeEnum.NewChapterOnFollowedStory, false);
        try
        {
            // Read site 1: the settings read follows the new default for email, keeps the override.
            NotificationSettingDto dto;
            using (IServiceScope scope = Factory.Services.CreateScope())
            {
                dto = (await scope.ServiceProvider.GetRequiredService<INotificationReadService>().GetSettingsAsync())
                    .Single(s => s.TypeId == NotificationTypeEnum.NewChapterOnFollowedStory);
            }
            dto.EmailEnabled.Should().BeFalse("the default flip reaches a user who only ever collapsed the type");
            dto.EmailIsDefault.Should().BeTrue();
            dto.Collapsed.Should().BeTrue();
            dto.CollapsedIsDefault.Should().BeFalse();

            // Read site 2: the flusher's eligibility gate agrees, so a fresh chapter sends nothing.
            SetActiveUser(_actorId);
            using (IServiceScope scope = Factory.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<INotificationWriteService>()
                    .NotifyNewChapterAsync(storyId, chapterId: 1, authorId: _actorId);
            }
            Buffer.Count.Should().Be(1, "the in-app row is created and queued regardless of email");
            (await FlushAsync()).Should().Be(0);
            Transport.Sent.Should().BeEmpty();
        }
        finally
        {
            await SetDefaultEmailEnabledAsync(NotificationTypeEnum.NewChapterOnFollowedStory, true);
        }
    }

    [Fact]
    public async Task CollapsedOnlyOverride_StillEmails_WhileTheTypeDefaultsEmailOn()
    {
        // The other direction of the per-field coalesce: a row whose EmailEnabled is NULL follows the
        // default (on), it does not read as "off".
        SetActiveUser(_recipientId);
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<INotificationWriteService>()
                .SetSettingAsync(NotificationTypeEnum.NewFollowerOnYou, emailEnabled: true, collapsed: true);
        }
        (await SettingRowAsync(_recipientId, NotificationTypeEnum.NewFollowerOnYou))!.EmailEnabled.Should().BeNull();
        SetActiveUser(_actorId);

        await NotifyNewFollowerAsync();

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
    public async Task UnsubscribePost_ForAnAccountDeletedSinceTheMail_AnswersTheInvalidLinkPage_Not500()
    {
        // The token is signed and lives a year; account deletion is a hard delete. Mail clients and
        // link scanners re-POST old messages, so a valid token for a gone account does arrive. The
        // old insert tripped the user_id FK into a 500 (WU-NotificationCorrectness review fixes).
        await NotifyNewFollowerAsync();
        await FlushAsync();
        string path = new Uri(Transport.Sent.Single().Headers!["List-Unsubscribe"].Trim('<', '>')).PathAndQuery;

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            (await scope.ServiceProvider.GetRequiredService<UserDeletionService>().DeleteUserAsync(_recipientId))
                .Should().BeTrue();
        }

        using HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsync(path, null);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Link no longer valid");
        (await SettingRowAsync(_recipientId, NotificationTypeEnum.NewFollowerOnYou)).Should().BeNull();
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

    private OutboundEmailBuffer Buffer =>
        Factory.Services.GetRequiredService<OutboundEmailBuffer>();

    private async Task<int> FlushAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OutboundEmailFlusher>().FlushAsync();
    }

    private async Task NotifyNewFollowerAsync(int? recipientId = null, int? followerId = null)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        INotificationWriteService svc = scope.ServiceProvider.GetRequiredService<INotificationWriteService>();
        await svc.NotifyNewFollowerAsync(recipientId ?? _recipientId, followerId ?? _actorId);
    }

    private async Task<int> DrainAllAsync(CancellationToken budget)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OutboundEmailFlusher>().DrainAllAsync(budget);
    }

    /// <summary>The id of <paramref name="recipientId"/>'s one notification.</summary>
    private async Task<long> NotificationIdForAsync(int recipientId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Notifications.AsNoTracking()
            .Where(n => n.RecipientUserId == recipientId)
            .Select(n => n.NotificationId)
            .SingleAsync();
    }

    /// <summary>The buffer item a notification id travels as.</summary>
    private static OutboundEmailWork NotificationItem(long notificationId) =>
        new(OutboundEmailKind.Notification, notificationId);

    /// <summary>Queues ids no notification row has — they drain as ineligible and are dropped.</summary>
    private void EnqueueMissingIds(int count) =>
        Buffer.Enqueue(Enumerable.Range(0, count)
            .Select(i => new OutboundEmailWork(OutboundEmailKind.Notification, 9_000_000_000L + i)));

    private async Task SeedFollowAsync(int userId, int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserStoryInteractions.Add(new UserStoryInteraction { UserId = userId, StoryId = storyId, IsFollowed = true });
        await db.SaveChangesAsync();
    }

    private async Task<UserNotificationSetting?> SettingRowAsync(int userId, NotificationTypeEnum type)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.UserNotificationSettings.AsNoTracking()
            .SingleOrDefaultAsync(s => s.UserId == userId && s.NotificationTypeId == type);
    }

    private async Task SetDefaultEmailEnabledAsync(NotificationTypeEnum type, bool enabled)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.NotificationTypes.Where(t => t.NotificationTypeId == type)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.DefaultEmailEnabled, enabled));
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
    /// Writes a sparse override row directly, in canonical form (D19): <c>Collapsed</c> NULL (follow
    /// the default) and <c>EmailEnabled</c> NULL when it equals the type default. Deliberately NOT via
    /// <c>SetSettingAsync</c>: that method is self-scoped to the active user, and these tests need to
    /// set the *recipient's* preference while the *actor* is the one generating notifications.
    /// </summary>
    private async Task SetSettingAsAsync(int userId, NotificationTypeEnum type, bool emailEnabled)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool defaultEmail = await db.NotificationTypes
            .Where(t => t.NotificationTypeId == type)
            .Select(t => t.DefaultEmailEnabled)
            .FirstAsync();
        if (emailEnabled == defaultEmail) return; // canonical "default" is no row at all

        db.UserNotificationSettings.Add(new UserNotificationSetting
        {
            UserId = userId,
            NotificationTypeId = type,
            EmailEnabled = emailEnabled,
            Collapsed = null
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Effective <c>EmailEnabled</c>, resolved per field (D19) straight from the tables.</summary>
    private async Task<bool> EffectiveEmailEnabledAsync(int userId, NotificationTypeEnum type)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        UserNotificationSetting? row = await db.UserNotificationSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.NotificationTypeId == type);
        if (row?.EmailEnabled is bool overridden) return overridden;

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
