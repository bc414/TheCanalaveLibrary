using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

public partial class Chapter
{
    public int ChapterId { get; set; }

    public int StoryId { get; set; }

    public int ChapterNumber { get; set; }

    [Required] [MaxLength(255)] public string Title { get; set; } = null!;

    /// <summary>
    /// FK to the current primary <see cref="ChapterContent"/> version. Nullable to break the
    /// circular insert dependency (Chapter ↔ ChapterContent): the Chapter row is inserted with
    /// null, then the ChapterContent is inserted (ChapterId now known), then PrimaryContentId is
    /// pointed at the new content in a second SaveChanges. Null is only the case during that brief
    /// window; in reads, treat null like an unpublished draft. Migration: WU17.
    /// </summary>
    public long? PrimaryContentId { get; set; }

    public bool IsPublished { get; set; }

    public int VersionCount { get; set; }

    public virtual ICollection<ChapterComment> ChapterComments { get; set; } = new List<ChapterComment>();

    public virtual ICollection<ChapterContent> ChapterContents { get; set; } = new List<ChapterContent>();

    public virtual ChapterContent? PrimaryContent { get; set; }

    public virtual Story Story { get; set; } = null!;

    public virtual ICollection<UserChapterInteraction> UserChapterInteractions { get; set; } = new List<UserChapterInteraction>();
}
