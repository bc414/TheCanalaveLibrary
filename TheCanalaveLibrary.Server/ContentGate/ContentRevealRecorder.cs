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
/// is a no-op (no token spent); then the bucket; then an existence check against ground truth (the
/// unfiltered write context — a reveal is consent, so a target the viewer cannot see yet still counts),
/// where a missing target is a no-op; then the idempotent insert. A no-op is silent on purpose: the
/// endpoint redirects either way, so the response is no existence oracle, and the legitimate
/// interstitial only ever posts a defined type for a real id. The type → set mapping mirrors
/// <see cref="ServerContentRevealService.GetMyRevealsAsync"/>: a profile post is a
/// <see cref="RevealedEntityType.BlogPost"/>; a group post reveals its group.</para>
/// </summary>
public sealed class ContentRevealRecorder(ApplicationDbContext writeDb, IWriteRateLimitService rateLimit)
{
    /// <summary>
    /// Records the reveal for <paramref name="userId"/> (the caller's authenticated id). Idempotent.
    /// </summary>
    /// <exception cref="WriteRateLimitExceededException">The user's <see cref="WriteActionKind.Reveal"/>
    /// bucket is empty.</exception>
    public async Task RecordAsync(int userId, RevealedEntityType entityType, int entityId)
    {
        if (!Enum.IsDefined(entityType))
            return;

        rateLimit.EnsureAllowed(WriteActionKind.Reveal, userId);

        bool targetExists = entityType switch
        {
            RevealedEntityType.Story => await writeDb.Stories.AnyAsync(s => s.StoryId == entityId),
            RevealedEntityType.Group => await writeDb.Groups.AnyAsync(g => g.GroupId == entityId),
            RevealedEntityType.BlogPost => await writeDb.ProfileBlogPosts.AnyAsync(p => p.BlogPostId == entityId),
            _ => false,
        };
        if (!targetExists)
            return;

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
