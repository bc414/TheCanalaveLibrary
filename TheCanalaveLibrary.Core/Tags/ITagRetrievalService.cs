namespace TheCanalaveLibrary.Core.Tags;

public interface ITagRetrievalService
{
    Task<List<TagDropDownDTO>> GetTagsByTypeAsync(TagTypeEnum type);
    Task<List<TagDropDownDTO>> GetAllCharacterTagsAsync();
    Task<List<TagDropDownDTO>> GetAllSettingTagsAsync();
    Task<List<TagDropDownDTO>> GetAllGenreTagsAsync();
    Task<List<TagDropDownDTO>> GetAllContentWarningTagsAsync();
}