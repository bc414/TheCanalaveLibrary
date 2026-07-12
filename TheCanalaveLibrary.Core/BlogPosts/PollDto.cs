namespace TheCanalaveLibrary.Core;

/// <summary>Computed lifecycle state of a poll — never stored; derived from the date columns.</summary>
public enum PollStatus : short
{
    /// <summary>DateOpened is in the future — visible but not votable.</summary>
    Pending = 0,
    Open = 1,
    /// <summary>DateClosed has passed (scheduled or manual). Votes frozen.</summary>
    Closed = 2,
}

/// <summary>A voter shown in a poll's public voter list (Public / opted-in VoterChoice modes).</summary>
public record PollVoterDto(int UserId, string UserName);

/// <summary>
/// One option with its tally. When the poll's results are not visible to the viewer
/// (<see cref="PollDto.ResultsVisibleToViewer"/> false), <see cref="VoteCount"/> is zeroed and
/// <see cref="PublicVoters"/> emptied server-side — visibility is enforced at the service, not the UI.
/// </summary>
public record PollOptionResultDto(
    int PollOptionId,
    string Text,
    int SortOrder,
    int VoteCount,
    PollVoterDto[] PublicVoters);

/// <summary>
/// Full display projection of a poll for any viewer. Viewer-relative fields
/// (<see cref="ViewerVotedOptionIds"/>, <see cref="ResultsVisibleToViewer"/>) are computed
/// server-side against <c>IActiveUserContext</c>.
/// </summary>
public record PollDto(
    int PollId,
    string PollName,
    string? Description,
    DateTime DateOpened,
    DateTime? DateClosed,
    bool AllowMultiple,
    PollResultsVisibility ResultsVisibility,
    PollAnonymityMode AnonymityMode,
    int OwnerId,
    string? OwnerUserName,
    bool IsArchived,           // SitePoll only; always false for blog-post polls
    int? BlogPostId,           // BlogPostPoll only; null for site polls
    PollStatus Status,
    bool ResultsVisibleToViewer,
    bool ConfigLocked,         // true once any vote exists — AllowMultiple/ResultsVisibility/AnonymityMode frozen
    int[] ViewerVotedOptionIds,
    bool ViewerVotedAnonymously,   // VoterChoice mode: the viewer's own current opt-out flag
    int TotalVoterCount,
    PollOptionResultDto[] Options);
