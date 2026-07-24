using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// The one reveal predicate gated reads call (WU-AccessGate; content-safety.md §"The Three-Plane
/// Access Model"): anonymous viewers' reveals live in the prefs cookie (via
/// <see cref="IActiveUserContext.HasAnonRevealed"/>), signed-in viewers' in
/// <see cref="UserContentReveal"/> rows — the context is deliberately DbContext-free, so the DB
/// half composes here, in read-service scope. Reveals affect Direct-navigation-plane reads only;
/// Discovery-plane listings never consult this.
/// </summary>
public static class RevealCheck
{
    public static async Task<bool> IsRevealedAsync(
        ReadOnlyApplicationDbContext readDb,
        IActiveUserContext viewer,
        RevealedEntityType entityType,
        int entityId)
    {
        if (viewer.UserId is int userId)
        {
            return await readDb.UserContentReveals.AnyAsync(r =>
                r.UserId == userId && r.EntityType == entityType && r.EntityId == entityId);
        }

        return viewer.HasAnonRevealed(entityType, entityId);
    }
}
