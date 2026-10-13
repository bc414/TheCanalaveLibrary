using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side write implementation of <see cref="IMessagingWriteService"/>.
/// Inherits <see cref="ServerMessagingReadService"/> for read methods (CQRS-lite pattern).
/// Uses <see cref="ApplicationDbContext"/> for all mutations (tracked, write-primary DbContext).
/// Message HTML is sanitized once on save (sanitize-once-on-save convention).
/// </summary>
public class ServerMessagingWriteService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser,
    IHtmlSanitizationService sanitizer,
    IWriteRateLimitService rateLimit,
    ILogger<ServerMessagingWriteService> logger)
    : ServerMessagingReadService(readDbFactory, activeUser), IMessagingWriteService
{
    // -----------------------------------------------------------------------
    // IMessagingWriteService
    // -----------------------------------------------------------------------

    public async Task<int> StartConversationAsync(StartConversationDto dto)
    {
        int senderId = RequireAuthenticatedUser();
        rateLimit.EnsureAllowed(WriteActionKind.Message, senderId);

        // Tier-3 domain validation (empty subject / body / self-message).
        List<string> errors = dto.Validate(senderId);
        if (errors.Count > 0)
            throw new MessagingValidationException(errors);

        // Load the recipient — needed for the AllowPrivateMessages gate.
        // Write-side read (Case 1: constraint check for consistency).
        User? recipient = await writeDb.Users.FindAsync(dto.RecipientUserId);
        if (recipient is null)
            throw new KeyNotFoundException($"Recipient user {dto.RecipientUserId} not found.");

        // AllowPrivateMessages gate — enforced on StartConversation only (settled WU35).
        // Gate is NOT re-checked on replies to an existing thread.
        // See layer2-services.md "AllowPrivateMessages Gate".
        await EnforcePrivacyGateAsync(
            dto.RecipientUserId,
            senderId,
            recipient.PrivacySettings.AllowPrivateMessages);

        // Sanitize-once-on-save: raw EditorView HTML → allowed-list HTML via IHtmlSanitizationService.
        string sanitizedMessage = sanitizer.Sanitize(dto.MessageHtml);

        // Conversation + two ConversationParticipant rows + first PrivateMessage, as ONE unit of
        // work (WU-NotificationCorrectness, 2026-10-12). The children ride the navigations, and EF's
        // fixup assigns their ConversationId inside the single SaveChangesAsync below, which runs in
        // one transaction — so a failure leaves nothing behind. (Two saves used to commit a bare,
        // participant-less conversation first, orphaning it if the second save failed.)
        // One timestamp for all three, so the sender's read watermark is never behind their own
        // first message.
        DateTime now = DateTime.UtcNow;
        var conversation = new Conversation
        {
            Subject = dto.Subject.Trim(),
            DateCreated = now
        };
        conversation.ConversationParticipants.Add(new ConversationParticipant
        {
            UserId = senderId,
            LastReadTimestamp = now   // sender has "read" their own first message
        });
        conversation.ConversationParticipants.Add(new ConversationParticipant
        {
            UserId = dto.RecipientUserId
            // LastReadTimestamp = null → recipient has unread from the start
        });
        conversation.PrivateMessages.Add(new PrivateMessage
        {
            SenderUserId = senderId,
            MessageText = sanitizedMessage,
            DateSent = now
        });
        writeDb.Conversations.Add(conversation);

        await writeDb.SaveChangesAsync();

        logger.LogInformation(
            "Conversation {ConversationId} started by user {SenderId} with recipient {RecipientId}.",
            conversation.ConversationId, senderId, dto.RecipientUserId);

        return conversation.ConversationId;
    }

    public async Task<MessageDto> SendMessageAsync(int conversationId, string messageHtml)
    {
        int senderId = RequireAuthenticatedUser();
        rateLimit.EnsureAllowed(WriteActionKind.Message, senderId);

        // Validate body.
        List<string> errors = MessagingValidations.ValidateMessageBody(messageHtml);
        if (errors.Count > 0)
            throw new MessagingValidationException(errors);

        // Guard: current user must be a participant in this conversation.
        ConversationParticipant? myParticipant = await writeDb.ConversationParticipants
            .FirstOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == senderId);

        if (myParticipant is null)
            throw new KeyNotFoundException($"Conversation {conversationId} not found.");

        // Sanitize-once-on-save.
        string sanitizedMessage = sanitizer.Sanitize(messageHtml);

        var message = new PrivateMessage
        {
            ConversationId = conversationId,
            SenderUserId = senderId,
            MessageText = sanitizedMessage,
            DateSent = DateTime.UtcNow
        };
        writeDb.PrivateMessages.Add(message);

        // Advance sender's read timestamp so they don't count their own message as unread.
        myParticipant.LastReadTimestamp = message.DateSent;

        await writeDb.SaveChangesAsync();

        // Resolve sender display info for the returned DTO (write-side read, read after commit).
        User? senderUser = await writeDb.Users.FindAsync(senderId);
        string senderUsername = senderUser?.UserName ?? "[deleted]";
        string senderAvatarUrl = senderUser?.ProfilePictureRelativeUrl ?? "/img/default-avatar.svg";

        return new MessageDto(
            message.MessageId,
            conversationId,
            senderId,
            senderUsername,
            senderAvatarUrl,
            sanitizedMessage,
            message.DateSent,
            IsOwnMessage: true);
    }

    public async Task MarkConversationReadAsync(int conversationId)
    {
        int? viewerId = ActiveUser.UserId;
        if (viewerId is null) return;   // anonymous — no-op

        ConversationParticipant? participant = await writeDb.ConversationParticipants
            .FirstOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == viewerId);

        if (participant is null) return;    // not a participant — no-op

        participant.LastReadTimestamp = DateTime.UtcNow;
        await writeDb.SaveChangesAsync();
    }

    public async Task SetArchivedAsync(int conversationId, bool archived)
    {
        int viewerId = RequireAuthenticatedUser();

        ConversationParticipant? participant = await writeDb.ConversationParticipants
            .FirstOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == viewerId);

        if (participant is null)
            throw new KeyNotFoundException($"Conversation {conversationId} not found.");

        participant.IsArchived = archived;
        await writeDb.SaveChangesAsync();
    }

    // -----------------------------------------------------------------------
    // Gate enforcement
    // -----------------------------------------------------------------------

    /// <summary>
    /// Enforces the recipient's <see cref="SocialInteractionPermission.AllowPrivateMessages"/> gate.
    /// Throws <see cref="MessagingPermissionException"/> when the gate blocks the sender.
    /// Called only from <see cref="StartConversationAsync"/> — not re-checked on replies.
    /// </summary>
    private async Task EnforcePrivacyGateAsync(
        int recipientId,
        int senderId,
        SocialInteractionPermission gate)
    {
        switch (gate)
        {
            case SocialInteractionPermission.Public:
            case SocialInteractionPermission.UsersOnly:
                // Any authenticated user may send. (Authentication is guaranteed by
                // RequireAuthenticatedUser() already called by the caller.)
                return;

            case SocialInteractionPermission.Following:
                // Recipient must follow the sender.
                // Write-side existence check (Case 1 — constraint check, writeDb for consistency).
                bool recipientFollowsSender = await writeDb.FollowedUsers
                    .AnyAsync(f => f.UserId == recipientId && f.FollowedUserId == senderId);

                if (!recipientFollowsSender)
                    throw new MessagingPermissionException(
                        "This user only accepts messages from people they follow.");
                return;

            case SocialInteractionPermission.Nobody:
                throw new MessagingPermissionException();

            default:
                // Unknown future tier — fail closed.
                throw new MessagingPermissionException();
        }
    }
}
