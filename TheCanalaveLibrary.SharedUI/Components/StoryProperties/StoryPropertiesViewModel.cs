using System.ComponentModel.DataAnnotations;
using TheCanalaveLibrary.Core.Models;
using TheCanalaveLibrary.Core.Story;
using TheCanalaveLibrary.Core.Tags;

namespace TheCanalaveLibrary.SharedUI.Components.StoryProperties;

/// <summary>
/// A view model that represents the editable properties of a story,
/// designed to be used with a Blazor EditForm.
/// </summary>
public class StoryPropertiesViewModel : IEditableStoryProperties
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

    [Required(ErrorMessage = "A long description or summary is required.")]
    public string? LongDescription { get; set; }

    public StoryStatusEnum PostApprovalStatus { get; set; }

    public List<IStoryTag> StoryTags { get; set; } = new();

    // --- UI-Specific Properties ---
    public bool IsLoading { get; set; }
    public List<string> ServerValidationErrors { get; set; } = new();
}