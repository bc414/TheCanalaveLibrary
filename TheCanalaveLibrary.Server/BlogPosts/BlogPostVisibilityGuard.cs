using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// The facts a blog post's visibility decision needs. Callers that already project these columns
/// (see <c>ServerBlogPostReadService.GetByIdAsync</c>) pass them straight to
/// <see cref="BlogPostVisibilityGuard.IsVisibleAsync"/> and pay no extra query.
/// </summary>
/// <param name="GroupAudience">The owning group's audience rating; null for profile posts.</param>
/// <param name="AuthorProfileVisibility">
/// The author's <see cref="ProfileVisibility"/> — non-null <b>only</b> for a profile post with a
/// non-null author. A profile post is profile-tab data (Class A), so it is exactly as visible as its
/// author's profile; group and site posts pass null and carry no profile check, so a Private
/// moderator's site announcement stays public (WU-AccessGateSweep2).
/// </param>
public readonly record struct BlogPostVisibilityFacts(
    int BlogPostId,
    int? AuthorId,
    bool IsPublished,
    Rating Rating,
    bool IsGroupPost,
    int? GroupId,
    Rating? GroupAudience,
    ProfileVisibility? AuthorProfileVisibility);

/// <summary>
/// Shared blog-post-visibility predicate — conditionality kind (g), the parent-visibility invariant
/// (<c>identity-and-authorization.md</c> §"Parent-visibility guards", WU-ParentVisibility).
/// <para>
/// A blog post's children (its polls, its comments, its likes) must never be more visible than the
/// post itself. Before this guard existed, <c>ServerPollReadService</c> and
/// <c>ServerCommentReadService</c> filtered on the bare <c>BlogPostId</c> FK — so the
/// <c>BaseBlogPost</c> <c>IsTakenDown</c> filter never applied and nothing consulted
/// <c>IsPublished</c> or the rating gate. Poll metadata for unpublished drafts was readable
/// anonymously (<c>hidden-deferrals-tracker.md</c> D2).
/// </para>
/// <para>
/// Semantics mirror <c>ServerBlogPostReadService.GetByIdAsync</c> exactly, because that read now
/// delegates here: the author always passes their own unpublished post; a profile post is hidden
/// when its author's <see cref="ProfileVisibility"/> hides their profile from this viewer
/// (<see cref="ProfileVisibilityGuard.IsVisible"/> — WU-AccessGateSweep2: blog posts are profile-tab
/// data, and a by-id read is just another path to them); a group reveal covers all group-owned
/// content (audience gate AND M-rated group posts — one consent per community) while a profile
/// post's M rating gates on its own per-post reveal; verified bots bypass consent but <b>not</b>
/// profile privacy (Class B vs Class A, so the bot check sits below the profile check); and a
/// taken-down post is hidden from <b>everyone including its author</b> (the <c>IsTakenDown</c> filter
/// is not author-conditional, so a taken-down row simply never loads here).
/// </para>
/// <para>
/// A missing post returns false — the caller's own query would return empty for it anyway, and
/// callers must render "hidden" and "absent" identically (non-disclosure rule).
/// </para>
/// </summary>
public static class BlogPostVisibilityGuard
{
    /// <summary>
    /// Loads the post's facts and decides. For callers holding only an id (poll reads, comment
    /// reads, the child write paths).
    /// </summary>
    public static async Task<bool> IsBlogPostVisibleAsync(
        ReadOnlyApplicationDbContext readDb, IActiveUserContext viewer, int blogPostId)
    {
        BlogPostVisibilityFacts? facts = await LoadFactsAsync(readDb, blogPostId);
        return facts is BlogPostVisibilityFacts row && await IsVisibleAsync(readDb, viewer, row);
    }

    /// <summary>
    /// Decides over facts the caller already projected — resolving the reveal only when consent is
    /// actually what the decision turns on, so the author, hidden-profile and verified-bot paths cost
    /// no query. The decision itself is always <see cref="IsVisible"/>'s.
    /// </summary>
    public static async Task<bool> IsVisibleAsync(
        ReadOnlyApplicationDbContext readDb, IActiveUserContext viewer, BlogPostVisibilityFacts facts)
    {
        bool isAuthor = viewer.UserId is int uid && uid == facts.AuthorId;

        // Every earlier rule in IsVisible decides without the reveal; ask for it only when the
        // rating/audience gate is what remains.
        bool revealCanMatter = facts.IsPublished && !isAuthor
            && IsAuthorProfileVisible(facts, viewer) && !viewer.IsVerifiedBot;

        // A group reveal covers all group-owned content (one consent per community); a profile
        // post's M rating gates on its own per-post reveal.
        bool isRevealed = revealCanMatter && (facts.IsGroupPost
            ? facts.GroupId is int gid
              && await RevealCheck.IsRevealedAsync(readDb, viewer, RevealedEntityType.Group, gid)
            : await RevealCheck.IsRevealedAsync(readDb, viewer, RevealedEntityType.BlogPost, facts.BlogPostId));

        return IsVisible(facts, viewer, isRevealed);
    }

