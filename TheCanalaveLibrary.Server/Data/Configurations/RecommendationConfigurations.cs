using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server.Data.Configurations;

public sealed class RecommendationConfiguration : IEntityTypeConfiguration<Recommendation>
{
    public void Configure(EntityTypeBuilder<Recommendation> builder)
    {
        builder.Property(e => e.DatePosted).HasDefaultValueSql("CURRENT_TIMESTAMP");
        // Future indexes for querying (e.g., by StoryId, RecommenderId, StatusId)...
    }
}

public sealed class RecommendationDetailConfiguration : IEntityTypeConfiguration<RecommendationDetail>
{
    public void Configure(EntityTypeBuilder<RecommendationDetail> builder)
    {
        // Configure the 1-to-1 relationship.
        // RecommendationDetail is the dependent, its PK is also the FK to Recommendation.
        builder.HasOne(d => d.Recommendation)
            .WithOne(r => r.RecommendationDetail)
            .HasForeignKey<RecommendationDetail>(d => d.RecommendationId)
            .OnDelete(DeleteBehavior.Cascade); // Deleting a Recommendation deletes its text

        // Future indexes for full-text search on the 'Text' column...
    }
}

public sealed class RecommendationStatusConfiguration : IEntityTypeConfiguration<RecommendationStatus>
{
    public void Configure(EntityTypeBuilder<RecommendationStatus> builder)
    {
        builder.HasMany(rs => rs.Recommendations)
            .WithOne(r => r.Status)
            .HasForeignKey(r => r.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new { RecommendationStatusId = (short)1, StatusName = "Pending Approval", Description = "Submitted by user, awaiting author review." },
            new { RecommendationStatusId = (short)2, StatusName = "Approved", Description = "Publicly visible." },
            new { RecommendationStatusId = (short)3, StatusName = "Rejected", Description = "Rejected by author, not visible." },
            new { RecommendationStatusId = (short)4, StatusName = "Under Review", Description = "An approved recommendation that was reported and is under review." }
        );

        builder.HasIndex(e => e.StatusName).IsUnique();
        // Future indexes for querying...
    }
}

public sealed class RecommendationSuccessConfiguration : IEntityTypeConfiguration<RecommendationSuccess>
{
    public void Configure(EntityTypeBuilder<RecommendationSuccess> builder)
    {
        builder.Property(e => e.DateRecorded).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasKey(e => new { e.UserId, e.RecommendationId });
        // Future indexes for querying (e.g., by RecommendationId)...
    }
}
