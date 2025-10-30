using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class Group
{
    public int GroupId { get; set; }

    public int? CreatorId { get; set; }

    public string GroupName { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime DateCreated { get; set; }

    public virtual ICollection<BlogPost> BlogPosts { get; set; } = new List<BlogPost>();

    public virtual User? Creator { get; set; }

    public virtual ICollection<GroupComment> GroupComments { get; set; } = new List<GroupComment>();

    public virtual ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();

    public virtual ICollection<GroupStory> GroupStories { get; set; } = new List<GroupStory>();
}
