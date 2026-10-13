using System.Runtime.ExceptionServices;
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
///   non-NULL <c>EmailEnabled</c>, falling back to <c>NotificationType.DefaultEmailEnabled</c> when
///   there is no row or the column is NULL (per field — owner ruling D19) — the same LEFT JOIN
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
/// <para><b>Failure posture (restore rules rewritten WU-NotificationCorrectness, 2026-10-12).</b>
/// <see cref="FlushAsync"/> restores to the buffer exactly the ids whose mail was not sent, then
/// rethrows; the worker logs and carries on. Per-message failures are handled inside the transport
/// (logged, counted, dropped — see <see cref="IMailTransport.SendBatchAsync"/>). Which ids go back:</para>
/// <list type="bullet">
///   <item>composing failed → the whole drained batch (nothing was sent);</item>
///   <item><see cref="MailBatchInterruptedException"/> → only the composed mails from its
///   <see cref="MailBatchInterruptedException.ProcessedCount"/> on — the leading ones were sent (or
///   dropped as a per-message failure) and must not go twice, and ids judged ineligible are already
///   correctly gone;</item>
///   <item>any other transport exception → every composed id (assume nothing was processed);</item>
///   <item>a mail the server <b>deferred</b> (a 4xx to its recipient, reported by the transport
///   whether or not the batch then stopped) → requeued through
///   <see cref="NotificationEmailBuffer.Defer"/>, which drops it, counted and logged here, at its
///   <see cref="NotificationEmailBuffer.MaxDeferrals"/>th deferral (review fixes, 2026-10-13). It
///   never holds back the rest of the batch;</item>
///   <item>cancellation (a shutdown mid-flight) restores by the same rules and rethrows the
///   <see cref="OperationCanceledException"/> itself, so the caller's shutdown path still sees one.</item>
/// </list>
/// <para>Nothing here ever touches the in-app <c>Notification</c> rows: mail is a side-channel, and a
/// mail outage must not corrupt the notification feed.</para>
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
    /// Drains and sends one batch. Returns the number of emails the transport took (sent, or dropped
    /// as a per-message failure — not the number delivered, see the class doc's failure posture);
    /// mails the server deferred are not counted. On failure the unsent ids are already back in the
    /// buffer when this throws.
    /// </summary>
    public async Task<int> FlushAsync(CancellationToken cancellationToken = default) =>
        (await FlushBatchAsync(cancellationToken)).Taken;

    /// <summary>One batch's result: mails the transport took, and whether any drained id left the
    /// queue for good (sent, dropped, or ineligible) rather than going straight back as a deferral.</summary>
    private readonly record struct BatchResult(int Taken, bool Settled);

    private async Task<BatchResult> FlushBatchAsync(CancellationToken cancellationToken)
    {
        List<long> batch = buffer.Drain(MaxBatchSize);
        if (batch.Count == 0) return new BatchResult(0, Settled: false);

        List<(long NotificationId, OutgoingMail Mail)> composed;
        try
        {
            composed = await ComposeAsync(batch, cancellationToken);
        }
        catch (Exception ex)
        {
            // Nothing was sent: the whole drained batch goes back.
            buffer.Restore(batch);
            if (ex is not OperationCanceledException)
            {
                logger.LogError(ex,
                    "Notification email compose failed for {BatchSize} notification(s); all {RestoredCount} restored for retry.",
                    batch.Count, batch.Count);
            }
            throw;
        }

        if (composed.Count == 0)
        {
            // Every drained id was ineligible: all of them are settled.
            buffer.ForgetDeferrals(batch);
            return new BatchResult(0, Settled: true);
        }

        IReadOnlyList<int> deferredIndices;
        try
        {
            deferredIndices = await transport.SendBatchAsync([.. composed.Select(c => c.Mail)], cancellationToken);
        }
        catch (Exception ex)
        {
            // The transport reports how many leading mails it finished with, and which of those the
            // server deferred; a transport that throws anything else is assumed to have processed
            // none (IMailTransport contract).
            var interrupted = ex as MailBatchInterruptedException;
            int processed = interrupted is null ? 0 : Math.Clamp(interrupted.ProcessedCount, 0, composed.Count);
            List<long> unsent = [.. composed.Skip(processed).Select(c => c.NotificationId)];
            buffer.Restore(unsent);
            HashSet<long> stillQueued = RequeueDeferred(
                composed, interrupted?.DeferredIndices.Where(i => i < processed) ?? []);
            stillQueued.UnionWith(unsent);
            buffer.ForgetDeferrals(batch.Where(id => !stillQueued.Contains(id)));

            Exception cause = interrupted is { InnerException: { } inner } ? inner : ex;
            if (cause is OperationCanceledException)
            {
                // A shutdown cancelled the send mid-batch. The unsent tail is back in the buffer;
                // rethrow the OCE itself so the caller's cancellation path (DrainAllAsync, the
                // worker) recognises it.
                logger.LogInformation(
                    "Notification email send cancelled after {ProcessedCount} of {BatchSize} mail(s); {RestoredCount} restored.",
                    processed, composed.Count, unsent.Count);
                ExceptionDispatchInfo.Capture(cause).Throw();
            }

            logger.LogError(ex,
                "Notification email send failed after {ProcessedCount} of {BatchSize} mail(s); {RestoredCount} restored for retry.",
                processed, composed.Count, unsent.Count);
            throw;
        }

        HashSet<long> deferred = RequeueDeferred(composed, deferredIndices);
        buffer.ForgetDeferrals(batch.Where(id => !deferred.Contains(id)));
        return new BatchResult(composed.Count - Math.Min(deferredIndices.Count, composed.Count),
            Settled: deferred.Count < batch.Count);
    }

    /// <summary>
    /// Hands the mails the server deferred back to the buffer, which counts each one's deferrals and
    /// gives up on it at <see cref="NotificationEmailBuffer.MaxDeferrals"/>. A mail given up on is
    /// counted on <c>canalave.email.failed</c> and logged here (the transport left the deferral to
    /// its caller), so no mail ever disappears silently.
    /// </summary>
    /// <returns>The ids actually requeued (deferred and still under the cap).</returns>
    private HashSet<long> RequeueDeferred(
        List<(long NotificationId, OutgoingMail Mail)> composed, IEnumerable<int> deferredIndices)
    {
        List<(long NotificationId, OutgoingMail Mail)> deferred =
            [.. deferredIndices.Where(i => i >= 0 && i < composed.Count).Distinct().Select(i => composed[i])];
        if (deferred.Count == 0) return [];

        HashSet<long> exhausted = [.. buffer.Defer(deferred.Select(d => d.NotificationId))];
        foreach ((long _, OutgoingMail mail) in deferred.Where(d => exhausted.Contains(d.NotificationId)))
        {
            CanalaveTelemetry.Email.Failed.Add(1, new KeyValuePair<string, object?>("canalave.email.kind", mail.Kind));
            logger.LogError(
                "Notification email {EmailKind} to user {UserId} was deferred by the SMTP server {MaxDeferrals} times; dropped (the in-app notification stands).",
                mail.Kind, mail.UserId, NotificationEmailBuffer.MaxDeferrals);
        }

        HashSet<long> requeued = [.. deferred.Select(d => d.NotificationId).Where(id => !exhausted.Contains(id))];
        if (requeued.Count > 0)
        {
            logger.LogWarning(
                "{DeferredCount} notification email(s) deferred by the SMTP server (a 4xx to the recipient); requeued for retry.",
                requeued.Count);
        }
        return requeued;
    }

    /// <summary>
    /// Drains the buffer to empty, one bounded batch at a time — the graceful-shutdown path
    /// (<see cref="NotificationEmailWorker"/>). A single <see cref="FlushAsync"/> moves at most
    /// <see cref="MaxBatchSize"/> mails, so a shutdown that flushed once would strand the rest of a
    /// deep queue. Lives here, not on the worker, so Integration tests drive it deterministically.
    ///
    /// <para>Stops at the first failed batch (that batch is already restored — retrying a dead SMTP
    /// host in a loop would only spin until the budget ran out), at a batch that settled nothing
    /// (everything it drained was deferred again, so what is left is mail the server is deferring, and
    /// retrying it at once would only repeat the deferral), or when <paramref name="budget"/> is
    /// cancelled. Never throws.</para>
    /// </summary>
    /// <returns>The number of ids still queued when it stopped (0 = fully drained).</returns>
    public async Task<int> DrainAllAsync(CancellationToken budget)
    {
        try
        {
            while (buffer.Count > 0)
            {
                budget.ThrowIfCancellationRequested();
                if (!(await FlushBatchAsync(budget)).Settled)
                    break;
            }
        }
        catch (OperationCanceledException) when (budget.IsCancellationRequested)
        {
            // The budget ran out. FlushAsync restored any batch it was holding; the caller logs the
            // stranded count this returns.
        }
        catch (Exception ex)
        {
            // FlushAsync has already logged this failure (Error, with the batch) and restored the batch.
            logger.LogWarning(
                "Notification email drain stopped after a failed batch ({ExceptionType}); {QueuedCount} still queued.",
                ex.GetType().Name, buffer.Count);
        }

        return buffer.Count;
    }

    /// <summary>
    /// Resolves eligibility for the drained ids and composes one mail per eligible notification,
    /// paired with its id so a partial send can restore exactly the unsent ones. Ids judged
    /// ineligible (read, emails off, unconfirmed address, gone) are simply absent from the result.
    /// </summary>
    private async Task<List<(long NotificationId, OutgoingMail Mail)>> ComposeAsync(
        List<long> notificationIds, CancellationToken cancellationToken)
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync(cancellationToken);

        // One query, all four eligibility gates. LEFT JOINs mirror GetNotificationsAsync's shape:
        //   • UserNotificationSettings (sparse per field, composite key) → effective EmailEnabled:
        //     the row's EmailEnabled when the row exists AND that column is non-NULL (D19), else
        //     the type default. The explicit guard translates to a CASE, not a bare COALESCE.
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
                  && (s != null && s.EmailEnabled != null ? s.EmailEnabled.Value : nt.DefaultEmailEnabled)
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

        var mails = new List<(long NotificationId, OutgoingMail Mail)>(rows.Count);
        foreach (var r in rows)
        {
            NotificationTarget target =
                targets.TryGetValue((r.NotificationTypeId, r.RelatedEntityId), out NotificationTarget found)
                    ? found
                    : default;
            string? targetUrl = target.Url;

            // Collapsed is a panel-display concern with no email meaning — pass false rather than
            // paying for the join. IsRead is false by construction (the query filters on it).
            var dto = new NotificationDto(
                r.NotificationId, r.NotificationTypeId, r.CategoryId, r.SourceUserId,
                r.SourceUserName, target.Title, targetUrl, r.RelatedEntityId,
                IsRead: false, r.DateCreated, Collapsed: false, TargetContextTitle: target.ContextTitle);

            string unsubscribeUrl = publicUrls.AbsolutePageUrl(
                UnsubscribeTokenService.PathFor(unsubscribeTokens.CreateToken(r.RecipientUserId, r.NotificationTypeId)));

            (string subject, string htmlBody) = NotificationEmailBodies.Compose(
                dto,
                r.TypeDisplayName,
                targetUrl is null ? null : publicUrls.AbsolutePageUrl(targetUrl),
                unsubscribeUrl,
                publicUrls.AbsolutePageUrl(NotificationEmailBodies.SettingsPath));

            mails.Add((r.NotificationId, new OutgoingMail(
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
                })));
        }

        return mails;
    }
}
