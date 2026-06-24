using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Data required to update an existing <c>ChapterContent</c> row (edit in place).
/// Does not change which version is primary — use <c>SetPrimaryVersionAsync</c> for that.
/// </summary>
public class UpdateChapterContentDto
{
    public long ChapterContentId { get; set; }

    /// <summary>
    /// Optional — null/blank leaves the chapter's current title unchanged.
    /// Max 255 characters.
    /// </summary>
    [MaxLength(255)]
    public string? Title { get; set; }

    /// <summary>Raw HTML from EditorView — the write service sanitizes before persisting.</summary>
    [Required]
    public string ChapterText { get; set; } = string.Empty;

    public string? TopAuthorsNote { get; set; }
    public string? BottomAuthorsNote { get; set; }

    /// <summary>Nullable — null means inherit the story's rating (spec §5.2).</summary>
    public Rating? Rating { get; set; }

    [MaxLength(100)]
    public string? VersionName { get; set; }
}
