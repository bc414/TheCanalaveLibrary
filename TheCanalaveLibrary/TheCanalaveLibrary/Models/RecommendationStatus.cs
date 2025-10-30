using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class RecommendationStatus
{
    public byte StatusId { get; set; }

    public string StatusName { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<Recommendation> Recommendations { get; set; } = new List<Recommendation>();
}
