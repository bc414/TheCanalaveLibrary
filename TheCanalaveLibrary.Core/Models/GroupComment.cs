namespace TheCanalaveLibrary.Core.Models;

public partial class GroupComment : BaseComment
{
    public int GroupId { get; set; }

    public virtual Group Group { get; set; } = null!;
}
