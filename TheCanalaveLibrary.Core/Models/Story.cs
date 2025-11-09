namespace TheCanalaveLibrary.Core.Models;

/// <summary>
/// The "hot" table. Contains only data for filtering, sorting,
/// and relationships. This table is optimized to be small
/// and live in RAM.
/// </summary>
public partial class Story
{
    public int StoryId { get; set; }
    public int? AuthorId { get; set; }
    public Rating Rating { get; set; }
    public StoryStatusEnum StoryStatusId { get; set; }
    public int WordCount { get; set; }
    public int ViewCount { get; set; }
    public DateTime PublishedDate { get; set; }
    public DateTime LastUpdatedDate { get; set; }
    public DateOnly? OriginalPublishedDate { get; set; }
    public DateOnly? OriginalLastUpdatedDate { get; set; }
    public int ActiveReportCount { get; set; }

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
    public virtual ICollection<DailyStoryStat> DailyStoryStats { get; set; } = new List<DailyStoryStat>();
    public virtual ICollection<GroupStory> GroupStories { get; set; } = new List<GroupStory>();
    public virtual ICollection<Recommendation> Recommendations { get; set; } = new List<Recommendation>();
    public virtual ICollection<SeriesEntry> SeriesEntries { get; set; } = new List<SeriesEntry>();
    public virtual ICollection<SettingDetail> SettingDetails { get; set; } = new List<SettingDetail>();
    public virtual ICollection<StoryAcknowledgment> StoryAcknowledgments { get; set; } = new List<StoryAcknowledgment>();
    public virtual ICollection<StoryArc> StoryArcs { get; set; } = new List<StoryArc>();
    public virtual ICollection<StoryCharacterRelationship> StoryCharacterRelationships { get; set; } = new List<StoryCharacterRelationship>();
    public virtual ICollection<StoryCharacter> StoryCharacters { get; set; } = new List<StoryCharacter>();
    public virtual StoryImport? StoryImport { get; set; }
    public virtual ICollection<StoryRelationship> StoryRelationshipSourceStories { get; set; } = new List<StoryRelationship>();
    public virtual ICollection<StoryRelationship> StoryRelationshipTargetStories { get; set; } = new List<StoryRelationship>();
    public virtual ICollection<StoryTag> StoryTags { get; set; } = new List<StoryTag>();
    public virtual ICollection<UserStoryInteraction> UserStoryInteractions { get; set; } = new List<UserStoryInteraction>();
}