using System.ComponentModel.DataAnnotations.Schema;
using TheCanalaveLibrary.Core.Models;
using TheCanalaveLibrary.Core.Tags;

namespace TheCanalaveLibrary.Core.Story;

/// <summary>
/// The "hot" table. Contains only data for filtering, sorting,
/// and relationships. This table is optimized to be small
/// and live in RAM.
/// </summary>
public partial class Story : IEditableStoryProperties
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

    // --- Explicit Interface Implementation for IEditableStoryProperties ---
    // This allows the Story model to be treated as an editable object by mappers and validators
    // without polluting its public API or confusing EF Core.

    [NotMapped]
    string IEditableStoryProperties.Title { get => StoryListing.StoryTitle; set => StoryListing.StoryTitle = value; }
    [NotMapped]
    string? IEditableStoryProperties.ShortDescription { get => StoryListing.ShortDescription; set => StoryListing.ShortDescription = value; }
    [NotMapped]
    Rating IEditableStoryProperties.Rating { get => Rating; set => Rating = value; }
    [NotMapped]
    StoryStatusEnum IEditableStoryProperties.StoryStatusId { get => StoryStatusId; set => StoryStatusId = value; }
    [NotMapped]
    string? IEditableStoryProperties.CoverArtRelativeUrl { get => StoryListing.CoverArtRelativeUrl; set => StoryListing.CoverArtRelativeUrl = value; }
    [NotMapped]
    string? IEditableStoryProperties.LongDescription { get => StoryDetail.LongDescription; set => StoryDetail.LongDescription = value; }
    [NotMapped]
    StoryStatusEnum IEditableStoryProperties.PostApprovalStatus { get => StoryDetail.PostApprovalStatus; set => StoryDetail.PostApprovalStatus = value; }
    [NotMapped]
    List<IStoryTag> IEditableStoryProperties.StoryTags
    {
        // The getter projects the internal collection to the DTO.
        get => StoryTags.Select(st => st as IStoryTag).ToList();
        // The setter clears the existing collection and adds the new tags.
        set { StoryTags.Clear(); foreach (IStoryTag tag in value) { StoryTags.Add(tag.ToStoryTag()); } }
    }
}