using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server.Data.Configurations;

/// <summary>
/// Spotlight cluster (Feature 55, WU-Spotlight). Relationships configured elsewhere (exactly-once
/// rule): Story → CommunitySpotlights (Cascade) in <see cref="StoryConfiguration"/>;
/// User → CommunitySpotlights via <c>SponsoringUserId</c> (SetNull) in
/// <c>IdentityConfigurations</c>. Everything slot-related is configured here.
/// </summary>
public sealed class CommunitySpotlightConfiguration : IEntityTypeConfiguration<CommunitySpotlight>
{
    public void Configure(EntityTypeBuilder<CommunitySpotlight> builder)
    {
        builder.Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");

        // One placement per slot (1:0..1 — the FK is unique by WithOne). Restrict: a slot with a
        // placement is Redeemed history; deleting it out from under the placement is never valid.
        builder.HasOne(e => e.Slot)
            .WithOne(s => s.Placement)
            .HasForeignKey<CommunitySpotlight>(e => e.SlotId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optional endorsing recommendation — deleting the recommendation just blanks the
        // display's rec half (SetNull), the placement survives.
        builder.HasOne(e => e.Recommendation)
            .WithMany()
            .HasForeignKey(e => e.RecommendationId)
            .OnDelete(DeleteBehavior.SetNull);

        // Serves the homepage active-window read (StartDate <= now < EndDate), the block
        // capacity overlap count, and the per-story-adjacent cooldown scan.
        builder.HasIndex(e => new { e.StartDate, e.EndDate })
            .HasDatabaseName("ix_community_spotlights_start_end");
    }
}

public sealed class SpotlightSlotConfiguration : IEntityTypeConfiguration<SpotlightSlot>
{
    public void Configure(EntityTypeBuilder<SpotlightSlot> builder)
    {
        builder.Property(e => e.Source).HasConversion<short>();
        builder.Property(e => e.Status).HasConversion<short>();
        builder.Property(e => e.GrantedUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Both user FKs SetNull — matches CommunitySpotlight.SponsoringUserId's policy (the
        // grant/audit rows outlive the accounts; an orphaned Available slot is unredeemable dead
        // weight but harmless). Configured here rather than IdentityConfigurations because User
        // deliberately carries no nav collections for slots (grant bookkeeping, not user-owned
        // content) — the relationship is still configured exactly once.
        builder.HasOne(e => e.GrantedToUser)
            .WithMany()
            .HasForeignKey(e => e.GrantedToUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.GrantedByUser)
            .WithMany()
            .HasForeignKey(e => e.GrantedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Serves "my available slots" (redemption page) — tiny table, but the query shape is
        // exact: granted_to + status.
        builder.HasIndex(e => new { e.GrantedToUserId, e.Status })
            .HasDatabaseName("ix_spotlight_slots_granted_to_status");
    }
}
