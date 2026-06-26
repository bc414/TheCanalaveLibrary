using System.ComponentModel.DataAnnotations;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.SharedUI;

/// <summary>
/// View model for StoryPropertiesForm. Carries UI-only state (IsLoading, ServerValidationErrors)
/// and shields the form from the wire DTOs' server-only fields (AuthorId, StoryId, Slug).
/// Structured tag collections are built by the form on every selection change and read by the
/// page on submit. Does not implement IEditableStoryProperties: mapping lives at the page layer.
/// </summary>
public class StoryPropertiesViewModel
{
    [Required(ErrorMessage = "Title is required")]
    [MaxLength(255, ErrorMessage = "Title cannot exceed 255 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "A short description is required.")]
    [MaxLength(500, ErrorMessage = "Short description cannot exceed 500 characters.")]
    public string? ShortDescription { get; set; }

    public Rating Rating { get; set; }

    public StoryStatusEnum StoryStatusId { get; set; }

    [MaxLength(512)]
    public string? CoverArtRelativeUrl { get; set; }

    /// <summary>
    /// Populated from EditorView.GetHtmlAsync() by the page before mapping to the DTO.
    /// Not bound via two-way binding — EditorView uses pull-on-submit.
    /// </summary>
    public string? LongDescription { get; set; }

    public StoryStatusEnum PostApprovalStatus { get; set; }

    /// <summary>
    /// Flat tag associations (Genre/ContentWarning/CrossoverFandom/Setting) with correct priorities.
    /// Built by the form on every selection change; read by the page when building the write DTO.
    /// </summary>
    public List<StoryTagDTO> SelectedFlatTags { get; set; } = new();

    public List<StoryCharacterDto> SelectedCharacters { get; set; } = new();
    public List<SettingDetailDto> SelectedSettingDetails { get; set; } = new();
    public List<StoryCharacterPairingDto> SelectedPairings { get; set; } = new();

    public bool IsLoading { get; set; }
    public List<string> ServerValidationErrors { get; set; } = new();
}
