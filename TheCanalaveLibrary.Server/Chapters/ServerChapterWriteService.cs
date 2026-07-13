using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

public class ServerChapterWriteService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser,
    IHtmlSanitizationService sanitizer,
    IWriteRateLimitService rateLimit)
    : ServerChapterReadService(readDbFactory, activeUser), IChapterWriteService
{
    public async Task<int> CreateChapterAsync(CreateChapterDto dto)
    {
        // Chapter creates stamp a nullable AuthorId rather than hard-requiring auth, so the
        // throttle mirrors that: authenticated axis only (security.md "Write Throttling").
        if (ActiveUser.UserId is int throttleUserId)
            rateLimit.EnsureAllowed(WriteActionKind.ContentCreate, throttleUserId);

        // Load story rating for invariant checks before validation.
        Rating? storyRating = await writeDb.Stories
            .Where(s => s.StoryId == dto.StoryId)
            .Select(s => (Rating?)s.Rating)
            .FirstOrDefaultAsync();

        // First version is always the primary — enforce both floor and primary invariants.
        List<string> errors = dto.CanSave(storyRating, isPrimary: true);
        if (errors.Count > 0) throw new ChapterValidationException(errors);

        // Sanitize all user HTML before persisting (layer2-services.md §"User HTML Is Sanitized
        // Once, On Save"). Word count is on the sanitized text (never raw editor output).
        string sanitizedText     = sanitizer.Sanitize(dto.ChapterText);
        string? sanitizedTop     = string.IsNullOrWhiteSpace(dto.TopAuthorsNote)    ? null : sanitizer.Sanitize(dto.TopAuthorsNote);
        string? sanitizedBottom  = string.IsNullOrWhiteSpace(dto.BottomAuthorsNote) ? null : sanitizer.Sanitize(dto.BottomAuthorsNote);
        int wordCount            = ChapterText.CountWords(sanitizedText);

        // Assign the next chapter number (max + 1, or 1 if the story has no chapters yet).
        int nextChapterNumber = await writeDb.Chapters
            .Where(c => c.StoryId == dto.StoryId)
            .Select(c => (int?)c.ChapterNumber)
            .MaxAsync() ?? 0;
        nextChapterNumber++;

        string title = string.IsNullOrWhiteSpace(dto.Title)
            ? $"Chapter {nextChapterNumber}"  // spec intent: nullable title defaults to "Chapter N"
            : dto.Title;

        // Build the connected graph. Chapter.PrimaryContentId is a Restrict FK to ChapterContent —
        // both rows must exist before it can point at anything.  Strategy:
        // 1. Insert Chapter with PrimaryContentId = 0 (temporary placeholder; not the real FK yet).
        // 2. Let EF cascade-insert the first ChapterContent (SortOrder = 0) via the
        //    ChapterContents collection nav.
        // 3. After SaveChanges we have real PKs for both rows; set PrimaryContentId correctly and
        //    save again.  Two round-trips for the initial create only — version adds/edits are one.

        ChapterContent firstVersion = new()
        {
            AuthorId         = ActiveUser.UserId,
            SortOrder        = 0,
            ChapterText      = sanitizedText,
            TopAuthorsNote   = sanitizedTop,
            BottomAuthorsNote = sanitizedBottom,
            WordCount        = wordCount,
            Rating           = dto.Rating,
            VersionName      = dto.VersionName,
            PublishDate      = DateTime.UtcNow
        };

        Chapter chapter = new()
        {
            StoryId          = dto.StoryId,
            ChapterNumber    = nextChapterNumber,
            Title            = title,
            PrimaryContentId = null, // null breaks the circular FK; set after first SaveChanges
            IsPublished      = false, // author explicitly publishes via SetPublishedAsync
            VersionCount     = 1,
            ChapterContents  = [firstVersion]
        };

        // graph-tracked Add: Chapter + ChapterContent inserted in one SaveChanges,
        // same as the Stories pattern (ServerStoryWriteService / WU12 lesson — no Attach()).
        writeDb.Chapters.Add(chapter);
        await writeDb.SaveChangesAsync();

        // PKs are now populated; wire the primary-version pointer and save again.
        chapter.PrimaryContentId = firstVersion.ChapterContentId;
        await writeDb.SaveChangesAsync();

        // Recompute Story.WordCount (primary chapter versions only).
        await RefreshStoryWordCountAsync(dto.StoryId);

        return chapter.ChapterId;
    }

    public async Task<long> AddAlternateVersionAsync(int chapterId, CreateChapterDto dto)
    {
        // Same conditional throttle as CreateChapterAsync (nullable AuthorId contract).
        if (ActiveUser.UserId is int throttleUserId)
            rateLimit.EnsureAllowed(WriteActionKind.ContentCreate, throttleUserId);

        // Load story rating for floor invariant (alternate versions are not primary — no primary invariant).
        Rating? storyRating = await writeDb.Chapters
            .Where(c => c.ChapterId == chapterId)
            .Select(c => (Rating?)c.Story.Rating)
            .FirstOrDefaultAsync();

        List<string> errors = dto.CanSave(storyRating, isPrimary: false);
        if (errors.Count > 0) throw new ChapterValidationException(errors);

        string sanitizedText    = sanitizer.Sanitize(dto.ChapterText);
        string? sanitizedTop    = string.IsNullOrWhiteSpace(dto.TopAuthorsNote)    ? null : sanitizer.Sanitize(dto.TopAuthorsNote);
        string? sanitizedBottom = string.IsNullOrWhiteSpace(dto.BottomAuthorsNote) ? null : sanitizer.Sanitize(dto.BottomAuthorsNote);
        int wordCount           = ChapterText.CountWords(sanitizedText);

        Chapter? chapter = await writeDb.Chapters
            .FirstOrDefaultAsync(c => c.ChapterId == chapterId);
        if (chapter is null) throw new KeyNotFoundException($"Chapter {chapterId} not found.");

        // Next SortOrder = max existing + 1 (unique (ChapterId, SortOrder) index).
        int nextSortOrder = await writeDb.ChapterContents
            .Where(cc => cc.ChapterId == chapterId)
            .Select(cc => (int?)cc.SortOrder)
            .MaxAsync() ?? -1;
        nextSortOrder++;

        ChapterContent altVersion = new()
        {
            ChapterId        = chapterId,
            AuthorId         = ActiveUser.UserId,
            SortOrder        = nextSortOrder,
            ChapterText      = sanitizedText,
            TopAuthorsNote   = sanitizedTop,
            BottomAuthorsNote = sanitizedBottom,
            WordCount        = wordCount,
            Rating           = dto.Rating,
            VersionName      = dto.VersionName,
            PublishDate      = DateTime.UtcNow
        };

        writeDb.ChapterContents.Add(altVersion);
        chapter.VersionCount++;
        await writeDb.SaveChangesAsync();

        return altVersion.ChapterContentId;
    }

    public async Task UpdateChapterContentAsync(UpdateChapterContentDto dto)
    {
        ChapterContent? content = await writeDb.ChapterContents
            .Include(cc => cc.Chapter)
                .ThenInclude(c => c.Story)
            .FirstOrDefaultAsync(cc => cc.ChapterContentId == dto.ChapterContentId);
        if (content is null)
            throw new KeyNotFoundException($"ChapterContent {dto.ChapterContentId} not found.");

        bool isPrimary = content.ChapterContentId == content.Chapter.PrimaryContentId;
        Rating storyRating = content.Chapter.Story.Rating;
        List<string> errors = dto.CanSave(storyRating, isPrimary);
        if (errors.Count > 0) throw new ChapterValidationException(errors);

        string sanitizedText    = sanitizer.Sanitize(dto.ChapterText);
        string? sanitizedTop    = string.IsNullOrWhiteSpace(dto.TopAuthorsNote)    ? null : sanitizer.Sanitize(dto.TopAuthorsNote);
        string? sanitizedBottom = string.IsNullOrWhiteSpace(dto.BottomAuthorsNote) ? null : sanitizer.Sanitize(dto.BottomAuthorsNote);

        content.ChapterText      = sanitizedText;
        content.TopAuthorsNote   = sanitizedTop;
        content.BottomAuthorsNote = sanitizedBottom;
        content.WordCount        = ChapterText.CountWords(sanitizedText);
        content.Rating           = dto.Rating;
        content.VersionName      = dto.VersionName;

        // If a title was supplied, also update the parent chapter's title.
        if (!string.IsNullOrWhiteSpace(dto.Title))
            content.Chapter.Title = dto.Title;

        await writeDb.SaveChangesAsync();

        // Recompute Story.WordCount (primary version's word count may have changed).
        await RefreshStoryWordCountAsync(content.Chapter.StoryId);
    }

    public async Task SetPrimaryVersionAsync(int chapterId, long chapterContentId)
    {
        Chapter? chapter = await writeDb.Chapters
            .Include(c => c.Story)
            .FirstOrDefaultAsync(c => c.ChapterId == chapterId);
        if (chapter is null) throw new KeyNotFoundException($"Chapter {chapterId} not found.");

        ChapterContent? targetContent = await writeDb.ChapterContents
            .FirstOrDefaultAsync(cc => cc.ChapterContentId == chapterContentId && cc.ChapterId == chapterId);
        if (targetContent is null)
            throw new KeyNotFoundException(
                $"ChapterContent {chapterContentId} does not belong to chapter {chapterId}.");

        // Primary invariant: effective rating of the new primary must equal story rating.
        Rating storyRating = chapter.Story.Rating;
        Rating effectiveRating = targetContent.Rating ?? storyRating;
        if (effectiveRating != storyRating)
            throw new ChapterValidationException(
                [$"To make this the default version, the story must be rated {effectiveRating} first, or change the version's rating to inherit/match the story's rating ({storyRating})."]);

        chapter.PrimaryContentId = chapterContentId;
        await writeDb.SaveChangesAsync();

        // Recompute Story.WordCount — the primary version's word count is now different.
        await RefreshStoryWordCountAsync(chapter.StoryId);
    }

    public async Task SetPublishedAsync(int chapterId, bool isPublished)
    {
        Chapter? chapter = await writeDb.Chapters
            .FirstOrDefaultAsync(c => c.ChapterId == chapterId);
        if (chapter is null) throw new KeyNotFoundException($"Chapter {chapterId} not found.");

        chapter.IsPublished = isPublished;
        await writeDb.SaveChangesAsync();
        // Story.ChapterCount is not a stored column — it's computed from Chapters.Count(IsPublished)
        // in EF projections. No counter to maintain here (forward_plan.md "Story.ChapterCount" Resolved).
    }

    public async Task MoveChapterAsync(int storyId, int fromNumber, int toNumber)
    {
        int userId = RequireAuthenticatedUser();
        if (fromNumber == toNumber) return;

        Story? story = await writeDb.Stories.FirstOrDefaultAsync(s => s.StoryId == storyId);
        if (story is null) throw new KeyNotFoundException($"Story {storyId} not found.");
        if (story.AuthorId != userId)
            throw new UnauthorizedAccessException("You must be the author of this story.");

        int chapterCount = await writeDb.Chapters.CountAsync(c => c.StoryId == storyId);
        if (toNumber < 1 || toNumber > chapterCount)
            throw new ChapterValidationException(
                [$"Target position must be between 1 and {chapterCount}."]);

        // EnableRetryOnFailure refuses bare user transactions — the whole unit runs through the
        // execution strategy (UserDeletionService precedent).
        var strategy = writeDb.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await writeDb.Database.BeginTransactionAsync();

            int lo = Math.Min(fromNumber, toNumber);
            int hi = Math.Max(fromNumber, toNumber);
            List<Chapter> affected = await writeDb.Chapters
                .Where(c => c.StoryId == storyId && c.ChapterNumber >= lo && c.ChapterNumber <= hi)
                .ToListAsync();
            Chapter? moved = affected.FirstOrDefault(c => c.ChapterNumber == fromNumber);
            if (moved is null) throw new KeyNotFoundException(
                $"Story {storyId} has no chapter {fromNumber}.");

            // Compute final numbers, then land them via a negative pass. The unique
            // (story_id, chapter_number) index is checked per-row inside a single UPDATE too, so
            // a direct ±1 range shift can transiently collide; negating the affected range first
            // frees every positive slot, then each row lands on its final number conflict-free.
            Dictionary<int, int> finalByChapterId = affected.ToDictionary(
                c => c.ChapterId,
                c => c.ChapterNumber == fromNumber
                    ? toNumber
                    : toNumber < fromNumber ? c.ChapterNumber + 1 : c.ChapterNumber - 1);

            foreach (Chapter c in affected) c.ChapterNumber = -c.ChapterNumber;
            await writeDb.SaveChangesAsync();
            foreach (Chapter c in affected) c.ChapterNumber = finalByChapterId[c.ChapterId];
            await writeDb.SaveChangesAsync();

            await ShiftArcsForMoveAsync(storyId, fromNumber, toNumber);
            await tx.CommitAsync();
        });
    }

    public async Task DeleteChapterAsync(int chapterId)
    {
        int userId = RequireAuthenticatedUser();

        Chapter? chapter = await writeDb.Chapters
            .Include(c => c.Story)
            .FirstOrDefaultAsync(c => c.ChapterId == chapterId);
        if (chapter is null) throw new KeyNotFoundException($"Chapter {chapterId} not found.");
        if (chapter.Story.AuthorId != userId)
            throw new UnauthorizedAccessException("You must be the author of this story.");

        int storyId = chapter.StoryId;
        int deletedNumber = chapter.ChapterNumber;

        var strategy = writeDb.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await writeDb.Database.BeginTransactionAsync();

            // TPT trap: the DB cascade Chapter→chapter_comments removes only the CHILD table's
            // rows and would orphan their base_comments rows. Deleting through EF removes both
            // tables' rows per entity. Replies are also ChapterComments of the same chapter, so
            // this set is closed under the parent/reply relationship.
            List<ChapterComment> comments = await writeDb.ChapterComments
                .Where(cc => cc.ChapterId == chapterId)
                .ToListAsync();
            if (comments.Count > 0) writeDb.ChapterComments.RemoveRange(comments);

            // Release the Restrict FK before the row delete (mirror of the two-step create),
            // then let the delete cascade to contents / read state.
            chapter.PrimaryContentId = null;
            await writeDb.SaveChangesAsync();
            writeDb.Chapters.Remove(chapter);
            await writeDb.SaveChangesAsync();

            // Shift later chapters down. Same negative-pass discipline as MoveChapterAsync —
            // a direct "-1 everything above D" UPDATE can transiently collide per-row.
            List<Chapter> later = await writeDb.Chapters
                .Where(c => c.StoryId == storyId && c.ChapterNumber > deletedNumber)
                .ToListAsync();
            if (later.Count > 0)
            {
                foreach (Chapter c in later) c.ChapterNumber = -c.ChapterNumber;
                await writeDb.SaveChangesAsync();
                foreach (Chapter c in later) c.ChapterNumber = -c.ChapterNumber - 1;
                await writeDb.SaveChangesAsync();
            }

            // Arc bounds shrink: Start moves when the deletion was before it, End when at/before.
            // An arc reduced to Start > End covered only the deleted chapter — auto-delete (WU45).
            List<StoryArc> arcs = await writeDb.StoryArcs
                .Where(a => a.StoryId == storyId)
                .ToListAsync();
            foreach (StoryArc arc in arcs)
            {
                if (deletedNumber < arc.StartChapterNumber) arc.StartChapterNumber--;
                if (deletedNumber <= arc.EndChapterNumber) arc.EndChapterNumber--;
                if (arc.StartChapterNumber > arc.EndChapterNumber)
                    writeDb.StoryArcs.Remove(arc);
            }
            await writeDb.SaveChangesAsync();

            await tx.CommitAsync();
        });

        await RefreshStoryWordCountAsync(storyId);
    }

    /// <summary>
    /// Applies a P→Q move to every arc of the story as the remove-at-P + insert-at-Q composition
    /// (WU45 settled rule — Start/End each move independently by whether the change point falls
    /// at/before them; applied uniformly so no-overlap and ordering invariants are preserved).
    /// An arc emptied by the removal step (its only chapter was moved away) is auto-deleted.
    /// </summary>
    private async Task ShiftArcsForMoveAsync(int storyId, int fromNumber, int toNumber)
    {
        List<StoryArc> arcs = await writeDb.StoryArcs
            .Where(a => a.StoryId == storyId)
            .ToListAsync();

        foreach (StoryArc arc in arcs)
        {
            int s = arc.StartChapterNumber;
            int e = arc.EndChapterNumber;

            // Remove at fromNumber…
            if (fromNumber < s) s--;
            if (fromNumber <= e) e--;
            if (s > e) { writeDb.StoryArcs.Remove(arc); continue; } // single-chapter arc vacated

            // …then insert at toNumber (same composition verified in the WU45 deliberation:
            // moving a chapter INTO an arc's span grows it; moving one out shrinks it; arcs
            // wholly before/after the affected range are untouched).
            if (toNumber <= s) s++;
            if (toNumber <= e) e++;

            arc.StartChapterNumber = s;
            arc.EndChapterNumber   = e;
        }
        await writeDb.SaveChangesAsync();
    }

    private int RequireAuthenticatedUser()
    {
        if (ActiveUser.UserId is not int id)
            throw new InvalidOperationException("This operation requires an authenticated user.");
        return id;
    }

    // Recomputes Story.WordCount as the sum of each primary ChapterContent's WordCount.
    // Called after any operation that may change a chapter's primary word count.
    // Also updates the author's WordsWritten UserStat by the delta (cross-cutting.md §"UserStats Updates").
    private async Task RefreshStoryWordCountAsync(int storyId)
    {
        // Sum word counts of primary ChapterContent rows for this story.
        // Chapters with null PrimaryContentId (brief create window) contribute 0.
        int totalWords = await writeDb.Chapters
            .Where(c => c.StoryId == storyId && c.PrimaryContentId != null)
            .SumAsync(c => c.PrimaryContent!.WordCount);

        Story? story = await writeDb.Stories.FindAsync(storyId);
        if (story is not null)
        {
            int wordDelta = totalWords - story.WordCount;
            story.WordCount = totalWords;
            await writeDb.SaveChangesAsync();

            // Update the author's WordsWritten counter by the word-count delta.
            if (wordDelta != 0)
            {
                await writeDb.UserStats.Where(us => us.UserId == story.AuthorId)
                    .ExecuteUpdateAsync(s => s.SetProperty(us => us.WordsWritten, us => us.WordsWritten + wordDelta));
            }
        }
    }
}
