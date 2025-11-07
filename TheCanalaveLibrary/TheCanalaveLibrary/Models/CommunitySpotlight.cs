using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Models;

public partial class CommunitySpotlight
{
    [Key]
    public int SpotlightId { get; set; }

    public int StoryId { get; set; }

    public int? SponsoringUserId { get; set; }
    
    [MaxLength(512)]
    public string? SponsorComment { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    [MaxLength(2048)]
    public string? PaymentId { get; set; }

    public DateTime DateCreated { get; set; }

    public virtual User? SponsoringUser { get; set; }

    public virtual Story Story { get; set; } = null!;
}
