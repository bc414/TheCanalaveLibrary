using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server.Data.Configurations;

public sealed class RecommendationConfiguration : IEntityTypeConfiguration<Recommendation>
{
    public void Configure(EntityTypeBuilder<Recommendation> builder)
    {
        builder.Property(e => e.DatePosted).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(e => e.LikeCount).HasDefaultValue(0);

        // One-per-user-per-story: NULL RecommenderId values are each distinct under Postgres NULL semantics (correct).
        builder.HasIndex(e => new { e.RecommenderId, e.StoryId })
            .IsUnique()
            .HasDatabaseName("ix_recommendations_recommender_id_story_id");
    }
}

public sealed class RecommendationDetailConfiguration : IEntityTypeConfiguration<RecommendationDetail>
{
    public void Configure(EntityTypeBuilder<RecommendationDetail> builder)
    {
        builder.HasOne(d => d.Recommendation)
            .WithOne(r => r.RecommendationDetail)
            .HasForeignKey<RecommendationDetail>(d => d.RecommendationId)
            .OnDelete(DeleteBehavior.Cascade);
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
    }
}

public sealed class RecommendationSuccessConfiguration : IEntityTypeConfiguration<RecommendationSuccess>
{
    public void Configure(EntityTypeBuilder<RecommendationSuccess> builder)
    {
        builder.Property(e => e.DateRecorded).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasKey(e => new { e.UserId, e.RecommendationId });
    }
}

public sealed class RecommendationLikeConfiguration : IEntityTypeConfiguration<RecommendationLike>
{
    public void Configure(EntityTypeBuilder<RecommendationLike> builder)
    {
        builder.HasKey(e => new { e.UserId, e.RecommendationId });

        builder.HasOne(l => l.Recommendation)
            .WithMany(r => r.Likes)
            .HasForeignKey(l => l.RecommendationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
