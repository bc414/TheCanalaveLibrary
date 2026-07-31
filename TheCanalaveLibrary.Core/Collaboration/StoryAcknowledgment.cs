namespace TheCanalaveLibrary.Core;

/// <summary>
/// An author's credit to another registered user for helping with a story — e.g. beta reading,
/// planning, cover art, editing, or inspiration (Feature 22/50, WU-StatBadgeProducers). Consent-
/// gated: the credited user must <see cref="StoryAcknowledgmentStatus.Accepted">accept</see> before
/// it counts toward <c>UserStat.AcknowledgedAsBetaReaderCount</c> or feeds the <c>BetaReader</c>
/// badge — this is what keeps the credit from being farmable by two colluding accounts.
/// Distinct from <see cref="BetaReader"/>, which is draft-access *authorization*, not credit.
/// </summary>
public partial class StoryAcknowledgment
{
    public int StoryId { get; set; }

    public int AcknowledgedUserId { get; set; }

    public short AcknowledgmentRoleId { get; set; }

    public StoryAcknowledgmentStatus StatusId { get; set; }

    public DateTime DateAcknowledged { get; set; }

    public DateTime? DateResponded { get; set; }

    public virtual User AcknowledgedUser { get; set; } = null!;

    public virtual AcknowledgmentRole AcknowledgmentRole { get; set; } = null!;

    public virtual Story Story { get; set; } = null!;
}
