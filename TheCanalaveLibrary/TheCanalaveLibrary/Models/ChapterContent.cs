using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class ChapterContent
{
    public long ChapterContentId { get; set; }

    public int ChapterId { get; set; }

    public int? AuthorId { get; set; }

    public string? VersionName { get; set; }

    public string? TopAuthorsNote { get; set; }

    public string ChapterText { get; set; } = null!;

    public string? BottomAuthorsNote { get; set; }

    public int WordCount { get; set; }

    public int ViewCount { get; set; }

    public byte? Rating { get; set; }

    public DateTime PublishDate { get; set; }

    public DateTime? OriginalPublishDate { get; set; }

    public virtual User? Author { get; set; }

    public virtual Chapter Chapter { get; set; } = null!;

    public virtual ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
}
