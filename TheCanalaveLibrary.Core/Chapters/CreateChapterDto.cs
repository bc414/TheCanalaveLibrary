using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Data required to create a new chapter (first version) or an alternate version of an existing
/// chapter. <c>ChapterNumber</c> and <c>SortOrder</c> are assigned server-side.
/// </summary>
public class CreateChapterDto
{
    public int StoryId { get; set; }

    /// <summary>
    /// Optional — the service defaults to <c>"Chapter {N}"</c> when blank/null,
    /// satisfying the spec's nullable-title intent at the service layer (no schema change).
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
