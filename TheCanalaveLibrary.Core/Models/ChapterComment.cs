namespace TheCanalaveLibrary.Core;

public partial class ChapterComment : BaseComment
{
    public int ChapterId { get; set; }

    public virtual Chapter Chapter { get; set; } = null!;
}
