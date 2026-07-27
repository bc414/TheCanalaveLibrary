using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side read implementation of <see cref="IMessagingReadService"/>.
/// Uses <see cref="ReadOnlyApplicationDbContext"/> (no-tracking) and projects straight to DTOs.
/// All methods are viewer-scoped via <see cref="IActiveUserContext"/>.
/// </summary>
public partial class ServerMessagingReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser) : IMessagingReadService
{
    private const string DefaultAvatarUrl = "/img/default-avatar.svg";

    /// <summary>
    /// Exposed as a protected property so the derived write service can access the user context
    /// without double-capturing the constructor parameter (eliminates CS9107/CS9124 warnings).
    /// </summary>
    protected IActiveUserContext ActiveUser { get; } = activeUser;

    /// <summary>
    /// Read contexts are created per method from this factory (`await using`) — never held for the
    /// service's lifetime. See <c>layer2-services.md</c> §"Read-context concurrency: factory per method".
    /// </summary>
    protected IDbContextFactory<ReadOnlyApplicationDbContext> ReadDbFactory { get; } = readDbFactory;

    // -----------------------------------------------------------------------
    // IMessagingReadService
    // -----------------------------------------------------------------------

    public async Task<IReadOnlyList<ConversationSummaryDto>> GetConversationsAsync(
        ConversationScope scope = ConversationScope.Active)
    {
        int viewerId = RequireAuthenticatedUser();
        bool archived = scope == ConversationScope.Archived;

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        // ── Step 1: order on METADATA only (id + last-message date) ──────────────────
        // ID-first shape, mirroring GetConversationThreadAsync's page-the-ids-then-fetch idiom.
        // This step transfers ~12 bytes/row and is where any future page window goes
        // (Skip/Take here — the hydration below already handles an arbitrary id list).
        //
        // The two ordering keys are load-bearing: Postgres defaults to NULLS FIRST for
        // ORDER BY ... DESC, so a single-key sort would promote message-less conversations to
        // the TOP; the contract is that they sort LAST.
        // See layer2-services.md §"Conversation Archiving Is Sticky".
        List<int> orderedIds = await readDb.ConversationParticipants
            .Where(cp => cp.UserId == viewerId && cp.IsArchived == archived)
            .Select(cp => new
            {
                cp.ConversationId,
                // MAX over an empty set is NULL in SQL — the nullable cast keeps the C# type honest.
                LastMessageDate = cp.Conversation.PrivateMessages.Max(m => (DateTime?)m.DateSent)
            })
            .OrderByDescending(x => x.LastMessageDate != null)
            .ThenByDescending(x => x.LastMessageDate)
            .Select(x => x.ConversationId)
            .ToListAsync();

        if (orderedIds.Count == 0) return [];

        // ── Step 2: hydrate details for exactly those ids ────────────────────────────
        var raw = await readDb.ConversationParticipants
            .Where(cp => cp.UserId == viewerId && orderedIds.Contains(cp.ConversationId))
            .Select(cp => new
            {
                cp.ConversationId,
                cp.Conversation.Subject,
                OtherParticipant = cp.Conversation.ConversationParticipants
                    .Where(other => other.UserId != viewerId)
                    .Select(other => new
                    {
                        other.UserId,
                        Username = other.User.UserName,
                        AvatarUrl = other.User.ProfilePictureRelativeUrl
                    })
                    .FirstOrDefault(),
                // Bounded prefix, not the whole body: the preview is ≤100 plain-text chars, so
                // shipping a multi-KB message across the wire to truncate it in C# is waste.
                //
                // MEASURED CONSTRAINT (2026-07-26 — do not "simplify" this back):
                // the Substring must sit in the OUTER projection, applied to the subquery's
                // scalar result. Putting it inside the FirstOrDefault projection pushes it into
                // EF's ROW_NUMBER window, where Postgres evaluates it on EVERY message row
                // before row elimination — forcing a detoast per row and taking the hydration
                // step from ~5 ms to ~10.5 ms on a 400-conversation / 8.5k-message inbox
                // (WindowAgg 0.88 ms → 5.96 ms). See audit/Messaging.md §WU-MsgReadPath measurement.
                LastMessageDate = cp.Conversation.PrivateMessages
                    .OrderByDescending(m => m.DateSent)
                    .Select(m => (DateTime?)m.DateSent)
                    .FirstOrDefault(),
                LastMessageHtmlPrefix = cp.Conversation.PrivateMessages
                    .OrderByDescending(m => m.DateSent)
                    .Select(m => m.MessageText)
                    .FirstOrDefault()!
                    .Substring(0, PreviewFetchPrefixChars),
                // Messages sent by the other participant after my LastReadTimestamp.
                UnreadCount = cp.Conversation.PrivateMessages
                    .Count(m => m.SenderUserId != viewerId
                                && (cp.LastReadTimestamp == null
                                    || m.DateSent > cp.LastReadTimestamp))
            })
            .ToListAsync();

        // Reassemble in step-1 order (Contains gives no ordering guarantee).
        Dictionary<int, int> rank = new(orderedIds.Count);
        for (int i = 0; i < orderedIds.Count; i++) rank[orderedIds[i]] = i;

        return raw
            .OrderBy(r => rank[r.ConversationId])
            .Select(r => new ConversationSummaryDto(
                r.ConversationId,
                r.Subject,
                r.OtherParticipant is null
                    ? new MessagingParticipantDto(0, "[unknown]", DefaultAvatarUrl)
                    : new MessagingParticipantDto(
                        r.OtherParticipant.UserId,
                        r.OtherParticipant.Username ?? "[deleted]",
                        r.OtherParticipant.AvatarUrl ?? DefaultAvatarUrl),
                r.LastMessageHtmlPrefix is null ? null : MakePreview(r.LastMessageHtmlPrefix),
                r.LastMessageDate,
                r.UnreadCount))
            .ToList();
    }

