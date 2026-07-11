using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// The "hot" table. Contains only data for filtering, sorting,
/// and relationships. This table is optimized to be small
/// and live in RAM.
/// </summary>
public partial class Story : IModeratableContent
{
    // IModeratableContent — AuthorUserId maps to the story's AuthorId FK
    int? IModeratableContent.AuthorUserId => AuthorId;

    public int StoryId { get; set; }
    public int? AuthorId { get; set; }
    public Rating Rating { get; set; }
    public StoryStatusEnum StoryStatusId { get; set; }
    public int WordCount { get; set; }
    // ViewCount deliberately absent (R2): a high-frequency mutable counter on the hot read row is a
    // write-amplification trap. Views accumulate in daily_story_stats (per-story/day, raw-SQL table,
    // no EF model); lifetime total = SUM — see IStoryReadService.GetStoryTotalViewsAsync.
    public DateTime PublishedDate { get; set; }
    public DateTime LastUpdatedDate { get; set; }
    public DateOnly? OriginalPublishedDate { get; set; }
    public DateOnly? OriginalLastUpdatedDate { get; set; }
    public int ActiveReportCount { get; set; }

    // Soft-delete (IsTakenDown named filter) — WU34; renamed from IsHidden/DateModeratedRemoved/ModerationRemovalReason in pre-integration cleanup
    public bool IsTakenDown { get; set; }
    public DateTime? TakedownDate { get; set; }
    [MaxLength(1024)]
    public string? TakedownReason { get; set; }
    /// <summary>
    /// Provides a read-only, type-safe view of the story's tags.
    /// This prevents InvalidCastExceptions by correctly projecting the collection.
    /// </summary>
    //[NotMapped] public IReadOnlyCollection<IStoryTag> StoryTags => StoryTags.ToList();

    // --- NAVIGATION PROPERTIES ---

    // 1-to-1 Navigation to the "warm" data (for list projections)
    public virtual StoryListing StoryListing { get; set; } = null!;

    // 1-to-1 Navigation to the "cold" data
    public virtual StoryDetail StoryDetail { get; set; } = null!;

    // --- Other Navigations ---
    public virtual User? Author { get; set; }
    public virtual StoryStatus StoryStatus { get; set; } = null!;
    public virtual ICollection<BetaReader> BetaReaders { get; set; } = new List<BetaReader>();
    public virtual ICollection<ProfileBlogPost> ProfileBlogPosts { get; set; } = new List<ProfileBlogPost>();
    public virtual ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
    public virtual ICollection<CoAuthor> CoAuthors { get; set; } = new List<CoAuthor>();
    public virtual ICollection<CommunitySpotlight> CommunitySpotlights { get; set; } = new List<CommunitySpotlight>();
    public virtual ICollection<CustomListEntry> CustomListEntries { get; set; } = new List<CustomListEntry>();
    public virtual ICollection<GroupStory> GroupStories { get; set; } = new List<GroupStory>();
    public virtual ICollection<Recommendation> Recommendations { get; set; } = new List<Recommendation>();
    public virtual ICollection<SeriesEntry> SeriesEntries { get; set; } = new List<SeriesEntry>();
    public virtual ICollection<SettingDetail> SettingDetails { get; set; } = new List<SettingDetail>();
    public virtual ICollection<StoryAcknowledgment> StoryAcknowledgments { get; set; } = new List<StoryAcknowledgment>();
    public virtual ICollection<StoryArc> StoryArcs { get; set; } = new List<StoryArc>();
    public virtual ICollection<StoryCharacterPairing> StoryCharacterPairings { get; set; } = new List<StoryCharacterPairing>();
    public virtual ICollection<StoryCharacter> StoryCharacters { get; set; } = new List<StoryCharacter>();
    // "Also posted on" links (Feature 53 reframe, WU38d) — many per story, remodeled from the
    // old 1-to-1 StoryImport.
    public virtual ICollection<StoryExternalLink> ExternalLinks { get; set; } = new List<StoryExternalLink>();
    public virtual ICollection<StoryRelationship> StoryRelationshipSourceStories { get; set; } = new List<StoryRelationship>();
    public virtual ICollection<StoryRelationship> StoryRelationshipTargetStories { get; set; } = new List<StoryRelationship>();
    public virtual ICollection<StoryTag> StoryTags { get; set; } = new List<StoryTag>();
    public virtual ICollection<UserStoryInteraction> UserStoryInteractions { get; set; } = new List<UserStoryInteraction>();

}