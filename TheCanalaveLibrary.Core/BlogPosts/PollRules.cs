namespace TheCanalaveLibrary.Core;

/// <summary>
/// Dependency-free poll lifecycle/visibility rules (Feature 37). Lives in Core so both the server
/// services and unit tests share one implementation — parallel to <c>ChapterText.CountWords</c>.
/// </summary>
public static class PollRules
{
    /// <summary>Computed lifecycle state. Closed wins over Pending when both would apply.</summary>
    public static PollStatus StatusFor(DateTime dateOpenedUtc, DateTime? dateClosedUtc, DateTime nowUtc)
    {
        if (dateClosedUtc is DateTime closed && nowUtc >= closed) return PollStatus.Closed;
        if (nowUtc < dateOpenedUtc) return PollStatus.Pending;
        return PollStatus.Open;
    }

    /// <summary>
    /// Whether tallies are visible to this viewer. The owner (and moderators) always see results —
    /// they manage the poll. <c>AfterVote</c> is a pure function of *current* state: retracting a
    /// vote hides results again (settled 2026-07-12).
    /// </summary>
    public static bool ResultsVisible(
        PollResultsVisibility visibility,
        PollStatus status,
        bool viewerHasCurrentVote,
        bool viewerIsOwnerOrModerator)
    {
        if (viewerIsOwnerOrModerator) return true;
        return visibility switch
        {
            PollResultsVisibility.Always => true,
            PollResultsVisibility.AfterClose => status == PollStatus.Closed,
            PollResultsVisibility.AfterVote => viewerHasCurrentVote,
            _ => false,
        };
    }
}
