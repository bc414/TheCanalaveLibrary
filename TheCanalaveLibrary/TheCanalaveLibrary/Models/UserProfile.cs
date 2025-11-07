using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Models;

public class UserProfile
{
    [Key]
    public int UserId { get; set; }
    public string? Text { get; set; } = null!;

    public User User { get; set; } = null!;
}