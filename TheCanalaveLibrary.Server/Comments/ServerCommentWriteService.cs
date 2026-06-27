using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side write implementation for Comments. Inherits the read path via primary-constructor
/// chaining. Sanitizes all user HTML before persisting (layer2-services.md
/// §"User HTML Is Sanitized Once, On Save"). Edit and delete are author-only; moderation delete
/// (WU34) will bypass the ownership check via a separate admin service or method.
/// </summary>
public class ServerCommentWriteService(
    ReadOnlyApplicationDbContext readDb,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser,
    IHtmlSanitizationService sanitizer)
    : ServerCommentReadService(readDb, activeUser), ICommentWriteService
{
    public async Task<long> PostChapterCommentAsync(PostChapterCommentDto dto)
    {
        if (ActiveUser.UserId is not int userId)
            throw new InvalidOperationException("Posting a comment requires an authenticated user.");

        List<string> errors = dto.CanSave();
        if (errors.Count > 0) throw new CommentValidationException(errors);

        // Verify the chapter exists.
        bool chapterExists = await writeDb.Chapters.AnyAsync(c => c.ChapterId == dto.ChapterId);
        if (!chapterExists)
            throw new KeyNotFoundException($"Chapter {dto.ChapterId} not found.");

        // If replying, verify the parent belongs to the same chapter.
        if (dto.ParentCommentId.HasValue)
        {
            bool parentOnSameChapter = await writeDb.ChapterComments
                .AnyAsync(cc =>
                    cc.CommentId == dto.ParentCommentId.Value
                    && cc.ChapterId == dto.ChapterId);
            if (!parentOnSameChapter)
                throw new KeyNotFoundException(
                    $"Parent comment {dto.ParentCommentId} not found on chapter {dto.ChapterId}.");
        }

        string sanitizedText = sanitizer.Sanitize(dto.CommentText);

        ChapterComment comment = new()
        {
            ChapterId        = dto.ChapterId,
            ParentCommentId  = dto.ParentCommentId,
            UserId           = userId,
            CommentText      = sanitizedText,
            IsSpoiler        = dto.IsSpoiler,
            DatePosted       = DateTime.UtcNow
        };

        writeDb.ChapterComments.Add(comment);
        await writeDb.SaveChangesAsync();

        // Increment CommentsWritten counter (cross-cutting.md §"UserStats Updates").
        await writeDb.UserStats.Where(us => us.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(us => us.CommentsWritten, us => us.CommentsWritten + 1));

        // TODO(WU22): notify story author of new comment, and parent-comment author of reply.

        return comment.CommentId;
    }

    public async Task<long> PostBlogPostCommentAsync(PostBlogPostCommentDto dto)
    {
        if (ActiveUser.UserId is not int userId)
            throw new InvalidOperationException("Posting a comment requires an authenticated user.");

        List<string> errors = dto.CanSave();
        if (errors.Count > 0) throw new CommentValidationException(errors);

        // Verify the blog post exists.
        bool postExists = await writeDb.BlogPosts.AnyAsync(p => p.BlogPostId == dto.BlogPostId);
        if (!postExists)
            throw new KeyNotFoundException($"Blog post {dto.BlogPostId} not found.");

        // If replying, verify the parent belongs to the same blog post.
        if (dto.ParentCommentId.HasValue)
        {
            bool parentOnSamePost = await writeDb.BlogPostComments
                .AnyAsync(bc =>
                    bc.CommentId == dto.ParentCommentId.Value
                    && bc.BlogPostId == dto.BlogPostId);
            if (!parentOnSamePost)
                throw new KeyNotFoundException(
                    $"Parent comment {dto.ParentCommentId} not found on blog post {dto.BlogPostId}.");
        }

        string sanitizedText = sanitizer.Sanitize(dto.CommentText);

        // Note: BlogPostComment has no IsSpoiler property — spoiler lives on the post itself
        // (ProfileBlogPost.HasSpoilers). Only ChapterComment extends BaseComment with IsSpoiler.
        BlogPostComment comment = new()
        {
            BlogPostId      = dto.BlogPostId,
            ParentCommentId = dto.ParentCommentId,
            UserId          = userId,
            CommentText     = sanitizedText,
            DatePosted      = DateTime.UtcNow
        };

        writeDb.BlogPostComments.Add(comment);
        await writeDb.SaveChangesAsync();

        // Increment CommentsWritten counter (cross-cutting.md §"UserStats Updates").
        await writeDb.UserStats.Where(us => us.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(us => us.CommentsWritten, us => us.CommentsWritten + 1));

        // TODO(WU33): notify blog post author of new comment, and parent-comment author of reply.

        return comment.CommentId;
    }

    public async Task<long> PostGroupCommentAsync(PostGroupCommentDto dto)
    {
        if (ActiveUser.UserId is not int userId)
            throw new InvalidOperationException("Posting a comment requires an authenticated user.");

        List<string> errors = dto.CanSave();
        if (errors.Count > 0) throw new CommentValidationException(errors);

        // Verify the group exists.
        bool groupExists = await writeDb.Groups.AnyAsync(g => g.GroupId == dto.GroupId);
        if (!groupExists)
            throw new KeyNotFoundException($"Group {dto.GroupId} not found.");

        // If replying, verify the parent belongs to the same group.
        if (dto.ParentCommentId.HasValue)
        {
            bool parentOnSameGroup = await writeDb.GroupComments
                .AnyAsync(gc =>
                    gc.CommentId == dto.ParentCommentId.Value
                    && gc.GroupId == dto.GroupId);
            if (!parentOnSameGroup)
                throw new KeyNotFoundException(
                    $"Parent comment {dto.ParentCommentId} not found on group {dto.GroupId}.");
        }

        string sanitizedText = sanitizer.Sanitize(dto.CommentText);

        // No IsSpoiler on group comments — spoilers are a chapter-only concept.
        GroupComment comment = new()
        {
            GroupId         = dto.GroupId,
            ParentCommentId = dto.ParentCommentId,
            UserId          = userId,
            CommentText     = sanitizedText,
            DatePosted      = DateTime.UtcNow
        };

        writeDb.GroupComments.Add(comment);
        await writeDb.SaveChangesAsync();

        // Increment CommentsWritten counter (cross-cutting.md §"UserStats Updates").
        await writeDb.UserStats.Where(us => us.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(us => us.CommentsWritten, us => us.CommentsWritten + 1));

        return comment.CommentId;
    }

    public async Task<long> PostUserProfileCommentAsync(PostUserProfileCommentDto dto)
    {
        if (ActiveUser.UserId is not int userId)
            throw new InvalidOperationException("Posting a comment requires an authenticated user.");

        List<string> errors = dto.CanSave();
        if (errors.Count > 0) throw new CommentValidationException(errors);

        // Verify the profile user exists.
        bool profileUserExists = await writeDb.Users.AnyAsync(u => u.Id == dto.ProfileUserId);
        if (!profileUserExists)
            throw new KeyNotFoundException($"Profile user {dto.ProfileUserId} not found.");

        // If replying, verify the parent belongs to the same profile wall.
        if (dto.ParentCommentId.HasValue)
        {
            bool parentOnSameProfile = await writeDb.UserProfileComments
                .AnyAsync(uc =>
                    uc.CommentId == dto.ParentCommentId.Value
                    && uc.ProfileUserId == dto.ProfileUserId);
            if (!parentOnSameProfile)
                throw new KeyNotFoundException(
                    $"Parent comment {dto.ParentCommentId} not found on profile {dto.ProfileUserId}.");
        }

        string sanitizedText = sanitizer.Sanitize(dto.CommentText);

        // No IsSpoiler on profile-wall comments — spoilers are a chapter-only concept.
        UserProfileComment comment = new()
        {
            ProfileUserId   = dto.ProfileUserId,
            ParentCommentId = dto.ParentCommentId,
            UserId          = userId,
            CommentText     = sanitizedText,
            DatePosted      = DateTime.UtcNow
        };

        writeDb.UserProfileComments.Add(comment);
        await writeDb.SaveChangesAsync();

        // Increment CommentsWritten counter (cross-cutting.md §"UserStats Updates").
        await writeDb.UserStats.Where(us => us.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(us => us.CommentsWritten, us => us.CommentsWritten + 1));

        // TODO(WU33): notify profile owner of new comment, and parent-comment author of reply.

        return comment.CommentId;
    }

    public async Task EditCommentAsync(UpdateCommentDto dto)
    {
        if (ActiveUser.UserId is not int userId)
            throw new InvalidOperationException("Editing a comment requires an authenticated user.");

        List<string> errors = dto.CanSave();
        if (errors.Count > 0) throw new CommentValidationException(errors);

        BaseComment? comment = await writeDb.BaseComments
            .FirstOrDefaultAsync(c => c.CommentId == dto.CommentId);
        if (comment is null)
            throw new KeyNotFoundException($"Comment {dto.CommentId} not found.");

        if (comment.UserId != userId)
            throw new UnauthorizedAccessException(
                "You can only edit your own comments.");

        comment.CommentText = sanitizer.Sanitize(dto.CommentText);
        await writeDb.SaveChangesAsync();
    }

    public async Task DeleteCommentAsync(long commentId)
    {
        if (ActiveUser.UserId is not int userId)
            throw new InvalidOperationException("Deleting a comment requires an authenticated user.");

        BaseComment? comment = await writeDb.BaseComments
            .FirstOrDefaultAsync(c => c.CommentId == commentId);
        if (comment is null)
            throw new KeyNotFoundException($"Comment {commentId} not found.");

        if (comment.UserId != userId)
            throw new UnauthorizedAccessException(
                "You can only delete your own comments.");

        // Hard delete. DB FKs handle the rest:
        //  • ParentCommentId SET NULL  → replies become flat top-level comments.
        //  • CommentLike CASCADE       → likes removed.
        //  • FeatureContribution SET NULL → attribution nulled.
        int? authorId = comment.UserId;
        writeDb.BaseComments.Remove(comment);
        await writeDb.SaveChangesAsync();

        // Decrement CommentsWritten counter for the comment's author (cross-cutting.md §"UserStats Updates").
        if (authorId.HasValue)
        {
            await writeDb.UserStats.Where(us => us.UserId == authorId.Value)
                .ExecuteUpdateAsync(s => s.SetProperty(us => us.CommentsWritten, us => us.CommentsWritten - 1));
        }
    }

    public async Task<CommentLikeResultDto> ToggleLikeAsync(long commentId)
    {
        if (ActiveUser.UserId is not int userId)
            throw new InvalidOperationException("Liking a comment requires an authenticated user.");

        // Load comment + its existing CommentLike row for this user in one round-trip.
        BaseComment? comment = await writeDb.BaseComments
            .Include(c => c.Likes.Where(l => l.UserId == userId))
            .FirstOrDefaultAsync(c => c.CommentId == commentId);
        if (comment is null)
            throw new KeyNotFoundException($"Comment {commentId} not found.");

        bool nowLiked;
        int delta;
        CommentLike? existingLike = comment.Likes.FirstOrDefault();

        if (existingLike is not null)
        {
            // Already liked — remove the like.
            writeDb.CommentLikes.Remove(existingLike);
            nowLiked = false;
            delta = -1;
        }
        else
        {
            // Not yet liked — add the like.
            writeDb.CommentLikes.Add(new CommentLike { CommentId = commentId, UserId = userId });
            nowLiked = true;
            delta = 1;
        }

        await writeDb.SaveChangesAsync();
        // No notification generated — anti-addictive design (§6.11).

        // Atomic counter update — see cross-cutting.md §"Counter mutation rule" for why
        // ExecuteUpdateAsync is used here instead of tracked read-modify-write.
        await writeDb.BaseComments
            .Where(c => c.CommentId == commentId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.LikeCount, c => c.LikeCount + delta));

        return new CommentLikeResultDto(Math.Max(0, comment.LikeCount + delta), nowLiked);
    }
}
