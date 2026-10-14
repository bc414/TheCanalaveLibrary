using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server.Data.Configurations;

public sealed class FollowedUserConfiguration : IEntityTypeConfiguration<FollowedUser>
{
    public void Configure(EntityTypeBuilder<FollowedUser> builder)
    {
        builder.Property(e => e.DateFollowed).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasKey(e => new { e.UserId, e.FollowedUserId });
        // Future indexes for querying (e.g., by FollowedUserId)...

        // No one follows themselves (schema audit §2.6; FollowingEndpoints refuses it first).
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_followed_users_no_self", "user_id <> followed_user_id"));
    }
}

// --- Vouch (replaces EF implicit many-to-many) ---
public sealed class VouchConfiguration : IEntityTypeConfiguration<Vouch>
{
    public void Configure(EntityTypeBuilder<Vouch> builder)
    {
        builder.HasKey(e => new { e.VouchingUserId, e.VouchedUserId });
        builder.Property(e => e.DateVouched).HasDefaultValueSql("CURRENT_TIMESTAMP");
        // No one vouches for themselves (schema audit §2.6).
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_vouches_no_self", "vouching_user_id <> vouched_user_id"));
        // Voucher-side cascades on delete; vouched-side is RESTRICT (incoming vouches cleared in C# UserDeletionService).
        builder.HasOne(v => v.VouchingUser).WithMany(u => u.VouchesGiven)
            .HasForeignKey(v => v.VouchingUserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(v => v.VouchedUser).WithMany(u => u.VouchesReceived)
            .HasForeignKey(v => v.VouchedUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
