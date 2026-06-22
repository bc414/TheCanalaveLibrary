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
}