using Microsoft.EntityFrameworkCore;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Deletes the TPT dependents of a content parent through their <b>base</b> rows — owner ruling D10
/// (WU-TptHardDelete, 2026-09-30). Rule: <c>layer1-data-model.md</c> §"Hard-deleting a content parent";
/// callers and transaction order: <c>layer2-services.md</c> §"Hard deletes of content parents".
/// <para>
/// <b>Why the service must do this — the structural fact.</b> A TPT child row (say
/// <c>chapter_comments</c>) carries two FKs: <c>comment_id → base_comments</c> (base → child; deleting
/// the base row removes both rows) and <c>chapter_id → chapters</c> (parent → child; a cascade there
/// would remove only the child row and orphan its base row). For a parent delete to reach
/// <c>base_comments</c>, the base table would need an FK to the parent, and the polymorphic base has
/// no column to hang one on. Cascades flow along FKs, so <b>no arrangement of <c>ON DELETE
/// CASCADE</c> can make a content-parent delete reach the base rows.</b> The parent → child FKs are
/// therefore RESTRICT: a path that skips this class fails with 23001 (restrict_violation) instead of
/// orphaning rows.
/// </para>
/// <para>
/// <b>Why one statement per child kind is complete — the composition argument.</b>
/// <list type="bullet">
///   <item>Each statement deletes base rows selected through the child table; the base → child
///   CASCADE removes each child row.</item>
///   <item><c>comment_likes</c>, <c>poll_options</c> and <c>poll_votes</c> cascade off the deleted
///   base rows (options off <c>base_polls</c>, votes off options).</item>
///   <item>Reply sets are closed under each scope: all four <c>Post*CommentAsync</c> methods refuse a
///   parent comment from another scope, so every reply of a deleted comment is in the same set.</item>
///   <item>If a reply ever escaped its scope, <c>base_comments.parent_comment_id</c> is SET NULL
///   (owner ruling D12) and reparents it to top level instead of failing the delete.</item>
/// </list>
/// </para>
/// <para>
/// <b>Mechanics.</b> Parameterized raw SQL (<c>ExecuteSqlAsync</c> over a <see cref="FormattableString"/>),
/// executed immediately, inside the <b>caller's</b> transaction. Never LINQ — <c>ExecuteDeleteAsync</c>
/// is unsupported on a TPT base-type <c>DbSet</c>. Never materialized — D10(b) forbids loading the
/// children only to <c>RemoveRange</c> them. Every statement is idempotent, so a retried
/// execution-strategy delegate re-runs it safely.
/// </para>
/// <para>
/// <b>No group-scope method.</b> No group-delete path exists (D47(b) is pending). A future group delete
/// adds <c>GroupDependentsAsync</c> here (group comments, then each group post through
/// <see cref="BlogPostAsync"/>'s shape); until then the RESTRICT FKs on <c>group_comments</c> and
/// <c>group_blog_posts</c> make such a path fail on its first group with content.
/// </para>
/// </summary>
public static class TptDelete
{
    /// <summary>Deletes every comment on one chapter (replies included) through <c>base_comments</c>.</summary>
    public static Task<int> ChapterCommentsAsync(ApplicationDbContext writeDb, int chapterId) =>
        writeDb.Database.ExecuteSqlAsync($"""
            DELETE FROM base_comments
            WHERE comment_id IN (SELECT comment_id FROM chapter_comments WHERE chapter_id = {chapterId})
            """);

    /// <summary>
    /// Deletes every comment on every chapter of one story through <c>base_comments</c> — the
    /// story-scope entry used by the moderation hard delete (and by the D15 author story delete, once
    /// built). The caller then deletes the story; its chapters cascade, and the RESTRICT on
    /// <c>chapter_comments.chapter_id</c> is satisfied because no comment row is left.
    /// </summary>
    public static Task<int> StoryCommentsAsync(ApplicationDbContext writeDb, int storyId) =>
        writeDb.Database.ExecuteSqlAsync($"""
            DELETE FROM base_comments
            WHERE comment_id IN (
                SELECT cc.comment_id
                FROM chapter_comments cc
                JOIN chapters c ON c.chapter_id = cc.chapter_id
                WHERE c.story_id = {storyId})
            """);

    /// <summary>
    /// Deletes every comment on one user's profile wall through <c>base_comments</c> — the account
    /// deletion's half of <c>user_profile_comments.profile_user_id</c>'s RESTRICT (the in-schema
    /// precedent for D10's posture).
    /// </summary>
    public static Task<int> ProfileWallCommentsAsync(ApplicationDbContext writeDb, int profileUserId) =>
        writeDb.Database.ExecuteSqlAsync($"""
            DELETE FROM base_comments
            WHERE comment_id IN (SELECT comment_id FROM user_profile_comments WHERE profile_user_id = {profileUserId})
            """);

    /// <summary>
    /// Deletes one blog post's comments (through <c>base_comments</c>) and polls (through
    /// <c>base_polls</c>; options and votes cascade), leaving the post itself. Used where the post is
    /// a loaded entity the caller removes through EF (the moderation hard delete); otherwise call
    /// <see cref="BlogPostAsync"/>. Works for every <c>BaseBlogPost</c> subtype.
    /// </summary>
    public static async Task BlogPostDependentsAsync(ApplicationDbContext writeDb, int blogPostId)
    {
        await writeDb.Database.ExecuteSqlAsync($"""
            DELETE FROM base_comments
            WHERE comment_id IN (SELECT comment_id FROM blog_post_comments WHERE blog_post_id = {blogPostId})
            """);
        await writeDb.Database.ExecuteSqlAsync($"""
            DELETE FROM base_polls
            WHERE poll_id IN (SELECT poll_id FROM blog_post_polls WHERE blog_post_id = {blogPostId})
            """);
    }

    /// <summary>
    /// Deletes one blog post of any subtype with its dependents: <see cref="BlogPostDependentsAsync"/>,
    /// then the <c>base_blog_posts</c> row. The base → child CASCADE removes the profile, group or site
    /// child row, and the post's likes cascade. Returns the base rows deleted (0 or 1).
    /// </summary>
    public static async Task<int> BlogPostAsync(ApplicationDbContext writeDb, int blogPostId)
    {
        await BlogPostDependentsAsync(writeDb, blogPostId);
        return await writeDb.Database.ExecuteSqlAsync(
            $"DELETE FROM base_blog_posts WHERE blog_post_id = {blogPostId}");
    }
}
