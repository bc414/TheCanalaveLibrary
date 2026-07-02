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
        bool includeArchived = false)
    {
        int viewerId = RequireAuthenticatedUser();

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        // Two-step: EF handles the DB work, C# handles HTML stripping and final ordering.
        var raw = await readDb.ConversationParticipants
            .Where(cp => cp.UserId == viewerId && (includeArchived || !cp.IsArchived))
            .Select(cp => new
            {
                cp.ConversationId,
                cp.Conversation.Subject,
                cp.IsArchived,
                cp.LastReadTimestamp,
                OtherParticipant = cp.Conversation.ConversationParticipants
                    .Where(other => other.UserId != viewerId)
                    .Select(other => new
                    {
                        other.UserId,
                        Username = other.User.UserName,
                        AvatarUrl = other.User.ProfilePictureRelativeUrl
                    })
                    .FirstOrDefault(),
                LastMessage = cp.Conversation.PrivateMessages
                    .OrderByDescending(m => m.DateSent)
                    .Select(m => new { m.MessageText, m.DateSent })
                    .FirstOrDefault(),
                // Messages sent by the other participant after my LastReadTimestamp.
                UnreadCount = cp.Conversation.PrivateMessages
                    .Count(m => m.SenderUserId != viewerId
                                && (cp.LastReadTimestamp == null
                                    || m.DateSent > cp.LastReadTimestamp))
            })
            .ToListAsync();

        // Order by most-recent message first; conversations with no messages sort last.
        return raw
            .OrderByDescending(r => r.LastMessage?.DateSent)
            .Select(r => new ConversationSummaryDto(
                r.ConversationId,
                r.Subject,
                r.OtherParticipant is null
                    ? new MessagingParticipantDto(0, "[unknown]", DefaultAvatarUrl)
                    : new MessagingParticipantDto(
                        r.OtherParticipant.UserId,
                        r.OtherParticipant.Username ?? "[deleted]",
                        r.OtherParticipant.AvatarUrl ?? DefaultAvatarUrl),
                r.LastMessage is null ? null : MakePreview(r.LastMessage.MessageText),
                r.LastMessage?.DateSent,
                r.UnreadCount,
                r.IsArchived))
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
                totalMessageCount);
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
            totalMessageCount);
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
    private static string MakePreview(string html)
    {
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
