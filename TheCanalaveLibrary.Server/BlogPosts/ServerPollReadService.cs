using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side read implementation for Polls (Feature 37). Projects viewer-relative
/// <see cref="PollDto"/>s: results visibility (<see cref="PollRules.ResultsVisible"/>) is enforced
/// here — when not visible to the viewer, tallies are zeroed and voter names emptied
/// <b>server-side</b>, so the UI never receives numbers it shouldn't show. Voter names are
/// likewise blanked for <c>Anonymous</c>-mode polls regardless of what the votes carry.
/// </summary>
public class ServerPollReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser) : IPollReadService
{
    protected IDbContextFactory<ReadOnlyApplicationDbContext> ReadDbFactory { get; } = readDbFactory;
    protected IActiveUserContext ActiveUser { get; } = activeUser;

    // NOTE: sources stay statically IQueryable<BasePoll> (filtered via `is` checks), never
    // OfType<TChild>() upcast through covariance — a child-typed source makes EF's expression
    // preprocessor coerce the OTHER child's cast in ProjectAsync's projection and throw
    // "No coercion operator is defined between types 'SitePoll' and 'BlogPostPoll'"
    // (found via browser verification 2026-07-12; regression net: PollServiceTests list tests).

    public async Task<PollDto[]> GetSitePollsAsync(bool includeArchived)
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
        IQueryable<BasePoll> q = readDb.Polls.Where(p => p is SitePoll);
        if (!includeArchived) q = q.Where(p => !((SitePoll)p).IsArchived);
        return await ProjectAsync(q.OrderByDescending(p => p.DateOpened));
    }

    public async Task<PollDto[]> GetPollsForBlogPostAsync(int blogPostId)
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
        return await ProjectAsync(readDb.Polls
            .Where(p => p is BlogPostPoll && ((BlogPostPoll)p).BlogPostId == blogPostId)
            .OrderBy(p => p.PollId));
    }

    public async Task<PollDto?> GetPollAsync(int pollId)
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
        PollDto[] result = await ProjectAsync(readDb.Polls.Where(p => p.PollId == pollId));
        return result.FirstOrDefault();
    }

    /// <summary>
    /// Shared projection: one SQL round-trip per call (options + votes as nested subqueries),
    /// then the viewer-relative computation (status, visibility, blanking) in C#.
    /// </summary>
    private async Task<PollDto[]> ProjectAsync(IQueryable<BasePoll> source)
    {
        int viewerId = ActiveUser.UserId ?? -1;

        var rows = await source
            .Select(p => new
            {
                p.PollId, p.PollName, p.Description, p.DateOpened, p.DateClosed,
                p.AllowMultiple, p.ResultsVisibility, p.AnonymityMode, p.OwnerId,
                OwnerUserName = (string?)p.Owner.UserName,
                IsArchived = p is SitePoll && ((SitePoll)p).IsArchived,
                BlogPostId = p is BlogPostPoll ? (int?)((BlogPostPoll)p).BlogPostId : null,
                TotalVoterCount = p.PollOptions
                    .SelectMany(o => o.Votes).Select(v => v.UserId).Distinct().Count(),
                ViewerVotedOptionIds = p.PollOptions
                    .SelectMany(o => o.Votes)
                    .Where(v => v.UserId == viewerId)
                    .Select(v => v.PollOptionId).ToArray(),
                ViewerVotedAnonymously = p.PollOptions
                    .SelectMany(o => o.Votes)
                    .Any(v => v.UserId == viewerId && v.IsAnonymous),
                Options = p.PollOptions
                    .OrderBy(o => o.SortOrder)
                    .Select(o => new
                    {
                        o.PollOptionId, o.Text, o.SortOrder,
                        VoteCount = o.Votes.Count(),
                        PublicVoters = o.Votes
                            .Where(v => !v.IsAnonymous)
                            .OrderBy(v => v.User.UserName)
                            .Select(v => new { v.UserId, v.User.UserName })
                            .ToArray(),
                    }).ToArray(),
            })
            .ToArrayAsync();

        DateTime now = DateTime.UtcNow;
        bool viewerIsMod = ActiveUser.IsModerator || ActiveUser.IsAdmin;

        return rows.Select(r =>
        {
            PollStatus status = PollRules.StatusFor(r.DateOpened, r.DateClosed, now);
            bool isOwner = ActiveUser.UserId == r.OwnerId;
            bool resultsVisible = PollRules.ResultsVisible(
                r.ResultsVisibility, status,
                viewerHasCurrentVote: r.ViewerVotedOptionIds.Length > 0,
                viewerIsOwnerOrModerator: isOwner || viewerIsMod);
            bool showNames = resultsVisible && r.AnonymityMode != PollAnonymityMode.Anonymous;

            return new PollDto(
                r.PollId, r.PollName, r.Description, r.DateOpened, r.DateClosed,
                r.AllowMultiple, r.ResultsVisibility, r.AnonymityMode,
                r.OwnerId, r.OwnerUserName, r.IsArchived, r.BlogPostId,
                status,
                ResultsVisibleToViewer: resultsVisible,
                ConfigLocked: r.TotalVoterCount > 0,
                ViewerVotedOptionIds: r.ViewerVotedOptionIds,
                ViewerVotedAnonymously: r.ViewerVotedAnonymously,
                TotalVoterCount: resultsVisible ? r.TotalVoterCount : 0,
                Options: r.Options.Select(o => new PollOptionResultDto(
                    o.PollOptionId, o.Text, o.SortOrder,
                    VoteCount: resultsVisible ? o.VoteCount : 0,
                    PublicVoters: showNames
                        ? o.PublicVoters.Select(v => new PollVoterDto(v.UserId, v.UserName ?? "")).ToArray()
                        : [])).ToArray());
        }).ToArray();
    }
}
