using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// The notification half of the outbound email pipeline (<c>layer2-services.md</c> §"Outbound email
/// pipeline"): given drained notification ids, decides which of them warrant an email and composes
/// one mail per eligible notification. <see cref="OutboundEmailFlusher"/> sends them. Until
/// WU-PrivateMessageEmail (2026-10-13) this was the notification-only flusher, which also did the
/// send; the send moved to the shared flusher, and this class kept the query and composition
/// unchanged.
///
/// <para><b>Eligibility is resolved here, at drain time, not at enqueue time</b> — see
/// <see cref="OutboundEmailBuffer"/> for why. Four gates, all in one query:</para>
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
/// accounts would be a regression; <c>NotificationEmailFanOutTests.Email_IsStillSentToRestrictedAccounts</c>
/// asserts the current behaviour so that change fails loudly.</para>
///
/// <para>Each mail's restore key is its own notification id. There is no after-send hook: the
/// notification row needs nothing recorded once its mail has left.</para>
/// </summary>
public sealed class NotificationEmailComposer(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IPublicUrlProvider publicUrls,
    UnsubscribeTokenService unsubscribeTokens,
    ILogger<NotificationEmailComposer> logger) : IOutboundEmailComposer
{
    public OutboundEmailKind Kind => OutboundEmailKind.Notification;

    /// <summary>
    /// Resolves eligibility for the drained ids and composes one mail per eligible notification,
    /// paired with its id so a partial send can restore exactly the unsent ones. Ids judged
    /// ineligible (read, emails off, unconfirmed address, gone) are simply absent from the result.
    /// </summary>
    public async Task<ComposedEmails> ComposeAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken)
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync(cancellationToken);
        List<long> notificationIds = [.. ids];

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
            logger.LogDebug("Notification email batch of {BatchSize} produced no eligible recipients.", ids.Count);
            return ComposedEmails.None;
        }

        // Same enrichment the in-app panel uses — one query per entity kind present, shared
        // implementation so titles and deep links can't drift between the two surfaces.
        var targets = await NotificationEnricher.ResolveTargetsAsync(
            readDb,
            rows.Select(r => (r.NotificationTypeId, r.RelatedEntityId)).ToList());

        var mails = new List<ComposedEmail>(rows.Count);
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

            mails.Add(new ComposedEmail(r.NotificationId, new OutgoingMail(
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

        return new ComposedEmails(mails);
    }
}
