using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side write implementation. Inherits the read path via primary-constructor chaining.
/// Applies the six panel-managed bits in a single upsert: load→decide (the D6 raise guard)→apply→
/// stamp dates→sparse cleanup→save. HasStarted is never touched — it belongs to the reading path
/// (WU26). Any later step that writes (e.g. an ensure-row insert) belongs after the decide step.
/// <para><b>Recommendation attribution (owner ruling D3, WU-InertFeatures).</b> The attribution row
/// hangs off the interaction row and describes how its <c>IsReadItLater</c> bit was set. Two producers
/// live here — <see cref="SetReadItLaterFromRecommendationAsync"/> (the rec card) and
/// <see cref="MarkStartedAsync"/>'s direct-link parameter — each writing the parent and the attribution
/// in one save; and the panel upsert deletes it when the bit goes true→false. Shared rules:
/// <see cref="RecommendationAttribution"/>; doctrine: <c>layer2-services.md</c> §"Attribution
/// (Feature 30)".</para>
/// </summary>
public class ServerUserStoryInteractionWriteService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser)
    : ServerUserStoryInteractionReadService(readDbFactory, activeUser), IUserStoryInteractionWriteService
{
    /// <summary>
    /// Kind (g) — applied to <b>raises</b> only (owner ruling D6, WU-AccessGateSweep2). A raise needs
    /// the guard because these writes once had no parent check of any kind — not even existence,
    /// since the FK was the only guard: marking a guessed draft/M-unrevealed/taken-down story as a
    /// favorite increments the <b>story author's</b> <c>UserStats.FavoritesOnStories</c> (a non-owner
    /// write to another user's public counter) and silently enrolls the actor in that story's
    /// notification fan-out sets, self-subscribing them to content they were never allowed to see.
    /// <para>
    /// A clear does neither — it withdraws the caller's own row, lowers a counter and leaves a set —
    /// so clears on an existing row are never guarded, on any axis (rating, status, takedown). The
    /// read plane already tells the caller which hidden stories it holds rows on
    /// (<c>GetStatesByStoryIdsAsync</c> is a bare-FK read), so refusing the clear protected nothing
    /// and cost the user their own data. <see cref="MarkStartedAsync"/>/<see cref="MarkCompletedAsync"/>
    /// only ever set bits, so they are raises by construction and keep the unconditional guard.
    /// Rule: <c>identity-and-authorization.md</c> §"Parent-visibility guards" → "Raises vs clears".
    /// </para>
    /// </summary>
    private async Task RequireStoryVisibleAsync(int storyId)
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
        if (!await StoryVisibilityGuard.IsStoryVisibleAsync(readDb, ActiveUser, storyId))
            throw new KeyNotFoundException($"Story {storyId} not found.");
    }

    public async Task SetUserStoryInteractionStateAsync(int storyId, UserStoryInteractionStateUpdate update)
    {
        if (CurrentUserId is not int userId)
            throw new InvalidOperationException("This operation requires an authenticated user.");

        // Empty extension point — spec §4's zero-coupling model forbids no combination today.
        ValidateCombination(update);

        // ── 1. Load first (D6: load, diff, then decide) ──────────────────────────────
        // The tracked row + its date partition + its attribution (D3 trigger 1 below); null when the
        // caller has never touched this story.
        UserStoryInteraction? row = await writeDb.UserStoryInteractions
            .Include(i => i.InteractionDatePartition)
            .Include(i => i.RecommendationSource)
            .FirstOrDefaultAsync(i => i.UserId == userId && i.StoryId == storyId);

        // No row + all-false: nothing to clear and nothing to raise. Returns BEFORE any guard, so
        // a hidden, an absent and a nonexistent story are indistinguishable (no existence oracle).
        if (row is null && !AnyBitTrue(update))
            return;

        // ── 2. Decide: any raise anywhere guards the whole call ──────────────────────
        // No per-bit partial application — the guard runs before the first property is assigned,
        // so a refused mixed payload leaves the row exactly as it was. Pure clears (including the
        // all-false sparse cleanup below) never reach the guard.
        if (IsRaise(row, update))
            await RequireStoryVisibleAsync(storyId);

        // ── 3. Mutate — every write below this line is authorized ────────────────────
        if (row is null)
        {
            row = new UserStoryInteraction { UserId = userId, StoryId = storyId };
            writeDb.UserStoryInteractions.Add(row);
        }

        // Capture derived state BEFORE applying the update (transition-delta rule —
        // layer2-services.md §"Transition-delta rule for UserStoryInteraction-derived counters").
        bool wasFavorite    = row?.IsFavorite  ?? false;
        bool wasCompleted   = row?.IsCompleted ?? false;
        bool wasIgnored     = row?.IsIgnored   ?? false;
        bool hadStarted     = row?.HasStarted  ?? false;
        bool wasInProgress  = hadStarted && !wasCompleted;
        bool wasReadItLater = row?.IsReadItLater ?? false;

        // Apply the six panel bits — HasStarted is intentionally untouched.
        DateTime now = DateTime.UtcNow;
        EnsureDatePartition(row!, now, update);

        row!.IsFavorite = update.IsFavorite;
        row.IsHiddenFavorite = update.IsHiddenFavorite;
        row.IsFollowed = update.IsFollowed;
        row.IsCompleted = update.IsCompleted;
        row.IsReadItLater = update.IsReadItLater;
        row.IsIgnored = update.IsIgnored;

        // Stamp / clear dates on the date partition.
        if (row.InteractionDatePartition is { } d)
        {
            d.FavoriteDate = update.IsFavorite ? (d.FavoriteDate ?? now) : null;
            d.HiddenFavoriteDate = update.IsHiddenFavorite ? (d.HiddenFavoriteDate ?? now) : null;
            d.FollowedDate = update.IsFollowed ? (d.FollowedDate ?? now) : null;
            d.CompletedDate = update.IsCompleted ? (d.CompletedDate ?? now) : null;
            d.ReadItLaterDate = update.IsReadItLater ? (d.ReadItLaterDate ?? now) : null;
            d.IgnoredDate = update.IsIgnored ? (d.IgnoredDate ?? now) : null;
        }

        // D3 removal trigger 1: the attribution is metadata on the IsReadItLater bit, so un-saving the
        // story deletes it in the same unit of work. A clear — never guarded (D6). An attribution that
        // exists while the bit was never set (a direct-link reader) is untouched by unrelated toggles.
        if (wasReadItLater && !update.IsReadItLater && row.RecommendationSource is { } attribution)
        {
            writeDb.UserStoryRecommendationSources.Remove(attribution);
        }

        // Sparse cleanup: if all bits (including the read-only HasStarted) are false, remove the row
        // (its attribution, if any, goes with it — FK cascade, D3 trigger 2).
        if (!row.HasStarted && !AnyBitTrue(update))
        {
            writeDb.UserStoryInteractions.Remove(row);
        }

        await writeDb.SaveChangesAsync();

        // ── Transition-delta UserStats updates ───────────────────────────────────
        // Counter moves only when the effective derived state *flips* (layer2-services.md
        // §"Transition-delta rule for UserStoryInteraction-derived counters"). A clear on a hidden
        // story moves counters like any other clear — the favorite is genuinely withdrawn (D6's
        // accepted counter consequence).

        // FavoritesOnStories → story author's stat
        bool willBeFavorite = update.IsFavorite;
        if (willBeFavorite != wasFavorite)
        {
            // Anonymous-type projection so a null AuthorId (authorless story) is not confused with
            // "row not found" (layer2-services.md §"Scalar projections on nullable FK columns").
            var storyRow = await writeDb.Stories
                .Where(s => s.StoryId == storyId)
                .Select(s => new { s.AuthorId })
                .FirstOrDefaultAsync();
            if (storyRow is { AuthorId: int storyAuthorId })
            {
                int delta = willBeFavorite ? 1 : -1;
                await writeDb.UserStats.Where(us => us.UserId == storyAuthorId)
                    .ExecuteUpdateAsync(s => s.SetProperty(us => us.FavoritesOnStories, us => us.FavoritesOnStories + delta));
            }
        }

        // StoriesRead / StoriesInProgress / StoriesIgnored → acting user's stat.
        // HasStarted is unchanged by this method; use captured hadStarted.
        bool willBeCompleted  = update.IsCompleted;
        bool willBeIgnored    = update.IsIgnored;
        bool willBeInProgress = hadStarted && !willBeCompleted;

        if (willBeCompleted != wasCompleted)
        {
            int delta = willBeCompleted ? 1 : -1;
            await writeDb.UserStats.Where(us => us.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(us => us.StoriesRead, us => us.StoriesRead + delta));
        }
        if (willBeInProgress != wasInProgress)
        {
            int delta = willBeInProgress ? 1 : -1;
            await writeDb.UserStats.Where(us => us.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(us => us.StoriesInProgress, us => us.StoriesInProgress + delta));
        }
        if (willBeIgnored != wasIgnored)
        {
            int delta = willBeIgnored ? 1 : -1;
            await writeDb.UserStats.Where(us => us.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(us => us.StoriesIgnored, us => us.StoriesIgnored + delta));
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────────

    private static void EnsureDatePartition(UserStoryInteraction row, DateTime now, UserStoryInteractionStateUpdate update)
    {
        if (row.InteractionDatePartition is not null) return;
        if (!AnyBitTrue(update)) return;

        row.InteractionDatePartition = new UserStoryInteractionDate { UserId = row.UserId, StoryId = row.StoryId };
    }

    private static bool AnyBitTrue(UserStoryInteractionStateUpdate update) =>
        update.IsFavorite || update.IsHiddenFavorite || update.IsFollowed
        || update.IsCompleted || update.IsReadItLater || update.IsIgnored;

    /// <summary>
    /// D6: a raise is any panel bit going false→true. With no row every bit starts false, so every
    /// true bit is a raise — the guessed-id enumeration kind (g) exists to stop stays blocked.
    /// </summary>
    private static bool IsRaise(UserStoryInteraction? row, UserStoryInteractionStateUpdate update) =>
        (update.IsFavorite       && !(row?.IsFavorite       ?? false))
        || (update.IsHiddenFavorite && !(row?.IsHiddenFavorite ?? false))
        || (update.IsFollowed       && !(row?.IsFollowed       ?? false))
        || (update.IsCompleted      && !(row?.IsCompleted      ?? false))
        || (update.IsReadItLater    && !(row?.IsReadItLater    ?? false))
        || (update.IsIgnored        && !(row?.IsIgnored        ?? false));

    public async Task SetReadItLaterFromRecommendationAsync(int recommendationId)
    {
        if (CurrentUserId is not int userId)
            throw new InvalidOperationException("This operation requires an authenticated user.");

        // The rec must be one a reader can see on a card: exists, Approved, not taken down. A missing
        // and a hidden rec are indistinguishable (non-disclosure).
        var rec = await writeDb.Recommendations
            .Where(r => r.RecommendationId == recommendationId)
            .Select(r => new { r.StoryId, r.StatusId, r.IsTakenDown })
            .FirstOrDefaultAsync();
        if (rec is null || rec.StatusId != (short)RecommendationStatusEnum.Approved || rec.IsTakenDown)
            throw new KeyNotFoundException($"Recommendation {recommendationId} not found.");

        // A raise (D6): the full story-visibility guard, before any write.
        await RequireStoryVisibleAsync(rec.StoryId);

        UserStoryInteraction? row = await writeDb.UserStoryInteractions
            .Include(i => i.InteractionDatePartition)
            .Include(i => i.RecommendationSource)
            .FirstOrDefaultAsync(i => i.UserId == userId && i.StoryId == rec.StoryId);

        // D3's defining sentence: the attribution records how the IsReadItLater bit came to be set.
        // A bit already set (say, on the story page) was not set from this card — no attribution.
        bool flipsTheBit = row is null || !row.IsReadItLater;

        if (row is null)
        {
            row = new UserStoryInteraction { UserId = userId, StoryId = rec.StoryId };
            writeDb.UserStoryInteractions.Add(row);
        }

        DateTime now = DateTime.UtcNow;
        row.IsReadItLater = true; // every other bit untouched — no counter moves on this bit
        row.InteractionDatePartition ??= new UserStoryInteractionDate { UserId = row.UserId, StoryId = row.StoryId };
        row.InteractionDatePartition.ReadItLaterDate ??= now;

        // First attribution wins within one attribution's life; the story's author never gets one.
        if (flipsTheBit && row.RecommendationSource is null
            && await RecommendationAttribution.IsAttributableAsync(writeDb, userId, rec.StoryId, recommendationId))
        {
            row.RecommendationSource = new UserStoryRecommendationSource
            {
                UserId = userId,
                StoryId = rec.StoryId,
                SourceRecommendationId = recommendationId,
            };
        }

        // ONE save: the parent row and the attribution row commit together, so the FK-ordering hazard
        // the old on-load write hit (sources row before any USI row) is structurally unreachable.
        await writeDb.SaveChangesAsync();
    }

    public async Task MarkStartedAsync(int storyId, int? attributedRecommendationId = null)
    {
        if (CurrentUserId is not int userId) return;  // anonymous: no-op

        await RequireStoryVisibleAsync(storyId);

        UserStoryInteraction? row = await writeDb.UserStoryInteractions
            .Include(i => i.RecommendationSource)
            .FirstOrDefaultAsync(i => i.UserId == userId && i.StoryId == storyId);

        // Capture BEFORE applying the write (transition-delta rule) — StoriesInProgress mirrors
        // the recompute formula (HasStarted && !IsCompleted, layer2-services.md "Recalculation
        // worker" note) and must move here too: this reading-path call, not the panel, is the only
        // producer of a HasStarted false→true flip. Without it, MarkCompletedAsync's symmetric
        // decrement would underflow the counter below zero for the common case of a user who never
        // touched the panel (A3, 2026-07-24 — found via CompletionProducerTests).
        bool alreadyStarted = row?.HasStarted ?? false;
        bool wasCompleted = row?.IsCompleted ?? false;

        if (row is null)
        {
            row = new UserStoryInteraction { UserId = userId, StoryId = storyId };
            writeDb.UserStoryInteractions.Add(row);
        }

        row.HasStarted = true;

        // D3 direct-link entry point: the ?rec= the reader arrived with becomes the attribution here, at
        // the 90%-of-Chapter-1 moment, in the same save as the parent row — never on page load. The URL is
        // untrusted and this is the primary write, so an unattributable value is silently ignored.
        if (attributedRecommendationId is int recId && row.RecommendationSource is null
            && await RecommendationAttribution.IsAttributableAsync(writeDb, userId, storyId, recId))
        {
            row.RecommendationSource = new UserStoryRecommendationSource
            {
                UserId = userId,
                StoryId = storyId,
                SourceRecommendationId = recId,
            };
        }

        await writeDb.SaveChangesAsync();

        if (!alreadyStarted && !wasCompleted)
        {
            await writeDb.UserStats.Where(us => us.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(us => us.StoriesInProgress, us => us.StoriesInProgress + 1));
        }
    }

    public async Task MarkCompletedAsync(int storyId)
    {
        if (CurrentUserId is not int userId) return;  // anonymous: no-op

        await RequireStoryVisibleAsync(storyId);

        UserStoryInteraction? row = await writeDb.UserStoryInteractions
            .Include(i => i.InteractionDatePartition)
            .FirstOrDefaultAsync(i => i.UserId == userId && i.StoryId == storyId);

        // Idempotent: already complete → no-op (guards against a double StoriesRead increment on a
        // re-visit/re-scroll of the final chapter — A3 "re-visit behavior" is fire-once per completion).
        if (row is { IsCompleted: true }) return;

        // Capture derived state BEFORE applying the write (transition-delta rule — layer2-services.md
        // §"Transition-delta rule for UserStoryInteraction-derived counters"). wasCompleted is always
        // false here (guarded above); StoriesInProgress only moves if the user had already started.
        bool wasInProgress = row?.HasStarted ?? false;

        if (row is null)
        {
            row = new UserStoryInteraction { UserId = userId, StoryId = storyId };
            writeDb.UserStoryInteractions.Add(row);
        }

        DateTime now = DateTime.UtcNow;
        row.IsCompleted = true;
        row.InteractionDatePartition ??= new UserStoryInteractionDate { UserId = row.UserId, StoryId = row.StoryId };
        row.InteractionDatePartition.CompletedDate = now;

        await writeDb.SaveChangesAsync();

        // StoriesRead +1 (wasCompleted was false); StoriesInProgress −1 only when the user had
        // started (mirrors SetUserStoryInteractionStateAsync's transition-delta handling).
        await writeDb.UserStats.Where(us => us.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(us => us.StoriesRead, us => us.StoriesRead + 1));

        if (wasInProgress)
        {
            await writeDb.UserStats.Where(us => us.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(us => us.StoriesInProgress, us => us.StoriesInProgress - 1));
        }
    }

    private static void ValidateCombination(UserStoryInteractionStateUpdate update)
    {
        // Per spec §4: all 8 (HasStarted × IsCompleted × IsIgnored) combos are valid —
        // including (HasStarted=0, IsCompleted=1), which is the panel's "read elsewhere" use case.
        // No panel-writable combination is currently forbidden; the call site is kept so future
        // restrictions slot in without restructuring.
    }
}
