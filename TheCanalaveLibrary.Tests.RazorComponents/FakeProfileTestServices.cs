using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Minimal in-memory fakes for the services injected by <see cref="ProfilePage"/> and its
/// sub-components. Used exclusively by <see cref="ProfilePageTests"/>. All methods not needed
/// for the specific tests return empty/null defaults; the configurable ones expose public setters.
/// </summary>

// ── Profile ───────────────────────────────────────────────────────────────────────────────────

internal sealed class FakeUserProfileReadService : IUserProfileReadService
{
    private readonly ProfileHeaderDto _header;
    public string? BioHtml { get; set; }

    public FakeUserProfileReadService(ProfileHeaderDto header) => _header = header;

    public Task<ProfileHeaderDto?> GetProfileHeaderAsync(int userId, bool includePrivate) =>
        Task.FromResult<ProfileHeaderDto?>(_header);

    public Task<string?> GetProfileTextAsync(int userId) =>
        Task.FromResult(BioHtml);
}

// ── Story ─────────────────────────────────────────────────────────────────────────────────────

internal sealed class FakeStoryReadService : IStoryReadService
{
    public Task<StoryDetailsDTO?> GetStoryByIdAsync(int storyId) => Task.FromResult<StoryDetailsDTO?>(null);
    public Task<StoryUpdateDTO?> GetStoryForEditAsync(int storyId) => Task.FromResult<StoryUpdateDTO?>(null);
    public Task<StoryListingDto[]> GetListingsByIdsAsync(IReadOnlyList<int> storyIds) => Task.FromResult(Array.Empty<StoryListingDto>());
    public Task<(StoryListingDto[] Items, int TotalCount)> GetRecentListingsAsync(int page, int pageSize) => Task.FromResult((Array.Empty<StoryListingDto>(), 0));
    public Task<(StoryListingDto[] Items, int TotalCount)> GetListingsAsync(StoryFilterDto filter, IReadOnlyCollection<int>? restrictToStoryIds = null) => Task.FromResult((Array.Empty<StoryListingDto>(), 0));
    public Task<StoryListingDto[]> GetRandomBatchAsync(StoryFilterDto filter, int batchSize) => Task.FromResult(Array.Empty<StoryListingDto>());
    public Task<IReadOnlyList<int>> FilterCandidateIdsAsync(IReadOnlyCollection<int> candidateIds, StoryFilterDto filter) => Task.FromResult<IReadOnlyList<int>>([.. candidateIds]);
    public Task<IReadOnlyList<int>> GetStoryIdsByAuthorAsync(int authorId) => Task.FromResult<IReadOnlyList<int>>([]);

    /// <summary>Configurable knob for StoryViewStats tests (Feature 45 on-demand reveal).</summary>
    public long TotalViews { get; set; }
    public Task<long> GetStoryTotalViewsAsync(int storyId) => Task.FromResult(TotalViews);

    /// <summary>Seeded-lookup mirror for the story form's "Also posted on" dropdown (WU38d).</summary>
    public Task<IReadOnlyList<ExternalPlatformDto>> GetExternalPlatformsAsync() =>
        Task.FromResult<IReadOnlyList<ExternalPlatformDto>>(
        [
            new ExternalPlatformDto(1, "Archive of Our Own", "archiveofourown.org"),
            new ExternalPlatformDto(2, "FanFiction.Net", "fanfiction.net"),
            new ExternalPlatformDto(7, "Other", null)
        ]);
}

// ── Interaction (read) ────────────────────────────────────────────────────────────────────────

internal sealed class FakeInteractionReadService : IUserStoryInteractionReadService
{
    public Task<UserStoryInteractionStateDto> GetStateAsync(int storyId) => Task.FromResult(UserStoryInteractionStateDto.AllFalse(storyId));
    public Task<IReadOnlyDictionary<int, UserStoryInteractionStateDto>> GetStatesByStoryIdsAsync(IReadOnlyList<int> storyIds) => Task.FromResult<IReadOnlyDictionary<int, UserStoryInteractionStateDto>>(new Dictionary<int, UserStoryInteractionStateDto>());
    public Task<IReadOnlyList<int>> GetBookshelfStoryIdsAsync(BookshelfTab tab) => Task.FromResult<IReadOnlyList<int>>([]);
    public Task<IReadOnlyList<int>> GetFavoriteStoryIdsAsync(int userId, bool includePrivate) => Task.FromResult<IReadOnlyList<int>>([]);
}

// ── Recommendation (read) ─────────────────────────────────────────────────────────────────────

internal sealed class FakeRecommendationReadService : IRecommendationReadService
{
    public Task<List<RecommendationDto>> GetForStoryAsync(int storyId) => Task.FromResult(new List<RecommendationDto>());
    public Task<RecommendationDto?> GetByIdAsync(int recommendationId) => Task.FromResult<RecommendationDto?>(null);
    public Task<IReadOnlyList<int>> GetRecommendedStoryIdsAsync() => Task.FromResult<IReadOnlyList<int>>([]);
    public Task<IReadOnlyList<int>> GetHiddenGemStoryIdsAsync() => Task.FromResult<IReadOnlyList<int>>([]);
    public Task<int?> GetHelpfulPromptRecommendationIdAsync(int storyId) => Task.FromResult<int?>(null);
    public Task<IReadOnlyList<int>> GetRecommendedStoryIdsByUserAsync(int userId) => Task.FromResult<IReadOnlyList<int>>([]);
}

// ── Blog post (read) ──────────────────────────────────────────────────────────────────────────

internal sealed class FakeBlogPostReadService : IBlogPostReadService
{
    public (BlogPostListingDto[] Items, int TotalCount) AuthorResult { get; set; } = ([], 0);

    public Task<BlogPostDto?> GetByIdAsync(int blogPostId) => Task.FromResult<BlogPostDto?>(null);
    public Task<(BlogPostListingDto[] Items, int TotalCount)> GetByAuthorAsync(int authorId, int page, int pageSize, bool includeUnpublished = false) =>
        Task.FromResult(AuthorResult);
    public Task<BlogPostEditDto?> GetForEditAsync(int blogPostId) => Task.FromResult<BlogPostEditDto?>(null);
    public Task<(BlogPostListingDto[] Items, int TotalCount)> GetByGroupAsync(int groupId, int page, int pageSize) =>
        Task.FromResult((Array.Empty<BlogPostListingDto>(), 0));
}

// ── Series (WU41) ─────────────────────────────────────────────────────────────────────────────

internal sealed class FakeSeriesReadService : ISeriesReadService
{
    public Task<SeriesDetailDto?> GetSeriesByIdAsync(int seriesId) => Task.FromResult<SeriesDetailDto?>(null);
    public Task<IReadOnlyList<SeriesListingDto>> GetSeriesByAuthorAsync(int authorId) => Task.FromResult<IReadOnlyList<SeriesListingDto>>([]);
    public Task<IReadOnlyList<StorySeriesMembershipDto>> GetMembershipsForStoryAsync(int storyId) => Task.FromResult<IReadOnlyList<StorySeriesMembershipDto>>([]);
}

// ── Device detection ──────────────────────────────────────────────────────────────────────────

internal sealed class AlwaysDesktopDeviceService : IDeviceDetectionService
{
    public bool IsMobile() => false;
}
