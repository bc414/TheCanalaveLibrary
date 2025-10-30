using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class ChapterComment
{
    public long CommentId { get; set; }

    public int ChapterId { get; set; }

    public virtual Chapter Chapter { get; set; } = null!;

    public virtual BaseComment Comment { get; set; } = null!;
}
