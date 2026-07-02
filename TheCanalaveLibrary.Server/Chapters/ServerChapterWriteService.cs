using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

public class ServerChapterWriteService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser,
    IHtmlSanitizationService sanitizer)
    : ServerChapterReadService(readDbFactory, activeUser), IChapterWriteService
{
    public async Task<int> CreateChapterAsync(CreateChapterDto dto)
    {
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
