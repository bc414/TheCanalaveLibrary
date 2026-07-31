namespace TheCanalaveLibrary.Core;

/// <summary>
/// Consent state of a <see cref="StoryAcknowledgment"/> — mirrors <see cref="StoryLineageStatus"/>'s
/// three-state shape exactly (WU-StatBadgeProducers). A row starts <see cref="Pending"/> when the
/// author credits someone else; the credited user must <see cref="Accepted">accept</see> before it
/// counts toward the beta-reader counter or badge. <see cref="Declined"/> rows are kept (not
/// deleted) so a re-credit reuses the composite-key row rather than duplicate-inserting.
/// </summary>
public enum StoryAcknowledgmentStatus : short
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
}
