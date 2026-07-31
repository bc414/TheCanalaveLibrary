using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// The drain half of the write-behind notification-email fan-out: takes a batch of notification ids
/// off <see cref="NotificationEmailBuffer"/>, decides which of them actually warrant an email,
/// composes the messages, and hands the whole batch to <see cref="IMailTransport"/> over one
/// connection. Body/worker split follows the <c>ReadingProgressFlusher</c>/<c>ReadingProgressFlushWorker</c>
/// pattern, so integration tests can flush deterministically instead of racing a timer.
///
/// <para><b>Eligibility is resolved here, not at enqueue time</b> — see
/// <see cref="NotificationEmailBuffer"/> for why. Four gates, all in one query:</para>
/// <list type="number">
///   <item><b>Still unread.</b> A notification the recipient already opened in-app needs no email.</item>
///   <item><b>Effective <c>EmailEnabled</c>.</b> The sparse <c>UserNotificationSetting</c> row's
///   value, falling back to <c>NotificationType.DefaultEmailEnabled</c> — the same LEFT JOIN
///   <c>GetSettingsAsync</c> implements.</item>
///   <item><b><c>EmailConfirmed</c>.</b> Never mail an unverified address: it may not belong to the
///   account holder, and unverified recipients are what wrecks a sending domain's reputation.</item>
///   <item><b>A non-empty address.</b> Defensive; Identity requires one, but the column is nullable.</item>
/// </list>
///
/// <para><b>Account status is deliberately NOT a gate.</b> <c>AccountWarning</c>,
/// <c>AccountSuspended</c>, and <c>AccountBanned</c> all seed <c>DefaultEmailEnabled = true</c> and
/// are precisely the notifications a restricted user must receive — in-app delivery is useless to
/// someone who cannot sign in. A future pass that "hardens" this by suppressing mail to suspended
/// accounts would be a regression; <c>NotificationEmailFlusherTests</c> asserts the current
/// behaviour so that change fails loudly.</para>
///
/// <para><b>Failure posture.</b> A connection-level SMTP failure propagates, and
/// <see cref="NotificationEmailWorker"/> restores the batch for the next cycle. Per-message
/// failures are handled inside the transport (logged, counted, dropped — see
/// <see cref="IMailTransport.SendBatchAsync"/>). Nothing here ever touches the in-app
/// <c>Notification</c> rows: mail is a side-channel, and a mail outage must not corrupt the
/// notification feed.</para>
/// </summary>
public sealed class NotificationEmailFlusher(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IMailTransport transport,
    IPublicUrlProvider publicUrls,
    UnsubscribeTokenService unsubscribeTokens,
    NotificationEmailBuffer buffer,
    ILogger<NotificationEmailFlusher> logger)
{
    /// <summary>
    /// Maximum notifications converted to email in one drain cycle. Bounds both the SMTP
    /// conversation length and the cost of restoring the batch after a connection failure.
    /// </summary>
    public const int MaxBatchSize = 200;

    /// <summary>
    /// Drains and sends one batch. Returns the number of emails handed to the transport (which is
    /// not the same as the number delivered — see the class doc's failure posture).
    /// </summary>
    public async Task<int> FlushAsync(CancellationToken cancellationToken = default)
    {
        List<long> batch = buffer.Drain(MaxBatchSize);
        if (batch.Count == 0) return 0;

        try
        {
            List<OutgoingMail> mails = await ComposeAsync(batch, cancellationToken);
            if (mails.Count == 0) return 0;

            await transport.SendBatchAsync(mails, cancellationToken);
            return mails.Count;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Restore before rethrowing so the worker's catch doesn't have to know the batch.
            buffer.Restore(batch);
            logger.LogError(ex,
                "Notification email flush failed for {BatchSize} notification(s); batch restored for retry.",
                batch.Count);
            throw;
        }
    }

    private async Task<List<OutgoingMail>> ComposeAsync(List<long> notificationIds, CancellationToken cancellationToken)
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync(cancellationToken);

        // One query, all four eligibility gates. LEFT JOINs mirror GetNotificationsAsync's shape:
        //   • UserNotificationSettings (sparse, composite key) → effective EmailEnabled.
        //   • Users on SourceUserId (int?) → actor name; null when the source was deleted
        //     (SET NULL policy) or the type has no actor.
        var rows = await (
            from n in readDb.Notifications
            where notificationIds.Contains(n.NotificationId) && !n.IsRead
            join nt in readDb.NotificationTypes
                on n.NotificationTypeId equals nt.NotificationTypeId
            join recipient in readDb.Users
                on n.RecipientUserId equals recipient.Id
            join uns in readDb.UserNotificationSettings
                on new { UserId = n.RecipientUserId, n.NotificationTypeId }
                equals new { uns.UserId, uns.NotificationTypeId } into settings
            from s in settings.DefaultIfEmpty()
            join u in readDb.Users
                on n.SourceUserId equals u.Id into sources
            from src in sources.DefaultIfEmpty()
            where recipient.EmailConfirmed
                  && recipient.Email != null
                  && (s != null ? s.EmailEnabled : nt.DefaultEmailEnabled)
            select new
            {
                n.NotificationId,
                n.NotificationTypeId,
                CategoryId = nt.NotificationCategory,
                TypeDisplayName = nt.DisplayName,
                n.SourceUserId,
                SourceUserName = src.UserName,
                n.RelatedEntityId,
                n.DateCreated,
                n.RecipientUserId,
                RecipientEmail = recipient.Email!
            }).ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            logger.LogDebug("Notification email batch of {BatchSize} produced no eligible recipients.",
                notificationIds.Count);
            return [];
        }

        // Same enrichment the in-app panel uses — one query per entity kind present, shared
        // implementation so titles and deep links can't drift between the two surfaces.
        var targets = await NotificationEnricher.ResolveTargetsAsync(
            readDb,
            rows.Select(r => (r.NotificationTypeId, r.RelatedEntityId)).ToList());

        var mails = new List<OutgoingMail>(rows.Count);
        foreach (var r in rows)
        {
            (string? targetTitle, string? targetUrl) =
                targets.TryGetValue((r.NotificationTypeId, r.RelatedEntityId), out var target)
                    ? target
                    : (null, null);

            // Collapsed is a panel-display concern with no email meaning — pass false rather than
            // paying for the join. IsRead is false by construction (the query filters on it).
            var dto = new NotificationDto(
                r.NotificationId, r.NotificationTypeId, r.CategoryId, r.SourceUserId,
                r.SourceUserName, targetTitle, targetUrl, r.RelatedEntityId,
                IsRead: false, r.DateCreated, Collapsed: false);

            string unsubscribeUrl = publicUrls.AbsolutePageUrl(
                UnsubscribeTokenService.PathFor(unsubscribeTokens.CreateToken(r.RecipientUserId, r.NotificationTypeId)));

            (string subject, string htmlBody) = NotificationEmailBodies.Compose(
                dto,
                r.TypeDisplayName,
                targetUrl is null ? null : publicUrls.AbsolutePageUrl(targetUrl),
                unsubscribeUrl,
                publicUrls.AbsolutePageUrl(NotificationEmailBodies.SettingsPath));

            mails.Add(new OutgoingMail(
                r.RecipientEmail,
                subject,
                htmlBody,
                $"Notification.{r.NotificationTypeId}",
                r.RecipientUserId,
                // RFC 8058 one-click unsubscribe. Both headers are required together: the URL
                // alone reads as a legacy mailto/link hint, and Gmail/Yahoo bulk-sender rules key
                // off the List-Unsubscribe-Post pair.
                new Dictionary<string, string>
                {
                    ["List-Unsubscribe"] = $"<{unsubscribeUrl}>",
                    ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click"
                }));
        }

        return mails;
    }
}
