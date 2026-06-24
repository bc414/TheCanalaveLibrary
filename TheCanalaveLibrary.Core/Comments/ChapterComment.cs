namespace TheCanalaveLibrary.Core;

public partial class ChapterComment : BaseComment
{
    public DateTime DatePosted { get; set; }

    public int ChapterId { get; set; }

    /// <summary>
    /// Marks comments containing spoilers for future chapters (§5.9.1). Chapter-scoped only —
    /// not on <see cref="BaseComment"/> because spoilers are a chapter-discussion concept that
    /// doesn't apply to profile or group comments.
    /// </summary>
    public bool IsSpoiler { get; set; }

    public virtual Chapter Chapter { get; set; } = null!;
}