    public async Task<ConversationThreadDto> GetConversationThreadAsync(
        int conversationId, int page, int pageSize)
    {
        int viewerId = RequireAuthenticatedUser();

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        // Guard + header in one query. Returns null when conversation doesn't exist
        // or the viewer is not a participant.
        var header = await readDb.ConversationParticipants
            .Where(cp => cp.ConversationId == conversationId && cp.UserId == viewerId)
            .Select(cp => new
            {
                cp.Conversation.Subject,
                // Viewer's own archived flag — free here (this query is already on their
                // participant row) and required by the thread-header archive control.
                cp.IsArchived,
                OtherParticipant = cp.Conversation.ConversationParticipants
                    .Where(other => other.UserId != viewerId)
                    .Select(other => new
                    {
                        other.UserId,
                        Username = other.User.UserName,
                        AvatarUrl = other.User.ProfilePictureRelativeUrl
                    })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync();

        if (header is null)
            throw new KeyNotFoundException($"Conversation {conversationId} not found.");

        int totalMessageCount = await readDb.PrivateMessages
            .CountAsync(m => m.ConversationId == conversationId);

        // Step 1: page the message ids — page 1 = most recent (descending order).
        List<long> messageIds = await readDb.PrivateMessages
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.DateSent)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => m.MessageId)
            .ToListAsync();

        if (messageIds.Count == 0)
        {
            return new ConversationThreadDto(
                conversationId,
                header.Subject,
                header.OtherParticipant is null
                    ? new MessagingParticipantDto(0, "[unknown]", DefaultAvatarUrl)
                    : new MessagingParticipantDto(
                        header.OtherParticipant.UserId,
                        header.OtherParticipant.Username ?? "[deleted]",
                        header.OtherParticipant.AvatarUrl ?? DefaultAvatarUrl),
                [],
                totalMessageCount,
                header.IsArchived);
        }

        // Step 2: fetch the selected messages, ordered ascending (oldest first) for display.
        List<MessageDto> messages = await readDb.PrivateMessages
            .Where(m => messageIds.Contains(m.MessageId))
            .OrderBy(m => m.DateSent)
            .Select(m => new MessageDto(
                m.MessageId,
                m.ConversationId,
                m.SenderUserId,
                m.SenderUser != null ? m.SenderUser.UserName! : "[deleted]",
                m.SenderUser != null
                    ? (m.SenderUser.ProfilePictureRelativeUrl ?? DefaultAvatarUrl)
                    : DefaultAvatarUrl,
                m.MessageText,
                m.DateSent,
                m.SenderUserId == viewerId))
            .ToListAsync();

        var otherParticipantDto = header.OtherParticipant is null
            ? new MessagingParticipantDto(0, "[unknown]", DefaultAvatarUrl)
            : new MessagingParticipantDto(
                header.OtherParticipant.UserId,
                header.OtherParticipant.Username ?? "[deleted]",
                header.OtherParticipant.AvatarUrl ?? DefaultAvatarUrl);

        return new ConversationThreadDto(
            conversationId,
            header.Subject,
            otherParticipantDto,
            messages,
            totalMessageCount,
            header.IsArchived);
    }

