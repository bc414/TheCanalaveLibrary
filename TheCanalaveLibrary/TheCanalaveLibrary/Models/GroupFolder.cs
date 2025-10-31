using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Models;

public class GroupFolder
{
    public int GroupFolderId { get; set; }
    
    public int GroupId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = null!;
    
    public Rating MaxRating { get; set; }
    
    public int SortOrder { get; set; }
    
    public int? ParentFolderId { get; set; }

    public virtual Group Group { get; set; } = null!;
    
    public virtual ICollection<GroupStory> GroupStories { get; set; } = new List<GroupStory>();

}