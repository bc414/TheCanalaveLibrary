using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core.Models;

public abstract class BasePoll
{
    [Key]
    public int PollId { get; set; }
    
    public int OwnerId { get; set; }

    [Required] [MaxLength(256)] public string PollName { get; set; } = null!;
    
    [MaxLength(2048)]
    public string? Description { get; set; }
    
    public DateTime DateOpened { get; set; }
    
    public DateTime DateClosed { get; set; }

    public User Owner { get; set; } = null!;

    public ICollection<PollOption> PollOptions { get; set; } = new List<PollOption>();
}