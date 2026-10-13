using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server.Data.Configurations;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side write implementation for Polls (Feature 37; requirements settled 2026-07-12 —
/// <c>audit/BlogPosts.md</c> F37).
/// <para>
/// <b>Security model:</b> site polls are gated on the shared <c>ActiveUser.RequireModerator()</c>
/// (Moderator or Admin — Admin does not inherit Moderator); blog-post polls on
/// <c>ownerId == ActiveUser.UserId</c>. UI affordances are convenience only; these service gates
/// are the control.
/// </para>
/// <para>
/// <b>Config lock:</b> once any vote exists, <c>AllowMultiple</c>/<c>ResultsVisibility</c>/
/// <c>AnonymityMode</c>/<c>DateOpened</c> are frozen (<see cref="PollValidationException"/> on
/// mismatch) — prevents retroactive anonymity exposure and multi→single vote invalidation. The
/// <c>AllowMultiple</c> half is also held by the data (owner ruling D23's <c>ConfigLocked</c> rider,
/// WU-PollVoteIntegrity 2026-10-13): each vote row's FK <c>(poll_id, allow_multiple)</c> →
/// <c>base_polls</c> has no ON UPDATE action, so an edit that races the first vote is refused by the
/// database and answered here as the same lock error. The other three fields are the service check
/// only (tracker F21).
/// </para>
/// <para>
/// <b>Single-choice is a constraint</b> (D23): the vote row's generated guard
/// <c>single_choice_poll_id</c> is unique with <c>user_id</c>. <see cref="VoteAsync"/>'s diff is the
/// friendly path. A vote that loses a race to the schema is never a 500: it succeeds when the voter's rows
/// already stand exactly as asked (their own double submit), and is otherwise a
/// <see cref="PollValidationException"/> (<see cref="VoteRaceMessage"/>) (<c>layer2-services.md</c>
/// §"Check-then-act posture").
/// </para>
/// <para>
/// <b>Edit notification:</b> a material edit (name/description/option change) to a voted-on poll
/// stamps <c>LastEditedAt</c>; <c>PollEditNotificationSweeper</c> notifies voters after the
/// 30-minute quiet period. No inline notification here — edits burst.
/// </para>
/// </summary>
public class ServerPollWriteService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser,
    IWriteRateLimitService rateLimit)
    : ServerPollReadService(readDbFactory, activeUser), IPollWriteService
{
    /// <summary>
    /// <see cref="VoteAsync"/>'s answer when its save loses a race and the voter's rows do not end up as
    /// the call asked: the voter's own other tab voting differently, the owner's edit (an option removed,
    /// or the vote mode changed before the first vote), or the poll's deletion changed what the call had
    /// loaded. Not retried: a retry would cast the vote under settings the voter never saw.
    /// </summary>
    public const string VoteRaceMessage =
        "This poll or your vote on it changed while your vote was being saved. Reload the page and vote again.";

    /// <summary>The config-lock error for a vote-mode change, from the service check or the database.</summary>
    public const string VoteModeLockedMessage = "Vote mode is locked once votes exist.";

    private const string OptionForeignKey = "fk_poll_votes_poll_options_poll_id_poll_option_id";

    public async Task<int> CreateSitePollAsync(PollEditDto dto)
    {
        int userId = ActiveUser.RequireModerator();
        rateLimit.EnsureAllowed(WriteActionKind.ContentCreate, userId);

        ValidateOrThrow(dto);
        DateTime now = DateTime.UtcNow;
        EnsureClosesAfterOpen(dto.DateClosed, dto.DateOpened ?? now);

        SitePoll poll = new() { IsArchived = false };
        ApplyCreate(poll, dto, userId, now);

        writeDb.Polls.Add(poll);
        await writeDb.SaveChangesAsync();
        return poll.PollId;
    }

    public async Task<int> CreateBlogPostPollAsync(int blogPostId, PollEditDto dto)
    {
        if (ActiveUser.UserId is not int userId)
            throw new InvalidOperationException("Creating a poll requires an authenticated user.");
        rateLimit.EnsureAllowed(WriteActionKind.ContentCreate, userId);

        // Write context is unfiltered; anonymous-type projection distinguishes "post missing"
        // from "authorless post" (layer2-services.md scalar-projection rule).
        var post = await writeDb.BlogPosts
            .Where(b => b.BlogPostId == blogPostId)
            .Select(b => new { b.AuthorId })
            .FirstOrDefaultAsync();
        if (post is null)
            throw new KeyNotFoundException($"Blog post {blogPostId} not found.");
        if (post.AuthorId != userId)
            throw new UnauthorizedAccessException("You can only add polls to your own blog posts.");

        ValidateOrThrow(dto);
        DateTime now = DateTime.UtcNow;
        EnsureClosesAfterOpen(dto.DateClosed, dto.DateOpened ?? now);

        BlogPostPoll poll = new() { BlogPostId = blogPostId };
        ApplyCreate(poll, dto, userId, now);

        writeDb.Polls.Add(poll);
        await writeDb.SaveChangesAsync();
        return poll.PollId;
    }

    public async Task UpdatePollAsync(int pollId, PollEditDto dto)
    {
        BasePoll poll = await LoadAuthorizedPollWithOptionsAsync(pollId);

        DateTime now = DateTime.UtcNow;
        if (PollRules.StatusFor(poll.DateOpened, poll.DateClosed, now) == PollStatus.Closed)
            throw new PollValidationException("Closed polls cannot be edited.");

        ValidateOrThrow(dto);
        // dto.DateOpened == null keeps the stored open date, which CanSave cannot see.
        EnsureClosesAfterOpen(dto.DateClosed, dto.DateOpened ?? poll.DateOpened);

        int[] existingOptionIds = poll.PollOptions.Select(o => o.PollOptionId).ToArray();
        bool hasVotes = existingOptionIds.Length > 0 && await writeDb.PollVotes
            .AnyAsync(v => existingOptionIds.Contains(v.PollOptionId));

        // Retained option ids must belong to this poll (a foreign id would silently steal votes).
        List<int> retainedIds = dto.Options
            .Where(o => o.PollOptionId is not null)
            .Select(o => o.PollOptionId!.Value)
            .ToList();
        if (retainedIds.Except(existingOptionIds).Any())
            throw new PollValidationException("An option in the update does not belong to this poll.");

        if (hasVotes)
        {
            // Config lock (settled 2026-07-12): the three config fields + DateOpened freeze once
            // any vote exists. dto.DateOpened == null means "keep as-is".
            List<string> lockErrors = [];
            if (dto.AllowMultiple != poll.AllowMultiple)
                lockErrors.Add(VoteModeLockedMessage);
            if (dto.ResultsVisibility != poll.ResultsVisibility)
                lockErrors.Add("Results visibility is locked once votes exist.");
            if (dto.AnonymityMode != poll.AnonymityMode)
                lockErrors.Add("Anonymity is locked once votes exist.");
            if (dto.DateOpened is DateTime opened && opened != poll.DateOpened)
                lockErrors.Add("The open date is locked once votes exist.");
            if (lockErrors.Count > 0) throw new PollValidationException(lockErrors);
        }

        // Material-change detection BEFORE mutating (name/description/option set — reorder alone
        // is not material; it doesn't change what anyone voted for).
        Dictionary<int, PollOption> byId = poll.PollOptions.ToDictionary(o => o.PollOptionId);
        bool materialChange =
            poll.PollName != dto.PollName.Trim()
            || (poll.Description ?? "") != (dto.Description?.Trim() ?? "")
            || existingOptionIds.Except(retainedIds).Any()                       // deletions
            || dto.Options.Any(o => o.PollOptionId is null)                      // additions
            || dto.Options.Any(o => o.PollOptionId is int id
                                    && byId[id].Text != o.Text.Trim());          // renames

        poll.PollName = dto.PollName.Trim();
        poll.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        if (dto.DateOpened is DateTime newOpened) poll.DateOpened = newOpened;
        poll.DateClosed = dto.DateClosed;
        if (!hasVotes)
        {
            poll.ResultsVisibility = dto.ResultsVisibility;
            poll.AnonymityMode = dto.AnonymityMode;
        }

        // AllowMultiple is half of base_polls' alternate key (the vote rows' mode-FK target), and EF holds
        // a key property read-only once saved, so a pre-vote mode change is written set-based inside the
        // transaction below (layer1-data-model.md §"Column Conventions"). If a first vote lands after the
        // hasVotes read, that UPDATE is what the database refuses.
        bool modeChange = !hasVotes && dto.AllowMultiple != poll.AllowMultiple;

        if (materialChange && hasVotes)
            poll.LastEditedAt = now;   // sweep picks this up after the 30-min quiet period

        // Options reconcile. The (PollId, SortOrder) and (PollId, Text) unique indexes are
        // non-deferred, so reorders/renames go through a staged sequence inside one transaction:
        // 1) delete removed options; 2) retained options get updated text + temporary NEGATIVE
        // sort orders (can't collide with anything); 3) inserts + final sort orders (finals are
        // >= 0, retained rows still negative when the inserts land). EnableRetryOnFailure demands
        // the execution-strategy wrapper around the explicit transaction.
        List<PollOption> removed = poll.PollOptions.Where(o => !retainedIds.Contains(o.PollOptionId)).ToList();

        IExecutionStrategy strategy = writeDb.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using IDbContextTransaction tx = await writeDb.Database.BeginTransactionAsync();

                if (modeChange)
                {
                    await writeDb.Polls
                        .Where(p => p.PollId == poll.PollId)
                        .ExecuteUpdateAsync(set => set.SetProperty(p => p.AllowMultiple, dto.AllowMultiple));
                }

                if (removed.Count > 0)
                {
                    writeDb.PollOptions.RemoveRange(removed);   // votes cascade
                    await writeDb.SaveChangesAsync();
                }

                for (int i = 0; i < dto.Options.Count; i++)
                {
                    if (dto.Options[i].PollOptionId is int id)
                    {
                        PollOption existing = byId[id];
                        existing.Text = dto.Options[i].Text.Trim();
                        existing.SortOrder = -(i + 1);
                    }
                }
                await writeDb.SaveChangesAsync();

                for (int i = 0; i < dto.Options.Count; i++)
                {
                    if (dto.Options[i].PollOptionId is int id)
                        byId[id].SortOrder = i;
                    else
                        writeDb.PollOptions.Add(new PollOption
                        {
                            PollId = poll.PollId,
                            Text = dto.Options[i].Text.Trim(),
                            SortOrder = i,
                        });
                }
                await writeDb.SaveChangesAsync();

                await tx.CommitAsync();
            });
        }
        // A first vote committed after the hasVotes read: the mode FK (no ON UPDATE action) refuses the
        // set-based UPDATE above and the transaction rolls back. Same answer as the service check.
        catch (Exception ex) when (modeChange && IsViolationOf(ex, PostgresErrorCodes.ForeignKeyViolation,
                                       PollVoteConfiguration.PollModeForeignKey))
        {
            throw new PollValidationException(VoteModeLockedMessage);
        }
        finally
        {
            // The write context is circuit-scoped. After a set-based mode write the tracked poll still holds
            // the old AllowMultiple (identity resolution never refreshes it), and after a failure its edits
            // are still pending; either way the next call must load it fresh.
            DetachPoll(poll.PollId);
        }
    }

    public async Task ClosePollAsync(int pollId)
    {
        BasePoll poll = await LoadAuthorizedPollWithOptionsAsync(pollId);

        DateTime now = DateTime.UtcNow;
        if (PollRules.StatusFor(poll.DateOpened, poll.DateClosed, now) != PollStatus.Open)
            throw new PollValidationException("Only an open poll can be closed.");

        poll.DateClosed = now;
        await writeDb.SaveChangesAsync();
    }

    public async Task SetSitePollArchivedAsync(int pollId, bool archived)
    {
        ActiveUser.RequireModerator();

        SitePoll? poll = await writeDb.Polls.OfType<SitePoll>()
            .FirstOrDefaultAsync(p => p.PollId == pollId);
        if (poll is null)
            throw new KeyNotFoundException($"Site poll {pollId} not found.");

        poll.IsArchived = archived;
        await writeDb.SaveChangesAsync();
    }

    public async Task DeletePollAsync(int pollId)
    {
        BasePoll poll = await LoadAuthorizedPollWithOptionsAsync(pollId);

        // Tracked TPT delete: EF issues child-then-base DELETE in one transaction; options and
        // votes cascade (ExecuteDeleteAsync is unsupported on TPT base-type sets — WU31.5).
        writeDb.Polls.Remove(poll);
        await writeDb.SaveChangesAsync();
    }

    public async Task<PollDto?> VoteAsync(int pollId, int[] optionIds, bool voteAnonymously)
    {
        if (ActiveUser.UserId is not int userId)
            throw new InvalidOperationException("Voting requires an authenticated user.");
        // Owner ruling D20: every call spends one token — a vote, a change and a retraction are this
        // one method. Before any DB work, so a vote on a missing or hidden poll spends one too.
        rateLimit.EnsureAllowed(WriteActionKind.Vote, userId);

        var poll = await writeDb.Polls
            .Where(p => p.PollId == pollId)
            .Select(p => new
            {
                p.AllowMultiple, p.AnonymityMode, p.DateOpened, p.DateClosed,
                BlogPostId = p is BlogPostPoll ? (int?)((BlogPostPoll)p).BlogPostId : null,
                OptionIds = p.PollOptions.Select(o => o.PollOptionId).ToArray(),
            })
            .FirstOrDefaultAsync();
        if (poll is null)
            throw new KeyNotFoundException($"Poll {pollId} not found.");

        int[] picked = optionIds.Distinct().ToArray();

        // IsAnonymous is only meaningful under VoterChoice; forced false otherwise so a later
        // display never has stray flags to honor.
        bool isAnonymous = poll.AnonymityMode == PollAnonymityMode.VoterChoice && voteAnonymously;

        // Load first, diff, then decide (owner ruling D6 — identity-and-authorization.md §"Raises
        // vs clears"). Replace semantics: the new set fully supersedes the viewer's votes on this
        // poll (empty = retract all). Kept rows also refresh IsAnonymous — re-voting is how a
        // VoterChoice voter flips their own visibility.
        List<PollVote> existing = await writeDb.PollVotes
            .Where(v => v.PollId == pollId && v.UserId == userId)
            .ToListAsync();

        // A pure withdrawal — the caller holds votes here, and the payload only drops some or all of
        // them without flipping a kept row's anonymity — is a clear of the caller's own rows. With
        // no existing vote there is nothing to withdraw, so the guard runs and a hidden poll answers
        // exactly like a missing one.
        bool isWithdrawal = existing.Count > 0
            && picked.All(id => existing.Any(v => v.PollOptionId == id))
            && existing.Where(v => picked.Contains(v.PollOptionId)).All(v => v.IsAnonymous == isAnonymous);

        // Kind (g), raise only: you cannot vote on a poll hanging off a post you cannot see. This is
        // the write half of D2 and the more damaging one — a vote on someone's draft sets
        // ConfigLocked, which freezes AllowMultiple/ResultsVisibility/AnonymityMode before the
        // author ever publishes. writeDb is unfiltered, so the existence check above proves nothing
        // about visibility; the guard needs the read context. Same message as a missing poll
        // (non-disclosure). Any raise in the payload guards the whole call, before any mutation.
        if (poll.BlogPostId is int parentId && !isWithdrawal)
        {
            await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
            if (!await BlogPostVisibilityGuard.IsBlogPostVisibleAsync(readDb, ActiveUser, parentId))
                throw new KeyNotFoundException($"Poll {pollId} not found.");
        }

        // Not a visibility guard: the Open window is the poll's own rule and binds withdrawals too.
        if (PollRules.StatusFor(poll.DateOpened, poll.DateClosed, DateTime.UtcNow) != PollStatus.Open)
            throw new PollValidationException("This poll is not open for voting.");

        if (picked.Except(poll.OptionIds).Any())
            throw new PollValidationException("One of the selected options does not belong to this poll.");
        if (!poll.AllowMultiple && picked.Length > 1)
            throw new PollValidationException("This poll allows a single choice.");

        foreach (PollVote vote in existing.Where(v => !picked.Contains(v.PollOptionId)))
            writeDb.PollVotes.Remove(vote);
        foreach (PollVote vote in existing.Where(v => picked.Contains(v.PollOptionId)))
            vote.IsAnonymous = isAnonymous;
        // A new row carries the poll's mode as read above. The mode FK refuses it if the owner changed the
        // mode since (possible only before the first vote), and the generated guard refuses a second live
        // row on a single-choice poll (owner ruling D23).
        List<PollVote> added = picked.Except(existing.Select(v => v.PollOptionId))
            .Select(optionId => new PollVote
            {
                PollId = pollId, PollOptionId = optionId, UserId = userId,
                AllowMultiple = poll.AllowMultiple, IsAnonymous = isAnonymous,
            })
            .ToList();
        writeDb.PollVotes.AddRange(added);

        try
        {
            await writeDb.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsLostVoteRace(ex))
        {
            // Detach what this call touched, so a circuit-scoped context does not retry it on its next
            // save and the next call reloads the voter's rows as they now stand.
            foreach (PollVote vote in existing.Concat(added))
                writeDb.Entry(vote).State = EntityState.Detached;

            // Lost to a request that left exactly what this one asked for (the voter's own double submit:
            // two tabs, a client retry): the vote stands as cast, so this is a success, as D23's
            // create-create rows count a racing duplicate as already done. Any other result is a refusal.
            if (!await VotesStandAsRequestedAsync(pollId, userId, picked, isAnonymous))
                throw new PollValidationException(VoteRaceMessage);
        }

        // The response is a read and applies the same kind-(g) guard. After a withdrawal from a poll
        // the caller can no longer see it is null: the clear landed, and the hidden poll's content
        // and tallies are not disclosed. After a raise it can only come back null if the parent post
        // was hidden between the check above and here (author unpublished it, or a mod took it down
        // mid-vote) — report that as the poll being gone rather than returning null.
        PollDto? refreshed = await GetPollAsync(pollId);
        if (refreshed is null && !isWithdrawal)
            throw new KeyNotFoundException($"Poll {pollId} not found.");
        return refreshed;
    }

    // ── Private helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Whether a vote save failed because the poll or the voter's own rows changed between
    /// <see cref="VoteAsync"/>'s load and its save (the caller then decides, by
    /// <see cref="VotesStandAsRequestedAsync"/>, between success and <see cref="VoteRaceMessage"/>). Each
    /// constraint is matched by name, so any other failure stays a genuine fault:
    /// <list type="bullet">
    ///   <item>a 0-row delete or update (<see cref="DbUpdateConcurrencyException"/>): another request
    ///   removed the row first;</item>
    ///   <item>23505 on the single-choice guard or the (option, user) index: the same voter's other
    ///   request inserted first;</item>
    ///   <item>23503 on the mode FK (the owner changed the mode before the first vote) or on the option
    ///   FK (the option, or the whole poll, was deleted).</item>
    /// </list>
    /// </summary>
    private static bool IsLostVoteRace(DbUpdateException ex) =>
        ex is DbUpdateConcurrencyException
        || IsViolationOf(ex, PostgresErrorCodes.UniqueViolation, PollVoteConfiguration.SingleChoiceIndex)
        || IsViolationOf(ex, PostgresErrorCodes.UniqueViolation, PollVoteConfiguration.OptionUserIndex)
        || IsViolationOf(ex, PostgresErrorCodes.ForeignKeyViolation, PollVoteConfiguration.PollModeForeignKey)
        || IsViolationOf(ex, PostgresErrorCodes.ForeignKeyViolation, OptionForeignKey);

    /// <summary>
    /// After a lost race: whether the caller's rows on the poll are now exactly what
    /// <see cref="VoteAsync"/> asked for, namely the options in <paramref name="picked"/> and no others, each
    /// with <paramref name="isAnonymous"/>. The (option, user) index makes the rows distinct by option, so
    /// the count and containment checks together amount to set equality. Untracked: the rows are only compared.
    /// </summary>
    private async Task<bool> VotesStandAsRequestedAsync(int pollId, int userId, int[] picked, bool isAnonymous)
    {
        var rows = await writeDb.PollVotes.AsNoTracking()
            .Where(v => v.PollId == pollId && v.UserId == userId)
            .Select(v => new { v.PollOptionId, v.IsAnonymous })
            .ToListAsync();
        return rows.Count == picked.Length
            && rows.All(r => picked.Contains(r.PollOptionId) && r.IsAnonymous == isAnonymous);
    }

    /// <summary>
    /// True when <paramref name="ex"/> (a <c>DbUpdateException</c> from <c>SaveChanges</c>, or the
    /// <see cref="PostgresException"/> a set-based write throws directly) is <paramref name="sqlState"/>
    /// on the named constraint.
    /// </summary>
    private static bool IsViolationOf(Exception ex, string sqlState, string constraintName) =>
        (ex as PostgresException ?? ex.InnerException as PostgresException) is { } pg
        && pg.SqlState == sqlState
        && pg.ConstraintName == constraintName;

    /// <summary>
    /// Detaches the tracked poll and every tracked option and vote of it (see
    /// <see cref="UpdatePollAsync"/>); a vote can be tracked here when the owner voted on this circuit.
    /// </summary>
    private void DetachPoll(int pollId)
    {
        List<EntityEntry> entries = writeDb.ChangeTracker.Entries()
            .Where(e => e.Entity is BasePoll p && p.PollId == pollId
                        || e.Entity is PollOption o && o.PollId == pollId
                        || e.Entity is PollVote v && v.PollId == pollId)
            .ToList();
        foreach (EntityEntry entry in entries)
            entry.State = EntityState.Detached;
    }

    private static void ValidateOrThrow(PollEditDto dto)
    {
        List<string> errors = dto.CanSave();
        if (errors.Count > 0) throw new PollValidationException(errors);
    }

    /// <summary>
    /// The close date must follow the <b>effective</b> open date. <see cref="PollEditDto.CanSave"/>
    /// compares only when both dates are sent, so a create that leaves the open date to "now" with a
    /// close date already past, or an update that keeps the stored open date and moves the close date
    /// before it, used to persist <c>date_closed &lt; date_opened</c>. Since WU-SchemaHardening
    /// <c>ck_base_polls_dates</c> refuses that row, so this check is what turns it into a 400 instead
    /// of the constraint's 500. Same message and the same strict <c>&lt;=</c> as <c>CanSave</c>.
    /// </summary>
    private static void EnsureClosesAfterOpen(DateTime? dateClosed, DateTime effectiveOpened)
    {
        if (dateClosed is DateTime closed && closed <= effectiveOpened)
            throw new PollValidationException(PollEditDto.CloseBeforeOpenMessage);
    }

    private void ApplyCreate(BasePoll poll, PollEditDto dto, int ownerId, DateTime now)
    {
        poll.OwnerId = ownerId;
        poll.PollName = dto.PollName.Trim();
        poll.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        poll.DateOpened = dto.DateOpened ?? now;
        poll.DateClosed = dto.DateClosed;
        poll.AllowMultiple = dto.AllowMultiple;
        poll.ResultsVisibility = dto.ResultsVisibility;
        poll.AnonymityMode = dto.AnonymityMode;
        for (int i = 0; i < dto.Options.Count; i++)
            poll.PollOptions.Add(new PollOption { Text = dto.Options[i].Text.Trim(), SortOrder = i });
    }

    /// <summary>
    /// Loads the poll (tracked, with options) and enforces the manage gate: site polls →
    /// moderator/admin; blog-post polls → owner. Throws KeyNotFound / UnauthorizedAccess.
    /// An ownerless blog-post poll (owner deleted, D11) is manageable by no one.
    /// </summary>
    private async Task<BasePoll> LoadAuthorizedPollWithOptionsAsync(int pollId)
    {
        BasePoll? poll = await writeDb.Polls
            .Include(p => p.PollOptions)
            .FirstOrDefaultAsync(p => p.PollId == pollId);
        if (poll is null)
            throw new KeyNotFoundException($"Poll {pollId} not found.");

        if (poll is SitePoll)
        {
            ActiveUser.RequireModerator();
        }
        else
        {
            // Pattern, never `UserId != OwnerId`: null != null is false in C#, so an anonymous caller
            // would pass this gate on an ownerless poll (owner ruling D11 — NULL owner = nobody).
            if (poll.OwnerId is not int ownerId || ActiveUser.UserId != ownerId)
                throw new UnauthorizedAccessException("You can only manage your own polls.");
        }

        return poll;
    }
}
