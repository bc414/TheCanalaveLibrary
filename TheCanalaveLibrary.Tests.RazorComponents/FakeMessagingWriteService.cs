using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// In-memory stand-in for <see cref="IMessagingWriteService"/> (which extends
/// <see cref="IMessagingReadService"/>) used by <see cref="MessagesPageTests"/>. Records each
/// call so tests can assert which methods were invoked without needing a host or database.
/// <para>
/// Unlike most fakes here, this one keeps a real conversation <em>store</em> rather than a single
/// canned result: <see cref="GetConversationsAsync"/> honours the <c>includeArchived</c> flag by
/// filtering that store, and <see cref="SetArchivedAsync"/> mutates it. That is deliberate —
/// MessagesPage's Inbox|Archived split is exactly a test of whether the flag is threaded through
/// correctly, so a fake that ignored it would render the tab tests meaningless.
/// </para>
/// </summary>
public class FakeMessagingWriteService : IMessagingWriteService
{
    // ── Store ─────────────────────────────────────────────────────────────────────

    private readonly List<ConversationSummaryDto> _conversations = [];
    private ConversationThreadDto? _thread;
    private MessagingParticipantDto? _lookupResult;
    private int _unreadCount;

    /// <summary>Seeds the conversation store the read methods project from.</summary>
    public void SetConversations(params ConversationSummaryDto[] conversations)
    {
        _conversations.Clear();
        _conversations.AddRange(conversations);
    }

    /// <summary>Seeds the thread returned by <see cref="GetConversationThreadAsync"/>.</summary>
    public void SetThread(ConversationThreadDto thread) => _thread = thread;

    public void SetUnreadCount(int count) => _unreadCount = count;

    public void SetUserLookupResult(MessagingParticipantDto? result) => _lookupResult = result;

    // ── Read tracking ─────────────────────────────────────────────────────────────

    public List<bool> GetConversationsCalls { get; } = [];
    public List<(int ConversationId, int Page, int PageSize)> GetThreadCalls { get; } = [];

    public Task<IReadOnlyList<ConversationSummaryDto>> GetConversationsAsync(
        bool includeArchived = false)
    {
        GetConversationsCalls.Add(includeArchived);

        // Mirrors ServerMessagingReadService: includeArchived widens the set, it does not
        // switch to archived-only.
        IReadOnlyList<ConversationSummaryDto> result = _conversations
            .Where(c => includeArchived || !c.IsArchived)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<ConversationThreadDto> GetConversationThreadAsync(
        int conversationId, int page, int pageSize)
    {
        GetThreadCalls.Add((conversationId, page, pageSize));

        if (_thread is null)
            throw new KeyNotFoundException($"Conversation {conversationId} not found.");

        return Task.FromResult(_thread);
    }

    public Task<int> GetUnreadConversationCountAsync() => Task.FromResult(_unreadCount);

    public Task<MessagingParticipantDto?> FindUserByUsernameAsync(string username)
        => Task.FromResult(_lookupResult);

    // ── Write tracking ────────────────────────────────────────────────────────────

    public List<StartConversationDto> StartConversationCalls { get; } = [];
    public List<(int ConversationId, string MessageHtml)> SendMessageCalls { get; } = [];
    public List<int> MarkReadCalls { get; } = [];
    public List<(int ConversationId, bool Archived)> SetArchivedCalls { get; } = [];

    public Task<int> StartConversationAsync(StartConversationDto dto)
    {
        StartConversationCalls.Add(dto);
        return Task.FromResult(99);
    }

    public Task<MessageDto> SendMessageAsync(int conversationId, string messageHtml)
    {
        SendMessageCalls.Add((conversationId, messageHtml));
        return Task.FromResult(new MessageDto(
            MessageId: 1,
            ConversationId: conversationId,
            SenderUserId: 1,
            SenderUsername: "TestUser",
            SenderAvatarUrl: "/img/default-avatar.svg",
            MessageText: messageHtml,
            DateSent: DateTime.UtcNow,
            IsOwnMessage: true));
    }

    public Task MarkConversationReadAsync(int conversationId)
    {
        MarkReadCalls.Add(conversationId);

        // Mirror the real service: advancing LastReadTimestamp zeroes the unread count, so a
        // follow-up GetConversationsAsync reflects the read. Without this, tests of the
        // sidebar-refresh-after-read flow would assert against a fake that can't change.
        for (int i = 0; i < _conversations.Count; i++)
        {
            if (_conversations[i].ConversationId == conversationId)
                _conversations[i] = _conversations[i] with { UnreadCount = 0 };
        }

        return Task.CompletedTask;
    }

    public Task SetArchivedAsync(int conversationId, bool archived)
    {
        SetArchivedCalls.Add((conversationId, archived));

        // Mutate the store so a follow-up GetConversationsAsync reflects the change, the same
        // way the real service would.
        for (int i = 0; i < _conversations.Count; i++)
        {
            if (_conversations[i].ConversationId == conversationId)
                _conversations[i] = _conversations[i] with { IsArchived = archived };
        }

        if (_thread is not null && _thread.ConversationId == conversationId)
            _thread = _thread with { IsArchived = archived };

        return Task.CompletedTask;
    }
}
