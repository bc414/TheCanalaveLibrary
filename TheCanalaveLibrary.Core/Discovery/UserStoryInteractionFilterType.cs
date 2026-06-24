using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Catalog of filterable interaction kinds — one entry per UserStoryInteraction boolean column
/// that can be used to exclude stories from search results. Examples: Ignored, Completed,
/// HasStarted, ReadItLater, Favorited, HiddenFavorited, Followed.
/// Not per-user data — this is the system catalog; per-user overrides live in
/// <see cref="UserStoryInteractionFilterSetting"/>.
/// </summary>
public partial class UserStoryInteractionFilterType
{
    [Key]
    [Required]
    [MaxLength(50)]
    public string UserStoryInteractionFilterKey { get; set; } = null!;

    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = null!;

    [MaxLength(2048)]
    public string? Description { get; set; }

    public virtual ICollection<DefaultUserStoryInteractionFilterSetting> DefaultUserStoryInteractionFilterSettings { get; set; } = new List<DefaultUserStoryInteractionFilterSetting>();

    public virtual ICollection<UserStoryInteractionFilterSetting> UserStoryInteractionFilterSettings { get; set; } = new List<UserStoryInteractionFilterSetting>();
}
