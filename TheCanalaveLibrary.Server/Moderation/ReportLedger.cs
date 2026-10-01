using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Report-ledger housekeeping for targets destroyed outside moderation — owner ruling D7's sub-edge,
/// picked by WU-ModerationIntegrity (2026-09-30): close the reports <b>at the source</b>, in the
/// transaction that destroys their target, never by a later reconciler. Reports carry no FK to their
/// polymorphic target, so they outlive it; the queue drops rows whose target no longer materializes,
/// which leaves them Open, invisible and unresolvable — a zombie. (<c>layer2-services.md</c>
/// §"Moderation Services" → "Zombie reports — closed at the source".)
/// </summary>
internal static class ReportLedger
{
    /// <summary>
    /// Closes every Open/UnderReview report on the given <c>(type, id)</c> targets as
    /// <see cref="ReportStatusEnum.ResolvedNoAction"/> with a NULL moderator, <c>DateResolved = now</c>
    /// and <paramref name="note"/> as <c>ActionTaken</c>; returns the rows closed. Runs immediately (one
    /// <c>ExecuteUpdateAsync</c>), so call it inside the caller's transaction.
    /// <para><b>No notification and no counter call.</b> The counters die with their rows, and both
    /// outcome texts (81, 82) would claim a moderator review that never happened.
    /// <c>ResolvedNoAction</c> with a NULL moderator is the honest ledger entry: no moderation action
    /// happened, and <c>ResolvedActionTaken</c> would read as a prior sanction in the per-user history
    /// (D8's ban signal). The NULL moderator plus the note tell it apart from a moderator's "no".</para>
    /// </summary>
    public static async Task<int> CloseForDestroyedTargetsAsync(ApplicationDbContext db,
        ReportedEntityType type, IReadOnlyCollection<long> ids, string note)
    {
        if (ids.Count == 0) return 0;

        List<long> targetIds = [..ids];
        DateTime now = DateTime.UtcNow;
        return await db.Reports
            .Where(r => r.ReportedEntityType == type
                        && targetIds.Contains(r.ReportedEntityId)
                        && (r.ReportStatusId == ReportStatusEnum.Open
                            || r.ReportStatusId == ReportStatusEnum.UnderReview))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.ReportStatusId, ReportStatusEnum.ResolvedNoAction)
                .SetProperty(r => r.ModeratorUserId, (int?)null)
                .SetProperty(r => r.DateResolved, now)
                .SetProperty(r => r.ActionTaken, note));
    }
}
