using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Shared group-visibility predicate — conditionality kind (g), the parent-visibility invariant
/// (<c>identity-and-authorization.md</c> §"Parent-visibility guards", WU-ParentVisibility).
/// <para>
/// The <c>GroupAudience</c> filter is declared on <c>Group</c> only, and its own note reasons that
/// "child entities are unreachable once their parent group is filtered." That holds solely for
/// queries which actually traverse the <c>Group</c> navigation. Reads filtering on the bare
/// <c>GroupId</c> FK — the member roster, the group's blog posts, the group comment wall — never
/// expand <c>Group</c>, so EF emitted no join and the filter could not bite. An M-audience group's
/// roster and posts were anonymously readable with mature content off.
/// </para>
/// <para>
/// Membership is a separate, stricter question and is not this guard's job: joining, posting, and
/// admin actions keep their own membership/role checks. This answers only "may this viewer see that
/// the group and its contents exist."
/// </para>
/// </summary>
public static class GroupVisibilityGuard
{
    /// <summary>
    /// True when the group survives the viewer's audience filter, or is audience-gated only and the
    /// viewer holds a per-group reveal (one consent covers all group-owned content) or is a verified
    /// bot.
    /// </summary>
    public static async Task<bool> IsGroupVisibleAsync(
        ReadOnlyApplicationDbContext readDb, IActiveUserContext viewer, int groupId)
    {
        // Fast path: the filtered set applies GroupAudience.
        if (await readDb.Groups.AnyAsync(g => g.GroupId == groupId)) return true;

        bool exists = await readDb.Groups
            .IgnoreQueryFilters(["GroupAudience"]) // elevated read: audience decided post-load (reveal-aware)
            .AnyAsync(g => g.GroupId == groupId);

        if (!exists) return false;
        if (viewer.IsVerifiedBot) return true;

        return await RevealCheck.IsRevealedAsync(readDb, viewer, RevealedEntityType.Group, groupId);
    }
}
