using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// The private-message half of the outbound email pipeline (owner ruling D18: Messaging owns PM email
/// end to end; WU-PrivateMessageEmail 2026-10-13). Given drained message ids, mails each participant
/// whose current unread run has not been mailed yet — <b>one email per unread run, never a re-nag</b> —
/// and, once the mail has left, stamps the run notified. Rules: <c>layer2-services.md</c>
/// §"Private-Message Email".
///
/// <para><b>Eligibility</b> (one query, resolved now, at drain time — "is it still unread?" has to be
/// checked as late as possible). A participant row of a conversation the batch names is mailed when:</para>
/// <list type="bullet">
///   <item>its run is not yet notified: <c>LastNotifiedTimestamp</c> is NULL, or at or before
///   <c>LastReadTimestamp</c> (a read reopens the run);</item>
///   <item>an unread message from the other party exists — the predicate
///   <c>GetUnreadConversationCountAsync</c> uses;</item>
///   <item>the row is not archived (archive mutes email too — decision row 28 (c));</item>
///   <item>the user has <c>EmailOnNewMessage</c> on, a confirmed address, and an address.</item>
/// </list>
/// <para>Account status is not a gate — a built default awaiting the owner (<c>roadmap.md</c>
/// decision row 28 (e)). <see cref="NotificationEmailComposer"/> makes the same choice for its own
/// reason (account-action notices are what a restricted user must receive), which does not carry over
/// here. What bounds it is the run rule: a Banned or currently Suspended recipient cannot sign in to
/// read, so their runs never reopen — at most one mail per conversation until they read again. The
/// sender drops out by construction: sending stamps the sender's own <c>LastReadTimestamp</c>.</para>
///
/// <para><b>Write-side read, Case 1.</b> Reads use the write context, untracked: this checks a
/// watermark it writes itself, so replica lag must never let it mail a run twice (the
/// <see cref="ServerMessagingWriteService"/> recipient-lookup precedent).</para>
///
/// <para><b>Stamp after the send.</b> The after-send hook sets <c>LastNotifiedTimestamp</c> to the
/// <c>DateSent</c> of the newest other-party message the mail covered, guarded so it only ever moves
/// forward. A failed send restores the item unstamped and the retry mails: exactly once at N=1
/// (<c>horizontal-scaling.md</c> §6 has the N≥2 caveat). A recipient who reads between compose and
/// stamp ends with <c>LastReadTimestamp</c> past the stamp, which correctly reopens the run.</para>
///
/// <para><b>Restore key:</b> the newest enqueued message id of the conversation. Composing it again
/// re-evaluates every participant of that conversation, so it re-mails exactly the recipients whose
/// mail did not go.</para>
/// </summary>
public sealed class PrivateMessageEmailComposer(
    ApplicationDbContext writeDb,
    IPublicUrlProvider publicUrls,
    MessagingUnsubscribeTokenService unsubscribeTokens,
    ILogger<PrivateMessageEmailComposer> logger) : IOutboundEmailComposer
{
    /// <summary>Telemetry kind (<c>OutgoingMail.Kind</c>) of every private-message email.</summary>
    public const string MailKind = "PrivateMessage";

    public OutboundEmailKind Kind => OutboundEmailKind.PrivateMessage;

    public async Task<ComposedEmails> ComposeAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken)
    {
        List<long> messageIds = [.. ids];

        // Message → conversation. A message deleted since it was enqueued (a moderation hard delete,
        // an account deletion that emptied the conversation) simply vanishes here.
        Dictionary<int, long> restoreIdByConversation = (await writeDb.PrivateMessages
                .AsNoTracking()
                .Where(m => messageIds.Contains(m.MessageId))
                .Select(m => new { m.ConversationId, m.MessageId })
                .ToListAsync(cancellationToken))
            .GroupBy(m => m.ConversationId)
            .ToDictionary(g => g.Key, g => g.Max(m => m.MessageId));

        if (restoreIdByConversation.Count == 0) return ComposedEmails.None;
        List<int> conversationIds = [.. restoreIdByConversation.Keys];

        // One query over the participant rows of those conversations. The newest unread other-party
        // message is read as two scalar subqueries (its date, its sender's name), never one
        // multi-column FirstOrDefault: that shape makes EF emit a ROW_NUMBER window over every
        // message (layer2-services.md §"Conversation Archiving Is Sticky", the measured trap).
        // `m.SenderUserId != cp.UserId` is null-aware in EF, so a deleted sender (NULL) counts as the
        // other party — the same predicate as GetUnreadConversationCountAsync.
        var candidates = await writeDb.ConversationParticipants
            .AsNoTracking()
            .Where(cp => conversationIds.Contains(cp.ConversationId)
                         && !cp.IsArchived
                         && (cp.LastNotifiedTimestamp == null
                             || (cp.LastReadTimestamp != null && cp.LastNotifiedTimestamp <= cp.LastReadTimestamp))
                         && cp.User.EmailOnNewMessage
                         && cp.User.EmailConfirmed
                         && cp.User.Email != null)
            .Select(cp => new
            {
                cp.ConversationId,
                cp.UserId,
                RecipientEmail = cp.User.Email!,
                NewestUnread = cp.Conversation.PrivateMessages
                    .Where(m => m.SenderUserId != cp.UserId
                                && (cp.LastReadTimestamp == null || m.DateSent > cp.LastReadTimestamp))
                    .Max(m => (DateTime?)m.DateSent),
                NewestUnreadSender = cp.Conversation.PrivateMessages
                    .Where(m => m.SenderUserId != cp.UserId
                                && (cp.LastReadTimestamp == null || m.DateSent > cp.LastReadTimestamp))
                    .OrderByDescending(m => m.DateSent)
                    .ThenByDescending(m => m.MessageId)
                    .Select(m => m.SenderUser != null ? m.SenderUser.UserName : null)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        // No unread from the other party (the sender's own row, a run already read) → no mail.
        var due = candidates.Where(c => c.NewestUnread is not null).ToList();
        if (due.Count == 0)
        {
            logger.LogDebug(
                "Private-message email batch of {BatchSize} message(s) produced no eligible recipients.", ids.Count);
            return ComposedEmails.None;
        }

        string settingsUrl = publicUrls.AbsolutePageUrl(UnsubscribeLandingPage.EmailSettingsPath);
        var mails = new List<ComposedEmail>(due.Count);
        var stamps = new List<(int ConversationId, int UserId, DateTime Stamp)>(due.Count);
        foreach (var d in due)
        {
            string unsubscribeUrl = publicUrls.AbsolutePageUrl(
                MessagingUnsubscribeTokenService.PathFor(unsubscribeTokens.CreateToken(d.UserId)));

            (string subject, string htmlBody) = PrivateMessageEmailBodies.Compose(
                d.NewestUnreadSender,
                publicUrls.AbsolutePageUrl($"/messages/{d.ConversationId}"),
                unsubscribeUrl,
                settingsUrl);

            mails.Add(new ComposedEmail(restoreIdByConversation[d.ConversationId], new OutgoingMail(
                d.RecipientEmail,
                subject,
                htmlBody,
                MailKind,
                d.UserId,
                // RFC 8058 one-click unsubscribe — both headers together (security.md
                // §"One-click unsubscribe tokens").
                new Dictionary<string, string>
                {
                    ["List-Unsubscribe"] = $"<{unsubscribeUrl}>",
                    ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click"
                })));
            stamps.Add((d.ConversationId, d.UserId, d.NewestUnread!.Value));
        }

        return new ComposedEmails(mails, (taken, ct) => StampAsync(taken.Select(i => stamps[i]), ct));
    }

    /// <summary>
    /// Marks each mailed run notified. Runs only after the transport took the mail (the flusher's
    /// after-send contract). The guard only ever moves the watermark forward, so a stale stamp can
    /// never undo a newer one. Set-based: nothing is tracked, and this is the only writer of the
    /// column.
    /// </summary>
    private async Task StampAsync(IEnumerable<(int ConversationId, int UserId, DateTime Stamp)> mailed, CancellationToken cancellationToken)
    {
        foreach ((int conversationId, int userId, DateTime stamp) in mailed)
        {
            await writeDb.ConversationParticipants
                .Where(cp => cp.ConversationId == conversationId
                             && cp.UserId == userId
                             && (cp.LastNotifiedTimestamp == null || cp.LastNotifiedTimestamp < stamp))
                .ExecuteUpdateAsync(set => set.SetProperty(cp => cp.LastNotifiedTimestamp, stamp), cancellationToken);
        }
    }
}
