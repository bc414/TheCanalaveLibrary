using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server.Data.Configurations;

// --- CONVERSATION ---
public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasMany(c => c.ConversationParticipants)
            .WithOne(p => p.Conversation)
            .HasForeignKey(p => p.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.PrivateMessages)
            .WithOne(m => m.Conversation)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Future indexes for querying...
    }
}

public sealed class ConversationParticipantConfiguration : IEntityTypeConfiguration<ConversationParticipant>
{
    public void Configure(EntityTypeBuilder<ConversationParticipant> builder)
    {
        builder.HasKey(e => new { e.ConversationId, e.UserId });
        // Future indexes for querying (e.g., by UserId, IsArchived)...
    }
}

public sealed class PrivateMessageConfiguration : IEntityTypeConfiguration<PrivateMessage>
{
    public void Configure(EntityTypeBuilder<PrivateMessage> builder)
    {
        builder.Property(e => e.DateSent).HasDefaultValueSql("CURRENT_TIMESTAMP");
        // L6 (2026-07-07): the hot thread-page query
        // (WHERE conversation_id = @c ORDER BY date_sent DESC LIMIT n —
        // ServerMessagingReadService) walks this in order. Supersedes the convention FK index.
        builder.HasIndex(e => new { e.ConversationId, e.DateSent })
            .HasDatabaseName("ix_private_messages_conversation_id_date_sent");
    }
}
