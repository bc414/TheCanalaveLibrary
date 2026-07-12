using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side write implementation for Polls (Feature 37; requirements settled 2026-07-12 —
/// <c>audit/BlogPosts.md</c> F37).
/// <para>
/// <b>Security model:</b> site polls are gated on <c>IsModerator || IsAdmin</c> (listed
/// explicitly — Admin does not inherit Moderator); blog-post polls on
/// <c>ownerId == ActiveUser.UserId</c>. UI affordances are convenience only; these service gates
/// are the control.
/// </para>
/// <para>
/// <b>Config lock:</b> once any vote exists, <c>AllowMultiple</c>/<c>ResultsVisibility</c>/
/// <c>AnonymityMode</c>/<c>DateOpened</c> are frozen (<see cref="PollValidationException"/> on
/// mismatch) — prevents retroactive anonymity exposure and multi→single vote invalidation.
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
    public async Task<int> CreateSitePollAsync(PollEditDto dto)
    {
        if (ActiveUser.UserId is not int userId)
            throw new InvalidOperationException("Creating a poll requires an authenticated user.");
        if (!(ActiveUser.IsModerator || ActiveUser.IsAdmin))
            throw new UnauthorizedAccessException("Only moderators can create site polls.");
        rateLimit.EnsureAllowed(WriteActionKind.ContentCreate, userId);

        ValidateOrThrow(dto);

        SitePoll poll = new() { IsArchived = false };
        ApplyCreate(poll, dto, userId);

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

        BlogPostPoll poll = new() { BlogPostId = blogPostId };
        ApplyCreate(poll, dto, userId);

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
                lockErrors.Add("Vote mode is locked once votes exist.");
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
            poll.AllowMultiple = dto.AllowMultiple;
            poll.ResultsVisibility = dto.ResultsVisibility;
            poll.AnonymityMode = dto.AnonymityMode;
        }

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
        await strategy.ExecuteAsync(async () =>
        {
            await using IDbContextTransaction tx = await writeDb.Database.BeginTransactionAsync();

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
        if (!(ActiveUser.IsModerator || ActiveUser.IsAdmin))
            throw new UnauthorizedAccessException("Only moderators can archive site polls.");

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

    public async Task<PollDto> VoteAsync(int pollId, int[] optionIds, bool voteAnonymously)
    {
        if (ActiveUser.UserId is not int userId)
            throw new InvalidOperationException("Voting requires an authenticated user.");

        var poll = await writeDb.Polls
            .Where(p => p.PollId == pollId)
            .Select(p => new
            {
                p.AllowMultiple, p.AnonymityMode, p.DateOpened, p.DateClosed,
                OptionIds = p.PollOptions.Select(o => o.PollOptionId).ToArray(),
            })
            .FirstOrDefaultAsync();
        if (poll is null)
            throw new KeyNotFoundException($"Poll {pollId} not found.");

        if (PollRules.StatusFor(poll.DateOpened, poll.DateClosed, DateTime.UtcNow) != PollStatus.Open)
            throw new PollValidationException("This poll is not open for voting.");

        int[] picked = optionIds.Distinct().ToArray();
        if (picked.Except(poll.OptionIds).Any())
            throw new PollValidationException("One of the selected options does not belong to this poll.");
        if (!poll.AllowMultiple && picked.Length > 1)
            throw new PollValidationException("This poll allows a single choice.");

        // IsAnonymous is only meaningful under VoterChoice; forced false otherwise so a later
        // display never has stray flags to honor.
        bool isAnonymous = poll.AnonymityMode == PollAnonymityMode.VoterChoice && voteAnonymously;

        // Replace semantics: the new set fully supersedes the viewer's votes on this poll
        // (empty = retract all). Kept rows also refresh IsAnonymous — re-voting is how a
        // VoterChoice voter flips their own visibility.
        List<PollVote> existing = await writeDb.PollVotes
            .Where(v => v.UserId == userId && poll.OptionIds.Contains(v.PollOptionId))
            .ToListAsync();

        foreach (PollVote vote in existing.Where(v => !picked.Contains(v.PollOptionId)))
            writeDb.PollVotes.Remove(vote);
        foreach (PollVote vote in existing.Where(v => picked.Contains(v.PollOptionId)))
            vote.IsAnonymous = isAnonymous;
        foreach (int optionId in picked.Except(existing.Select(v => v.PollOptionId)))
            writeDb.PollVotes.Add(new PollVote { PollOptionId = optionId, UserId = userId, IsAnonymous = isAnonymous });

        await writeDb.SaveChangesAsync();

        return (await GetPollAsync(pollId))!;
    }

    // ── Private helpers ──────────────────────────────────────────────────────────

    private static void ValidateOrThrow(PollEditDto dto)
    {
        List<string> errors = dto.CanSave();
        if (errors.Count > 0) throw new PollValidationException(errors);
    }

    private void ApplyCreate(BasePoll poll, PollEditDto dto, int ownerId)
    {
        poll.OwnerId = ownerId;
        poll.PollName = dto.PollName.Trim();
        poll.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        poll.DateOpened = dto.DateOpened ?? DateTime.UtcNow;
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
            if (!(ActiveUser.IsModerator || ActiveUser.IsAdmin))
                throw new UnauthorizedAccessException("Only moderators can manage site polls.");
        }
        else
        {
            if (ActiveUser.UserId != poll.OwnerId)
                throw new UnauthorizedAccessException("You can only manage your own polls.");
        }

        return poll;
    }
}
