using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

public partial class CustomList
{
    public int CustomListId { get; set; }

    public int UserId { get; set; }

    [Required]
    [MaxLength(256)]
    public string ListName { get; set; } = null!;

    public bool IsPublic { get; set; }

    public DateTime DateCreated { get; set; }

    public virtual ICollection<CustomListEntry> CustomListEntries { get; set; } = new List<CustomListEntry>();

    public virtual User User { get; set; } = null!;
}
