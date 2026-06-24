namespace TheCanalaveLibrary.Core;

public partial class GroupComment : BaseComment
{
    public DateTime DatePosted { get; set; }

    public int GroupId { get; set; }

    public virtual Group Group { get; set; } = null!;
}
