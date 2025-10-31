using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Models;

public class NotificationCategory
{
    [Key]
    public byte NotificationCategoryId { get; set; }

    [Required]
    [MaxLength(100)]
    public string CategoryName { get; set; } = null!;

    public string Description { get; set; } = null!;

    public int SortOrder { get; set; }

    // --- Navigation Property ---
    // A category has many types (e.g., "Story Alerts" has "NewChapter", "NewFavorite", etc.)
    public virtual ICollection<NotificationType> NotificationTypes { get; set; } = new List<NotificationType>();
}