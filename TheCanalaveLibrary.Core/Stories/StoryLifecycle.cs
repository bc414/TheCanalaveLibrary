namespace TheCanalaveLibrary.Core;

/// <summary>
/// The story status transition table (owner ruling D1, built WU-StoryLifecycle 2026-09-30) as a
/// pure function the server applies — rule of record: <c>layer2-services.md</c> §"Story Lifecycle".
/// <list type="bullet">
/// <item><b>Published set</b> = <c>InProgress(2)..OpenBeta(7)</c> — every status that means "the
/// author published this".</item>
/// <item><b>Entry set</b> = <c>{InProgress, Completed, OpenBeta}</c> — the statuses a story may be
/// published <i>as</i>, checked at submit and again at moderator approve.</item>
/// <item><b>Trusted</b> = <c>ApprovedStorySubmissions &gt;= 1 &amp;&amp; CanAutoApprove</c>. The
/// trust waiver is routing, not a second action: a trusted author's submit lands at
/// <c>PostApprovalStatus</c> instead of <c>PendingApproval</c>.</item>
/// </list>
/// Moderator-only transitions (approve, reject) are not in this table — they live in
/// <c>ServerModerationWriteService</c> and act only on <c>PendingApproval</c>.
/// </summary>
public static class StoryLifecycle
{
    /// <summary>Shared with <see cref="StoryValidations.CanSubmitForApproval"/> so the submit gate
    /// reads the same wherever it fires.</summary>
    public const string PostApprovalStatusRequiredMessage =
        "You must select a Status for the story to move to once approved.";

    /// <summary>True for every status that means the story is publicly published (2..7).</summary>
    public static bool IsPublished(StoryStatusEnum status) =>
        status is >= StoryStatusEnum.InProgress and <= StoryStatusEnum.OpenBeta;

    /// <summary>True for the statuses a story may be published as (the entry set).</summary>
    public static bool IsEntryStatus(StoryStatusEnum status) =>
        status is StoryStatusEnum.InProgress or StoryStatusEnum.Completed or StoryStatusEnum.OpenBeta;

    /// <summary>
    /// Resolves an author-requested status move. Returns the status the story actually lands on
    /// (which differs from <paramref name="target"/> only for a trusted author's submit), or the
    /// user-facing reasons it is refused. A same-status request is a no-op that returns
    /// <paramref name="current"/>.
    /// </summary>
    public static StoryTransitionResult ResolveAuthorTransition(
        StoryStatusEnum current, StoryStatusEnum target, StoryStatusEnum postApprovalStatus, bool trusted)
    {
        if (!Enum.IsDefined(target))
            return StoryTransitionResult.Refused("That isn't a valid story status.");

        if (target == current)
            return StoryTransitionResult.Allowed(current);

        // Submit (Draft → PendingApproval). The waiver routes a trusted author straight to the
        // status they chose; everyone else enters the moderator queue.
        if (current == StoryStatusEnum.Draft && target == StoryStatusEnum.PendingApproval)
        {
            if (!IsEntryStatus(postApprovalStatus))
                return StoryTransitionResult.Refused(PostApprovalStatusRequiredMessage);
            return StoryTransitionResult.Allowed(trusted ? postApprovalStatus : StoryStatusEnum.PendingApproval);
        }

        // Withdraw (PendingApproval → Draft) and revise (Rejected → Draft, uncapped).
        if (target == StoryStatusEnum.Draft &&
            current is StoryStatusEnum.PendingApproval or StoryStatusEnum.Rejected)
            return StoryTransitionResult.Allowed(StoryStatusEnum.Draft);

        // Moves among the published set, and unpublish (published → Draft).
        if (IsPublished(current) && (IsPublished(target) || target == StoryStatusEnum.Draft))
            return StoryTransitionResult.Allowed(target);

        return StoryTransitionResult.Refused(IllegalMoveMessage(current, target));
    }

    private static string IllegalMoveMessage(StoryStatusEnum current, StoryStatusEnum target)
    {
        if (target == StoryStatusEnum.Rejected)
            return "Only a moderator can reject a story.";
        if (current == StoryStatusEnum.PendingApproval && IsPublished(target))
            return "A story awaiting review is published by a moderator's approval.";
        if (current == StoryStatusEnum.Draft && IsPublished(target))
            return "Submit the story for publication instead.";
        if (current == StoryStatusEnum.Rejected)
            return "Return a rejected story to draft before submitting it again.";
        return $"A story can't move from {current} to {target}.";
    }
}

/// <summary>Outcome of <see cref="StoryLifecycle.ResolveAuthorTransition"/>: the resulting status
/// when <see cref="Errors"/> is empty, otherwise the user-facing refusal reasons.</summary>
public sealed record StoryTransitionResult(StoryStatusEnum ResultingStatus, IReadOnlyList<string> Errors)
{
    public bool IsAllowed => Errors.Count == 0;

    public static StoryTransitionResult Allowed(StoryStatusEnum status) => new(status, []);

    public static StoryTransitionResult Refused(string error) => new(default, [error]);
}
