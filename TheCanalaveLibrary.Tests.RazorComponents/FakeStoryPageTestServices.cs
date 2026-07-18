using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Minimal in-memory fakes for the supplementary services injected by <see cref="StoryPage"/>
/// (WU-ResponsiveMerge — the former StoryDesktop render tests now target the page, which loads
/// its own data). All methods not needed for the tests return empty/null defaults; the chapter
/// list is configurable.
/// </summary>

// ── Chapters (read) ───────────────────────────────────────────────────────────────────────────

internal sealed class FakeChapterReadService : IChapterReadService
{
    /// <summary>Configurable knob — what <c>GetChapterListAsync</c> returns to the page.</summary>
    public IReadOnlyList<ChapterListEntryDto> ChapterList { get; set; } = [];

    public Task<ChapterReadingDto?> GetChapterForReadingAsync(int storyId, int chapterNumber, int? versionOrder = null) =>
        Task.FromResult<ChapterReadingDto?>(null);
    public Task<IReadOnlyList<ChapterTocEntryDto>> GetChapterTocAsync(int storyId) =>
        Task.FromResult<IReadOnlyList<ChapterTocEntryDto>>([]);
    public Task<IReadOnlyList<ChapterVersionDto>> GetChapterVersionsAsync(int storyId, int chapterNumber) =>
        Task.FromResult<IReadOnlyList<ChapterVersionDto>>([]);
    public Task<IReadOnlyList<ChapterListEntryDto>> GetChapterListAsync(int storyId) =>
        Task.FromResult(ChapterList);
    public Task<DateTime?> GetViewerLastInteractionUtcAsync(int storyId) =>
        Task.FromResult<DateTime?>(null);
    public Task<ChapterReadingDto?> GetChapterForEditAsync(long chapterContentId) =>
        Task.FromResult<ChapterReadingDto?>(null);
    public Task<IReadOnlyList<ChapterExportDto>> GetChaptersForExportAsync(int storyId) =>
        Task.FromResult<IReadOnlyList<ChapterExportDto>>([]);
}

// ── Story lineage (read, Feature 10/WU42) ─────────────────────────────────────────────────────

internal sealed class FakeStoryLineageReadService : IStoryLineageReadService
{
    public Task<IReadOnlyList<StoryLineageDto>> GetLineageForStoryAsync(int storyId) =>
        Task.FromResult<IReadOnlyList<StoryLineageDto>>([]);
    public Task<StoryLineageManageDto> GetManageDataForUserAsync() =>
        Task.FromResult(new StoryLineageManageDto([], []));
    public Task<IReadOnlyList<StoryLineageTypeDto>> GetLineageTypesAsync() =>
        Task.FromResult<IReadOnlyList<StoryLineageTypeDto>>([]);
}

// ── Story arcs (read, WU45) ───────────────────────────────────────────────────────────────────

internal sealed class FakeStoryArcReadService : IStoryArcReadService
{
    public Task<IReadOnlyList<StoryArcDto>> GetArcsForStoryAsync(int storyId) =>
        Task.FromResult<IReadOnlyList<StoryArcDto>>([]);
}

// ── View counting (write, Feature 45) ─────────────────────────────────────────────────────────

internal sealed class FakeViewCountWriteService : IViewCountWriteService
{
    public Task RecordViewAsync(int storyId) => Task.CompletedTask;
}
