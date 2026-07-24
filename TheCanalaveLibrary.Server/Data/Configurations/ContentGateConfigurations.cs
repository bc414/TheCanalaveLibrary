using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server.Data.Configurations;

/// <summary>
/// ContentGate cluster (Feature 66, WU-AccessGate) — durable per-item mature-content consent.
/// Composite PK doubles as the lookup index for the only query shape this table serves
/// ("is (user, type, id) revealed?" / "all reveals for user"); no additional index needed.
/// Cascade-delete with the user (a deleted account leaves no consent residue).
/// </summary>
public sealed class UserContentRevealConfiguration : IEntityTypeConfiguration<UserContentReveal>
{
    public void Configure(EntityTypeBuilder<UserContentReveal> builder)
    {
        builder.HasKey(r => new { r.UserId, r.EntityType, r.EntityId });

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
