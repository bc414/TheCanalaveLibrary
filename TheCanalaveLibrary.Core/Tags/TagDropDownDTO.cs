namespace TheCanalaveLibrary.Core.Tags;

/// <summary>
/// Used to populate the list of all tags for a user to select when choosing tags for a story or choosing tags
/// to filter their search results. It is as minimal as possible to minimize payload.
/// </summary>
public class TagDropDownDTO
{
    public int TagId { get; set; }
    public string TagName { get; set; }
}