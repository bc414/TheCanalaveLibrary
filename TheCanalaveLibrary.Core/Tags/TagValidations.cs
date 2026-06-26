namespace TheCanalaveLibrary.Core;

/// <summary>
/// Pure domain validation rules for tag CRUD. No EF dependency — the service layer passes in
/// pre-fetched values (existing names, parent shape) so these methods are unit-testable directly.
/// </summary>
public static class TagValidations
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 512;
    public const int MaxSpriteIdentifierLength = 50;

    /// <summary>
    /// Validates <see cref="CreateTagDto"/> fields and throws <see cref="TagValidationException"/>
    /// on the first violation.
    /// </summary>
    /// <param name="dto">The DTO to validate.</param>
    /// <param name="nameExistsInType">
    /// <c>true</c> when a tag with the same <c>TagName</c> (case-insensitive) already exists for
    /// the same <c>TagTypeId</c> — the composite unique constraint expressed at the domain level.
    /// </param>
    /// <param name="parentTag">
    /// The resolved parent tag, when <c>dto.ParentTagId</c> is set. <c>null</c> when no parent.
    /// </param>
    public static void ValidateCreate(CreateTagDto dto, bool nameExistsInType, Tag? parentTag)
    {
        ValidateName(dto.TagName);
        ValidateDescription(dto.Description);
        ValidateSpriteIdentifier(dto.SpriteIdentifier);

        if (nameExistsInType)
            throw new TagValidationException(
                $"A {dto.TagTypeId} tag named \"{dto.TagName}\" already exists.");

        if (dto.ParentTagId is not null)
            ValidateParent(dto.ParentTagId.Value, editTagId: null, dto.TagTypeId, parentTag);
    }

    /// <summary>
    /// Validates <see cref="UpdateTagDto"/> fields and throws <see cref="TagValidationException"/>
    /// on the first violation.
    /// </summary>
    /// <param name="dto">The DTO to validate.</param>
    /// <param name="nameExistsInType">
    /// <c>true</c> when another tag (not this one) has the same <c>TagName</c> within the same
    /// <c>TagTypeId</c>.
    /// </param>
    /// <param name="parentTag">The resolved parent tag, or <c>null</c> when no parent is chosen.</param>
    public static void ValidateUpdate(UpdateTagDto dto, bool nameExistsInType, Tag? parentTag)
    {
        ValidateName(dto.TagName);
        ValidateDescription(dto.Description);
        ValidateSpriteIdentifier(dto.SpriteIdentifier);

        if (nameExistsInType)
            throw new TagValidationException(
                $"A {dto.TagTypeId} tag named \"{dto.TagName}\" already exists.");

        if (dto.ParentTagId is not null)
            ValidateParent(dto.ParentTagId.Value, editTagId: dto.TagId, dto.TagTypeId, parentTag);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new TagValidationException("Tag name is required.");

        if (name.Length > MaxNameLength)
            throw new TagValidationException(
                $"Tag name must be {MaxNameLength} characters or fewer.");
    }

    private static void ValidateDescription(string? description)
    {
        if (description is not null && description.Length > MaxDescriptionLength)
            throw new TagValidationException(
                $"Description must be {MaxDescriptionLength} characters or fewer.");
    }

    private static void ValidateSpriteIdentifier(string? identifier)
    {
        if (identifier is not null && identifier.Length > MaxSpriteIdentifierLength)
            throw new TagValidationException(
                $"Sprite identifier must be {MaxSpriteIdentifierLength} characters or fewer.");
    }

    /// <param name="parentTagId">The proposed parent ID.</param>
    /// <param name="editTagId">
    /// The ID of the tag being edited (<c>null</c> for create). Used to detect self-reference.
    /// </param>
    /// <param name="tagTypeId">The type of the tag being created/updated.</param>
    /// <param name="parentTag">The resolved parent tag (must be non-null when parentTagId is set).</param>
    private static void ValidateParent(int parentTagId, int? editTagId, TagTypeEnum tagTypeId, Tag? parentTag)
    {
        if (editTagId is not null && parentTagId == editTagId)
            throw new TagValidationException("A tag cannot be its own parent.");

        if (parentTag is null)
            throw new TagValidationException("The selected parent tag does not exist.");

        if (parentTag.TagTypeId != tagTypeId)
            throw new TagValidationException(
                $"Parent tag must be the same type ({tagTypeId}). " +
                $"Selected parent is {parentTag.TagTypeId}.");

        if (parentTag.ParentTagId is not null)
            throw new TagValidationException(
                "The selected parent tag already has a parent. " +
                "Tag hierarchy is limited to one level deep.");
    }

    /// <summary>
    /// Returns <c>false</c> for non-Character types — <c>AllowOCDetails</c> is only meaningful on
    /// Character tags. The service coerces the value before persisting.
    /// </summary>
    public static bool CoerceAllowOCDetails(bool value, TagTypeEnum tagTypeId) =>
        tagTypeId == TagTypeEnum.Character && value;

    /// <summary>
    /// Returns <c>false</c> for non-Setting types — <c>AllowSettingDetails</c> is only meaningful on
    /// Setting tags. The service coerces the value before persisting.
    /// </summary>
    public static bool CoerceAllowSettingDetails(bool value, TagTypeEnum tagTypeId) =>
        tagTypeId == TagTypeEnum.Setting && value;
}
