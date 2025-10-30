using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class CommunitySpotlight
{
    public int SpotlightId { get; set; }

    public int StoryId { get; set; }

    public int? SponsoringUserId { get; set; }

    public string? SponsorComment { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public string? PaymentId { get; set; }

    public DateTime DateCreated { get; set; }

    public virtual User? SponsoringUser { get; set; }

    public virtual Story Story { get; set; } = null!;
}
