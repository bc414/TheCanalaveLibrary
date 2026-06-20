namespace TheCanalaveLibrary.Core;

public class ProfileBlogPost : BaseBlogPost
{
    public int? StoryId { get; set; }
    
    public virtual Story? Story { get; set; }
    
    public bool HasSpoilers { get; set; }
}