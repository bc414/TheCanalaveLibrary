using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

public partial class TagType
{
    public TagTypeEnum TagTypeId { get; set; }

    [Required]
    [MaxLength(50)]
    public string TypeName { get; set; } = null!;

    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
