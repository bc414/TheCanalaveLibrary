namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of the fanonization pipeline (WU-TagFanon Groups 7+8). Moderator half: link a
/// custom-name group to an official tag and invite the affected authors (never the same author
/// twice per tag). Author half: adopt per story or in bulk — always opt-in, never automatic —
/// and reversibly dismiss. Adoption mutates rows in place: naming moves to the tag
/// (IsOc→false, CustomName→null), Nuance and priority survive, the character row keeps its
/// stable id so pairings survive; collisions (story already carries the target) skip with an
/// explanation, never merge.
/// </summary>
public interface IFanonWriteService : IFanonReadService
{
    /// <summary>
    /// Moderator: link a (name, base tag) group to <c>TargetTagId</c> and notify the affected
    /// authors who have never been told about this tag. The target may be fanon or canon; create
    /// a new tag first via <see cref="ITagWriteService"/> when it doesn't exist yet. Throws
    /// <see cref="UnauthorizedAccessException"/> for non-mods; rejects a duplicate link for the
    /// same group.
    /// </summary>
    /// <returns>The number of authors notified.</returns>
    Task<int> LinkGroupAsync(FanonLinkCreateDto dto);

    /// <summary>
    /// Moderator: invite the group's newly-arrived authors (those with no adoption state for the
    /// link's target tag). Resolved by the group key — (normalized name, base tag) — the same
    /// identity the dashboard rows carry. Safe to repeat — the never-twice rule is enforced by
    /// <see cref="TagAdoptionState.DateNotified"/>, not by notification dedup.
    /// </summary>
    /// <returns>The number of authors newly notified.</returns>
    Task<int> NotifyNewAuthorsAsync(string name, int baseTagId);

    /// <summary>Author: adopt the target tag on ONE of their stories' matching rows.</summary>
    Task<AdoptResultDto> AdoptAsync(int targetTagId, int storyId);

    /// <summary>Author: adopt the target tag across all their matching rows.</summary>
    Task<AdoptResultDto> AdoptAllAsync(int targetTagId);

    /// <summary>Author: mark a pending adoption not-applicable (reversible — only the author
    /// knows whether their Saura is <i>that</i> Saura).</summary>
    Task SetDismissedAsync(int targetTagId, bool dismissed);
}
