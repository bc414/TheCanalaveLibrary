using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core.Models;

public class Theme
{
    public int ThemeId { get; set; }

    [Required] [MaxLength(100)] public string Name { get; set; } = null!;

    [Required] [MaxLength(512)]
    public string Description { get; set; } = null!;
}