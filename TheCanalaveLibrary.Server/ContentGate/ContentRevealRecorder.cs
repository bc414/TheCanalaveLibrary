using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Records a signed-in member's durable mature-content reveal — the one write behind
/// <c>POST /content-gate/reveal</c> (<see cref="ContentGateEndpoints"/>) and the single enforcement point
/// of the <see cref="WriteActionKind.Reveal"/> throttle (owner ruling D20, WU-ThrottleCoverage).
///
/// <para><b>Server-only, deliberately not on <see cref="IContentRevealService"/>.</b> The reveal is a
/// full-document form POST that ends in a redirect (the frozen-circuit rule in the endpoint class
/// doc); the circuit and WASM never call it, so HTTP is its only transport and this class is the whole
/// surface. Putting it on the WASM-registered interface would add a server-only method there.</para>
///
/// <para><b>Order</b> (security.md §"Write Throttling"): an undefined <see cref="RevealedEntityType"/>
/// is a no-op (no token spent); then the bucket; then the target check, where a target the viewer may
/// not see is a no-op exactly like a missing one; then the idempotent insert.</para>
///
/// <para><b>The target check is the viewer's confidentiality predicate, never ground truth</b>
/// (review fixes, 2026-10-13). It is <see cref="ServerContentRevealService.ResolveVisibleTitlesAsync"/>,
/// the lookup the <c>/settings</c> list titles its rows with: the rating/audience gate lifted (consent
/// is what a reveal records), story status, takedown and a profile post's own visibility rule kept. So
/// a row is minted exactly when the list can title it. Checked against the unfiltered write context,
/// another author's draft left a "(deleted story)" row while a missing id left none, and the list
/// answered "does hidden story N exist" for any N. A no-op is silent: the endpoint redirects either
/// way, and the legitimate interstitial only ever posts a defined type for a target its viewer can
/// reach.</para>
///
/// <para>One identity source: the bucket's user, the row's user and the viewer the predicate is
/// evaluated for are all <see cref="IActiveUserContext"/>.</para>
/// </summary>
public sealed class ContentRevealRecorder(
    ApplicationDbContext writeDb,
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser,
    IWriteRateLimitService rateLimit)
{
    /// <summary>
    /// Records the active user's reveal of the target. Idempotent.
    /// </summary>
    /// <exception cref="InvalidOperationException">The caller is anonymous (the endpoint keeps the
    /// anonymous reveal in the prefs cookie and never calls this).</exception>
    /// <exception cref="WriteRateLimitExceededException">The user's <see cref="WriteActionKind.Reveal"/>
    /// bucket is empty.</exception>
    public async Task RecordAsync(RevealedEntityType entityType, int entityId)
    {
        if (activeUser.UserId is not int userId)
            throw new InvalidOperationException("Recording a reveal requires an authenticated user.");

        if (!Enum.IsDefined(entityType))
            return;

        rateLimit.EnsureAllowed(WriteActionKind.Reveal, userId);

        await using (ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync())
        {
            Dictionary<(RevealedEntityType, int), string> visible =
                await ServerContentRevealService.ResolveVisibleTitlesAsync(readDb, activeUser, [(entityType, entityId)]);
            if (visible.Count == 0)
                return;
        }

        if (await writeDb.UserContentReveals.FindAsync(userId, entityType, entityId) is not null)
            return;

        writeDb.UserContentReveals.Add(new UserContentReveal
        {
            UserId = userId,
            EntityType = entityType,
            EntityId = entityId,
            DateRevealed = DateTime.UtcNow,
        });
        await writeDb.SaveChangesAsync();
    }
}