    public async Task<int> GetUnreadConversationCountAsync()
    {
        int? viewerId = ActiveUser.UserId;
        if (viewerId is null) return 0;

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        // Count conversations (non-archived) that have at least one message from the other
        // participant sent after my LastReadTimestamp (or ever, if I have no timestamp yet).
        return await readDb.ConversationParticipants
            .Where(cp => cp.UserId == viewerId && !cp.IsArchived)
            .CountAsync(cp => cp.Conversation.PrivateMessages.Any(m =>
                m.SenderUserId != viewerId
                && (cp.LastReadTimestamp == null || m.DateSent > cp.LastReadTimestamp)));
    }

    public async Task<MessagingParticipantDto?> FindUserByUsernameAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;

        // Case-insensitive match via Npgsql's ILike (EF Core string.Contains(StringComparison)
        // overload is untranslatable — use EF.Functions.ILike for case-insensitive LIKE,
        // or equality after normalisation).
        // Identity stores normalised usernames in NormalizedUserName (upper-case).
        string normalised = username.Trim().ToUpperInvariant();

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        return await readDb.Users
            .Where(u => u.NormalizedUserName == normalised)
            .Select(u => new MessagingParticipantDto(
                u.Id,
                u.UserName!,
                u.ProfilePictureRelativeUrl ?? DefaultAvatarUrl))
            .FirstOrDefaultAsync();
    }

    // -----------------------------------------------------------------------
    // Shared helpers
    // -----------------------------------------------------------------------

    /// <summary>Returns the current viewer's UserId or throws if anonymous.</summary>
    protected int RequireAuthenticatedUser()
    {
        return ActiveUser.UserId
            ?? throw new InvalidOperationException("Messaging operations require an authenticated user.");
    }

    /// <summary>
    /// Strips HTML tags and entity-decodes to produce a plain-text preview of the message,
    /// truncated to 100 characters. Used for the conversation list "last message" excerpt.
    /// The message text is already sanitized (stored after allow-list sanitization); no
    /// security implication here — this is purely a display convenience.
    /// </summary>
    /// <summary>
    /// Raw-HTML prefix fetched per conversation for the list preview. The preview is ≤100
    /// plain-text chars; 2048 raw chars is a ~20× allowance for tag/entity inflation, so the
    /// stripped prefix virtually always yields the full 100. Pathological bodies (e.g. one
    /// enormous link URL) may yield a shorter preview — acceptable for a listing excerpt.
    /// </summary>
    private const int PreviewFetchPrefixChars = 2048;

    private static string MakePreview(string html)
    {
        // The input may be a SQL-truncated prefix (PreviewFetchPrefixChars), so it can end
        // mid-tag ("...<a hre"); an unclosed trailing "<" fragment would survive tag-stripping
        // as literal text. Drop it. (A bisected entity like "&am" decodes to itself — harmless,
        // and it can only surface when the stripped text is shorter than the preview cap.)
        int lastOpen = html.LastIndexOf('<');
        if (lastOpen >= 0 && html.IndexOf('>', lastOpen) < 0) html = html[..lastOpen];

        // Strip tags then decode entities.
        string plain = HtmlTagPattern().Replace(html, " ");
        plain = System.Net.WebUtility.HtmlDecode(plain);
        // Collapse whitespace.
        plain = WhitespacePattern().Replace(plain.Trim(), " ");
        return plain.Length <= 100 ? plain : plain[..100] + "…";
    }

    [System.Text.RegularExpressions.GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagPattern();

    [System.Text.RegularExpressions.GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
