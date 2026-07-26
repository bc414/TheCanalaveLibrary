using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// In-memory stand-in for <see cref="IMessagingWriteService"/> (which extends
/// <see cref="IMessagingReadService"/>) used by <see cref="MessagesPageTests"/>. Records each
/// call so tests can assert which methods were invoked without needing a host or database.
/// <para>
/// Unlike most fakes here, this one keeps a real conversation <em>store</em> rather than a single
/// canned result: <see cref="GetConversationsAsync"/> honours <see cref="ConversationScope"/> by
/// filtering that store, <see cref="SetArchivedAsync"/> moves rows between scopes, and
/// <see cref="MarkConversationReadAsync"/> zeroes the unread count. That is deliberate —
/// MessagesPage's Inbox|Archived split and its refresh-after-read flows are exactly tests of
/// whether those signals thread through correctly, so a fake that ignored them would render the
/// page tests meaningless. The archived flag lives beside the DTO (not on it) because
/// <see cref="ConversationSummaryDto"/> deliberately carries none — scope implies it.
/// </para>
/// </summary>
public class FakeMessagingWriteService : IMessagingWriteService
{
    // ── Store ─────────────────────────────────────────────────────────────────────

    private sealed class StoredConversation(ConversationSummaryDto dto, bool archived)
    {
        public ConversationSummaryDto Dto { get; set; } = dto;
        public bool Archived { get; set; } = archived;
    }

    private readonly List<StoredConversation> _conversations = [];
    private ConversationThreadDto? _thread;
    private MessagingParticipantDto? _lookupResult;
    private int _unreadCount;

    /// <summary>Seeds the conversation store the read methods project from.</summary>
    public void SetConversations(params (ConversationSummaryDto Dto, bool Archived)[] conversations)
    {
        _conversations.Clear();
        _conversations.AddRange(conversations.Select(c => new StoredConversation(c.Dto, c.Archived)));
    }

    /// <summary>Seeds the thread returned by <see cref="GetConversationThreadAsync"/>.</summary>
    public void SetThread(ConversationThreadDto thread) => _thread = thread;

    public void SetUnreadCount(int count) => _unreadCount = count;

    public void SetUserLookupResult(MessagingParticipantDto? result) => _lookupResult = result;

    // ── Read tracking ─────────────────────────────────────────────────────────────

    public List<ConversationScope> GetConversationsCalls { get; } = [];
    public List<(int ConversationId, int Page, int PageSize)> GetThreadCalls { get; } = [];

    public Task<IReadOnlyList<ConversationSummaryDto>> GetConversationsAsync(
        ConversationScope scope = ConversationScope.Active)
    {
        GetConversationsCalls.Add(scope);

        // Mirrors ServerMessagingReadService: scopes are disjoint slices of the store.
        bool archived = scope == ConversationScope.Archived;
        IReadOnlyList<ConversationSummaryDto> result = _conversations
            .Where(c => c.Archived == archived)
            .Select(c => c.Dto)
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
        foreach (StoredConversation stored in _conversations)
        {
            if (stored.Dto.ConversationId == conversationId)
                stored.Dto = stored.Dto with { UnreadCount = 0 };
        }

        return Task.CompletedTask;
    }

    public Task SetArchivedAsync(int conversationId, bool archived)
    {
        SetArchivedCalls.Add((conversationId, archived));

        // Mutate the store so a follow-up GetConversationsAsync reflects the change, the same
        // way the real service would.
        foreach (StoredConversation stored in _conversations)
        {
            if (stored.Dto.ConversationId == conversationId)
                stored.Archived = archived;
        }

        if (_thread is not null && _thread.ConversationId == conversationId)
            _thread = _thread with { IsArchived = archived };

        return Task.CompletedTask;
    }
}
