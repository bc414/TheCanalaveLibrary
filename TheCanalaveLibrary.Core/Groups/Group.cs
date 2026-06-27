using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

public partial class Group
{
    public int GroupId { get; set; }

    public int? CreatorId { get; set; }

    [Required]
    [MaxLength(256)]
    public string GroupName { get; set; } = null!;

    [MaxLength(2048)]
    public string? Description { get; set; }
    
    /// <summary>
    /// Controls group visibility. E = visible to all; M = hidden from users with mature content disabled.
    /// Renamed from Rating (WU32) for semantic clarity vs. MaxContentRating. See cross-cutting.md
    /// "Group Audience-Visibility Filter".
    /// </summary>
    public Rating AudienceRating { get; set; }

    /// <summary>
    /// Ceiling on what stories/folders may be added. Enforced at write time (layer2-services.md
    /// "Group Rating Waterfall"). Folder MaxRating must be ≤ this value.
    /// </summary>
    public Rating MaxContentRating { get; set; }

    public DateTime DateCreated { get; set; }

    public virtual ICollection<GroupBlogPost> GroupBlogPosts { get; set; } = new List<GroupBlogPost>();

    public virtual User? Creator { get; set; }

    public virtual ICollection<GroupComment> GroupComments { get; set; } = new List<GroupComment>();

    public virtual ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();

    public virtual ICollection<GroupStory> GroupStories { get; set; } = new List<GroupStory>();

    public ICollection<GroupFolder> GroupFolders { get; set; } = new List<GroupFolder>();
}
