namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of the Tags service contract. Inherits the read interface (CQRS-lite).
/// <para>
/// All mutation methods are gated to moderators and admins
/// (<see cref="IActiveUserContext.IsModerator"/> || <see cref="IActiveUserContext.IsAdmin"/>).
/// </para>
/// </summary>
public interface ITagWriteService : ITagReadService
{
    /// <summary>
    /// Creates a new tag. Validates name uniqueness within type, parent constraints, and
    /// coerces <c>AllowOCDetails</c> to <c>false</c> for non-Character types.
    /// </summary>
    /// <returns>
    /// A <see cref="TagSaveResult"/> with the new <c>TagId</c> and an optional
    /// <see cref="TagSaveResult.SpriteWarning"/> if no sprite asset was found for the given
    /// identifier (non-blocking advisory — save still succeeded).
    /// </returns>
    /// <exception cref="TagValidationException">Validation fails.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not a moderator or admin.</exception>
    Task<TagSaveResult> CreateTagAsync(CreateTagDto dto);

    /// <summary>
    /// Updates an existing tag's fields. Validates name uniqueness within type (excluding self),
    /// parent constraints, and coerces <c>AllowOCDetails</c> to <c>false</c> for non-Character types.
    /// </summary>
    /// <returns>
    /// An optional <see cref="TagSaveResult.SpriteWarning"/> advisory message, or <c>null</c> if
    /// the sprite identifier is absent or the asset was found. Save always succeeded if no exception
    /// was thrown.
    /// </returns>
    /// <exception cref="TagValidationException">Validation fails.</exception>
    /// <exception cref="KeyNotFoundException">Tag not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not a moderator or admin.</exception>
    Task<string?> UpdateTagAsync(UpdateTagDto dto);

    /// <summary>
    /// Hard-deletes a tag. Blocked if the tag is referenced by any <c>StoryTag</c>,
    /// <c>SavedTagSelectionEntry</c>, or has child tags — throws <see cref="TagValidationException"/>
    /// so the Restrict FK never fires.
    /// </summary>
    /// <exception cref="TagValidationException">Tag is in use and cannot be deleted.</exception>
    /// <exception cref="KeyNotFoundException">Tag not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not a moderator or admin.</exception>
    Task DeleteTagAsync(int tagId);
}
