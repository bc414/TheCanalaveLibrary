using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core.Models;

public class PollOption
{
    public int PollOptionId { get; set; }

    [Required] [MaxLength(2048)] public string Text { get; set; } = null!;
    
    public int SortOrder { get; set; }

    public int PollId { get; set; }

    public BasePoll Poll { get; set; } = null!;

    public ICollection<User> Voters = new List<User>();
}