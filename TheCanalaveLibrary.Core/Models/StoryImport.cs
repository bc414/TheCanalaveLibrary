using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

public partial class StoryImport
{
    [Key]
    public int ImportId { get; set; }

    public int StoryId { get; set; }

    [Required]
    [MaxLength(255)]
    public string SourcePlatform { get; set; } = null!;

    [Required]
    [MaxLength(2048)]
    public string SourceUrl { get; set; } = null!;

    public short VerificationStatus { get; set; }

    public DateTime DateImported { get; set; }

    public virtual Story Story { get; set; } = null!;
}
