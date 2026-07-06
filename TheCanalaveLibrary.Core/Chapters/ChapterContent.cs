using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

public partial class ChapterContent
{
    public long ChapterContentId { get; set; }

    public int ChapterId { get; set; }

    public int? AuthorId { get; set; }

    [MaxLength(100)]
    public string? VersionName { get; set; }

    public string? TopAuthorsNote { get; set; }

    [Required]
    public string ChapterText { get; set; } = null!;

    public string? BottomAuthorsNote { get; set; }

    public int WordCount { get; set; }

    // ViewCount dropped (R2): never written by any path; view counting is stories-only, accumulated
    // in daily_story_stats. Re-addable via a per-chapter daily-stat row if chapter views are ever wanted.
    public int SortOrder { get; set; }

    /// <summary>Nullable — null inherits the parent story's rating (spec §5.2).</summary>
    public Rating? Rating { get; set; }

    public DateTime PublishDate { get; set; }

    public DateTime? OriginalPublishDate { get; set; }

    public virtual User? Author { get; set; }

    public virtual Chapter Chapter { get; set; } = null!;
}
