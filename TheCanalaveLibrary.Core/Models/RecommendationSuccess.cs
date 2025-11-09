namespace TheCanalaveLibrary.Core.Models;

public partial class RecommendationSuccess
{
    public int UserId { get; set; }

    public int RecommendationId { get; set; }

    public DateTime DateRecorded { get; set; }

    public virtual Recommendation Recommendation { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
