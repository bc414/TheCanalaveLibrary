using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.SharedUI;

/// <summary>
/// View model for <see cref="SiteAnnouncementPropertiesForm"/> — the <c>SiteBlogPost</c>
/// counterpart to <see cref="BlogPostPropertiesViewModel"/>, kept separate rather than adding a
/// conditional-field mode to that form (no Rating/HasSpoilers/story-picker; adds
/// <see cref="NotifyAllUsers"/>). Mirrors its shielding purpose: carries UI-only state
/// (<c>IsLoading</c>, <c>ServerValidationErrors</c>) and keeps the form bUnit-testable with no
/// dependencies. The page owns the ViewModel↔DTO mapping and the EditorView pull-on-submit call.
/// </summary>
public class SiteAnnouncementPropertiesViewModel
{
    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(256, ErrorMessage = "Title cannot exceed 256 characters.")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Tracks the draft/publish toggle. Unlike <see cref="BlogPostPropertiesViewModel.IsPublished"/>
    /// (which is always <c>false</c> on create — profile posts are drafts by default), a site
    /// announcement may be created already-published; the moderator chooses via this toggle.
    /// </summary>
    public bool IsPublished { get; set; }

    /// <summary>
    /// Fan the SiteAnnouncement notification out to every user once this post publishes. Fires
    /// at most once per post regardless of how many times this stays checked across edits — see
    /// <c>SiteBlogPost.NotifiedAtUtc</c>.
    /// </summary>
    public bool NotifyAllUsers { get; set; }

    /// <summary>
    /// Populated from <c>EditorView.GetHtmlAsync()</c> by the page before mapping to the DTO.
    /// Not bound via two-way binding — EditorView uses pull-on-submit (layer3-logic.md
    /// §"EditorView Pull-on-Submit").
    /// </summary>
    public string? Content { get; set; }

    public bool IsLoading { get; set; }
    public List<string> ServerValidationErrors { get; set; } = new();
}
