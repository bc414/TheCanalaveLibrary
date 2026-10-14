namespace TheCanalaveLibrary.Core;

public interface ITagReadService
{
    Task<List<TagDropDownDTO>> GetTagsByTypeAsync(TagTypeEnum type);
    Task<List<TagDropDownDTO>> GetAllCharacterTagsAsync();
    Task<List<TagDropDownDTO>> GetAllSettingTagsAsync();
    Task<List<TagDropDownDTO>> GetAllGenreTagsAsync();
    Task<List<TagDropDownDTO>> GetAllContentWarningTagsAsync();

    /// <summary>
    /// Per-keystroke typeahead source for TagSelector. Capped, case-insensitive name match within
    /// one TagTypeEnum. Returns render-ready TagChipDto (not the lean TagDropDownDTO) so dropdown
    /// rows and selected chips can show type color + sprite without a second round trip.
    /// </summary>
    Task<List<TagChipDto>> SearchTagChipsAsync(TagTypeEnum type, string term);

    /// <summary>
    /// Bulk chip lookup by exact ID — used by the story editor page to prefill TagSelectors from a
    /// story's saved tags (which carry TagId but not display data). Order matches the input list.
    /// IDs not found are silently dropped.
    /// </summary>
    Task<List<TagChipDto>> GetTagChipsByIdsAsync(IReadOnlyList<int> tagIds);

    /// <summary>
    /// Full tag directory for the <c>/tags</c> browse + mod-edit page. Returns all tags grouped
    /// by <see cref="TagTypeEnum"/> (in enum declaration order), with top-level parents as nodes
    /// and their direct children nested inside each node. Both parents and children carry
    /// <see cref="TagChipDto"/>s with the raw <c>SpriteIdentifier</c>; the rendering component
    /// resolves the sprite URL for the viewer's theme (<c>layer2-services.md</c> §"Sprite URLs Are
    /// Resolved At Render Time, In the Component").
    /// Distinct from <see cref="SearchTagChipsAsync"/> (per-keystroke, capped) and
    /// <see cref="GetTagChipsByIdsAsync"/> (prefill by ID).
    /// </summary>
    Task<List<TagDirectoryGroupDto>> GetTagDirectoryAsync();
}