    /// <summary>
    /// Loads the two-branch TPT projection the decision needs. Returns null when the post does not
    /// exist or is taken down (the <c>IsTakenDown</c> filter on <c>BaseBlogPost</c> applies through
    /// both child DbSets).
    /// </summary>
    public static async Task<BlogPostVisibilityFacts?> LoadFactsAsync(
        ReadOnlyApplicationDbContext readDb, int blogPostId)
    {
        // Profile branch: carries the author's ProfileVisibility (jsonb, projected through the
        // Author navigation) — the Class-A half of the decision.
        BlogPostVisibilityFacts? row = await readDb.ProfileBlogPosts
            .Where(p => p.BlogPostId == blogPostId)
            .Select(p => (BlogPostVisibilityFacts?)new BlogPostVisibilityFacts(
                p.BlogPostId, p.AuthorId, p.IsPublished, p.Rating, false, null, null,
                p.Author != null ? (ProfileVisibility?)p.Author.PrivacySettings.ProfileVisibility : null))
            .FirstOrDefaultAsync();

        if (row is not null) return row;

        // Group branch: the GroupAudience filter is bypassed so the audience decision can be made
        // reveal-aware in IsVisible — the filtered navigation join would drop the row before we
        // could ask. Mirrors ServerBlogPostReadService.GetByIdAsync.
        row = await readDb.GroupBlogPosts
            .IgnoreQueryFilters(["GroupAudience"]) // elevated read: audience decided post-load (reveal-aware)
            .Where(p => p.BlogPostId == blogPostId)
            .Select(p => (BlogPostVisibilityFacts?)new BlogPostVisibilityFacts(
                p.BlogPostId, p.AuthorId, p.IsPublished, p.Rating, true, p.GroupId,
                p.Group != null ? p.Group.AudienceRating : Rating.E,
                null)) // group posts are the group's content, not profile-tab data
            .FirstOrDefaultAsync();

        if (row is not null) return row;

        // Site branch (WU-SiteNews): staff announcements — never group-owned, never M-rated
        // (Rating stays E, so the rating gate below always passes for a published post). Parent-
        // visibility invariant enrolment: this branch is what makes comments/likes on a
        // SiteBlogPost visible at all — omitting it would silently hide them, not just skip a
        // feature (identity-and-authorization.md §"Parent-visibility guards").
        return await readDb.SiteBlogPosts
            .Where(p => p.BlogPostId == blogPostId)
            .Select(p => (BlogPostVisibilityFacts?)new BlogPostVisibilityFacts(
                p.BlogPostId, p.AuthorId, p.IsPublished, p.Rating, false, null, null,
                null)) // site announcements are not profile-tab data
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// The rule, pure over already-resolved inputs. This is the single copy — every other entry
    /// point in this class delegates here.
    /// </summary>
    public static bool IsVisible(BlogPostVisibilityFacts facts, IActiveUserContext viewer, bool isRevealed)
    {
        bool isAuthor = viewer.UserId is int uid && uid == facts.AuthorId;

        if (!facts.IsPublished && !isAuthor) return false;
        if (isAuthor) return true;

        // Class A before Class B: a profile post is as visible as its author's profile, and the
        // verified-bot elevation below bypasses consent only, never privacy.
        if (!IsAuthorProfileVisible(facts, viewer)) return false;
        if (viewer.IsVerifiedBot) return true;

        if (facts.IsGroupPost)
        {
            bool audienceHidden = facts.GroupAudience == Rating.M && !viewer.ShowMatureContent;
            return !((audienceHidden || facts.Rating > viewer.MaxRating) && !isRevealed);
        }

        return facts.Rating <= viewer.MaxRating || isRevealed;
    }

    /// <summary>
    /// The profile-privacy half: true for group/site posts (no setting carried) and for profile
    /// posts whose author's <see cref="ProfileVisibility"/> admits this viewer.
    /// </summary>
    private static bool IsAuthorProfileVisible(BlogPostVisibilityFacts facts, IActiveUserContext viewer) =>
        facts.AuthorProfileVisibility is not ProfileVisibility visibility
        || facts.AuthorId is not int authorId
        || ProfileVisibilityGuard.IsVisible(visibility, viewer, authorId);
}
