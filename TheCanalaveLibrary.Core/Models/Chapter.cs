using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core.Models;

public partial class Chapter
{
    public int ChapterId { get; set; }

    public int StoryId { get; set; }

    public int ChapterNumber { get; set; }

    [Required] [MaxLength(255)] public string Title { get; set; } = null!;

    public long PrimaryContentId { get; set; }

    public bool IsPublished { get; set; }
    
    public int VersionCount { get; set; }

    public virtual ICollection<ChapterComment> ChapterComments { get; set; } = new List<ChapterComment>();

    public virtual ICollection<ChapterContent> ChapterContents { get; set; } = new List<ChapterContent>();

    public virtual ChapterContent PrimaryContent { get; set; } = null!;

    public virtual Story.Story Story { get; set; } = null!;

    public virtual ICollection<UserChapterInteraction> UserChapterInteractions { get; set; } = new List<UserChapterInteraction>();
}
