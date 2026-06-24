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

    public int ViewCount { get; set; }
    
    public int SortOrder { get; set; }

    /// <summary>Nullable — null inherits the parent story's rating (spec §5.2).</summary>
    public Rating? Rating { get; set; }

    public DateTime PublishDate { get; set; }

    public DateTime? OriginalPublishDate { get; set; }

    public virtual User? Author { get; set; }

    public virtual Chapter Chapter { get; set; } = null!;
}
