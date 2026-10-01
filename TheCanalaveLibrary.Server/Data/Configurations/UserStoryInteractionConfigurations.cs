using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server.Data.Configurations;

public sealed class UserStoryInteractionConfiguration : IEntityTypeConfiguration<UserStoryInteraction>
{
    public void Configure(EntityTypeBuilder<UserStoryInteraction> builder)
    {
        // --- VERTICAL PARTITIONS (1-to-1) ---
        builder.HasOne(usi => usi.InteractionDatePartition)
            .WithOne(d => d.UserStoryInteraction)
            .HasForeignKey<UserStoryInteractionDate>(d => new { d.UserId, d.StoryId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(usi => usi.RecommendationSource)
            .WithOne(r => r.UserStoryInteraction)
            .HasForeignKey<UserStoryRecommendationSource>(r => new { r.UserId, r.StoryId })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasKey(e => new { e.UserId, e.StoryId });
        // This table will have MANY filtered indexes on the boolean flags.
        // Future indexes for querying (e.g., by StoryId, IsFavorite, IsFollowed)...

        // Filtered, Covered Indexes for User-centric filtering (one per bookshelf tab flag).
        //
        // The NAME argument on HasIndex is LOAD-BEARING (L6 reality lesson, 2026-07-07): multiple
        // unnamed HasIndex calls on the SAME property set re-open ONE index definition — each call
        // silently overwrites the previous filter/name, and only the LAST survives into the
        // migration. That is exactly what happened here: these seven were declared unnamed from
        // WU0 until WU-L6, and the database contained only ix_user_story_interactions_has_started.
        // Distinct model names make them seven real indexes. See layer6-indexes.md
        // §"Multiple indexes on the same columns".
        builder.HasIndex(e => e.UserId, "ix_user_story_interactions_ignored").IncludeProperties(e => e.StoryId)
            .HasFilter("\"is_ignored\" = true").HasDatabaseName("ix_user_story_interactions_ignored");
        builder.HasIndex(e => e.UserId, "ix_user_story_interactions_favorite").IncludeProperties(e => e.StoryId)
            .HasFilter("\"is_favorite\" = true").HasDatabaseName("ix_user_story_interactions_favorite");
        builder.HasIndex(e => e.UserId, "ix_user_story_interactions_hidden_favorite").IncludeProperties(e => e.StoryId)
            .HasFilter("\"is_hidden_favorite\" = true").HasDatabaseName("ix_user_story_interactions_hidden_favorite");
        builder.HasIndex(e => e.UserId, "ix_user_story_interactions_followed").IncludeProperties(e => e.StoryId)
            .HasFilter("\"is_followed\" = true").HasDatabaseName("ix_user_story_interactions_followed");
        builder.HasIndex(e => e.UserId, "ix_user_story_interactions_read_it_later").IncludeProperties(e => e.StoryId)
            .HasFilter("\"is_read_it_later\" = true").HasDatabaseName("ix_user_story_interactions_read_it_later");
        builder.HasIndex(e => e.UserId, "ix_user_story_interactions_completed").IncludeProperties(e => e.StoryId)
            .HasFilter("\"is_completed\" = true").HasDatabaseName("ix_user_story_interactions_completed");
        builder.HasIndex(e => e.UserId, "ix_user_story_interactions_has_started").IncludeProperties(e => e.StoryId)
            .HasFilter("\"has_started\" = true").HasDatabaseName("ix_user_story_interactions_has_started");
    }
}

public sealed class UserStoryInteractionDateConfiguration : IEntityTypeConfiguration<UserStoryInteractionDate>
{
    public void Configure(EntityTypeBuilder<UserStoryInteractionDate> builder)
    {
        builder.HasKey(e => new { e.UserId, e.StoryId });
        // This table will have filtered indexes on each date column for sorting.
    }
}

/// <summary>
/// The attribution partition (Feature 30). Both FKs cascade, deliberately (owner ruling D3 —
/// <c>layer2-services.md</c> §"Attribution (Feature 30)"):
/// <list type="bullet">
/// <item><b>→ the USI row</b> (composite PK, configured on <see cref="UserStoryInteractionConfiguration"/>):
/// the attribution is metadata on the <c>IsReadItLater</c> bit, so it is coupled to the interaction.
/// Do <b>not</b> decouple it to users+stories — the FK failures the service audit saw came from a
/// placeholder on-load caller, not from this coupling.</item>
/// <item><b>→ the recommendation</b> (below): CASCADE is the faithful translation of the 2025 intent.
/// The SQL-Server DDL's <c>NO ACTION</c> was a multi-cascade-path workaround for an intended
/// <c>SET NULL</c> ("deleting a recommendation clears the attribution and leaves the interaction
/// alone"); in this two-table shape, nulling the attribution and deleting the sources row are the
/// same operation. RESTRICT is rejected: attribution must never block a recommender from deleting
/// their own recommendation.</item>
/// </list>
/// <para><b>Why there is no <c>Recommendation.UserStoryInteractions</c> collection.</b> It used to
/// exist, unpaired, and minted a shadow <c>user_story_interactions.recommendation_id</c> FK column
/// that nothing wrote. That was a fossil of the 2025 DDL's <c>SourceRecommendationID INT NULL</c> on
/// <c>UserStoryInteractions</c> (<c>CanalaveDBCreation.sql</c>), from before attribution moved to this
/// sparse partition — a mostly-null column on a hot table for a feature absent from its filtering
/// job. Removed by migration <c>WU_InertFeatures</c> (2026-09-30); the explicit
/// <c>WithMany()</c> below is the only recommendation edge this partition has.</para>
/// </summary>
public sealed class UserStoryRecommendationSourceConfiguration : IEntityTypeConfiguration<UserStoryRecommendationSource>
{
    public void Configure(EntityTypeBuilder<UserStoryRecommendationSource> builder)
    {
        builder.HasKey(e => new { e.UserId, e.StoryId });

        builder.HasOne(s => s.SourceRecommendation)
            .WithMany()
            .HasForeignKey(s => s.SourceRecommendationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
