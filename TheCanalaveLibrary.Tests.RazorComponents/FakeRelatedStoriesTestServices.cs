using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// In-memory fakes for the services injected by <see cref="RelatedStoriesSection"/> (Feature 61,
/// WU-RelatedStories). Used exclusively by <see cref="RelatedStoriesSectionTests"/>.
/// </summary>

internal sealed class FakeCoOccurrenceReadService : ICoOccurrenceReadService
{
    public List<RelatedStoryScoreDto> FavoritedResult { get; set; } = [];
    public List<RelatedStoryScoreDto> RecommendedResult { get; set; } = [];

    public List<(int StoryId, int Take, IReadOnlyList<UserStoryInteractionTypeEnum>? Excluded)> FavoritedCalls { get; } = [];
    public List<(int StoryId, int Take, IReadOnlyList<UserStoryInteractionTypeEnum>? Excluded)> RecommendedCalls { get; } = [];

    public Task<IReadOnlyList<RelatedStoryScoreDto>> GetAlsoFavoritedAsync(
        int storyId, int take = 10,
        IReadOnlyList<UserStoryInteractionTypeEnum>? excludedInteractions = null,
        CancellationToken ct = default)
    {
        FavoritedCalls.Add((storyId, take, excludedInteractions));
        return Task.FromResult<IReadOnlyList<RelatedStoryScoreDto>>(FavoritedResult);
    }

    public Task<IReadOnlyList<RelatedStoryScoreDto>> GetAlsoRecommendedAsync(
        int storyId, int take = 10,
        IReadOnlyList<UserStoryInteractionTypeEnum>? excludedInteractions = null,
        CancellationToken ct = default)
    {
        RecommendedCalls.Add((storyId, take, excludedInteractions));
        return Task.FromResult<IReadOnlyList<RelatedStoryScoreDto>>(RecommendedResult);
    }
}

internal sealed class FakeRelatedStoriesStoryReadService : IStoryReadService
{
    public Dictionary<int, StoryListingDto> StoriesById { get; set; } = new();

    public Task<StoryDetailsDTO?> GetStoryByIdAsync(int storyId) => Task.FromResult<StoryDetailsDTO?>(null);
    public Task<StoryUpdateDTO?> GetStoryForEditAsync(int storyId) => Task.FromResult<StoryUpdateDTO?>(null);

    public Task<StoryListingDto[]> GetListingsByIdsAsync(IReadOnlyList<int> storyIds) =>
        Task.FromResult(storyIds.Where(StoriesById.ContainsKey).Select(id => StoriesById[id]).ToArray());

    public Task<(StoryListingDto[] Items, int TotalCount)> GetRecentListingsAsync(int page, int pageSize) =>
        Task.FromResult((Array.Empty<StoryListingDto>(), 0));
    public Task<(StoryListingDto[] Items, int TotalCount)> GetListingsAsync(
        StoryFilterDto filter, IReadOnlyCollection<int>? restrictToStoryIds = null) =>
        Task.FromResult((Array.Empty<StoryListingDto>(), 0));
    public Task<StoryListingDto[]> GetRandomBatchAsync(StoryFilterDto filter, int batchSize) =>
        Task.FromResult(Array.Empty<StoryListingDto>());
    public Task<IReadOnlyList<int>> FilterCandidateIdsAsync(IReadOnlyCollection<int> candidateIds, StoryFilterDto filter) =>
        Task.FromResult<IReadOnlyList<int>>([.. candidateIds]);
    public Task<IReadOnlyList<int>> GetStoryIdsByAuthorAsync(int authorId) => Task.FromResult<IReadOnlyList<int>>([]);
    public Task<IReadOnlyList<ExternalPlatformDto>> GetExternalPlatformsAsync() =>
        Task.FromResult<IReadOnlyList<ExternalPlatformDto>>([]);
    public Task<long> GetStoryTotalViewsAsync(int storyId) => Task.FromResult(0L);
    public Task<IReadOnlyList<StoryTitleSearchDto>> SearchStoriesByTitleAsync(string term) =>
        Task.FromResult<IReadOnlyList<StoryTitleSearchDto>>([]);
}

internal sealed class FakeRelatedStoriesInteractionReadService : IUserStoryInteractionReadService
{
    public IReadOnlyDictionary<int, UserStoryInteractionStateDto> States { get; set; } =
        new Dictionary<int, UserStoryInteractionStateDto>();

    public Task<UserStoryInteractionStateDto> GetStateAsync(int storyId) =>
        Task.FromResult(UserStoryInteractionStateDto.AllFalse(storyId));
    public Task<IReadOnlyDictionary<int, UserStoryInteractionStateDto>> GetStatesByStoryIdsAsync(
        IReadOnlyList<int> storyIds) => Task.FromResult(States);
    public Task<IReadOnlyList<int>> GetBookshelfStoryIdsAsync(BookshelfTab tab) => Task.FromResult<IReadOnlyList<int>>([]);
    public Task<IReadOnlyList<int>> GetFavoriteStoryIdsAsync(int userId, bool includePrivate) =>
        Task.FromResult<IReadOnlyList<int>>([]);
}

internal sealed class FakeDiscoveryDefaultsReadService : IDiscoveryDefaultsReadService
{
    public IReadOnlyList<UserStoryInteractionTypeEnum> Defaults { get; set; } = [UserStoryInteractionTypeEnum.Ignore];

    public Task<IReadOnlyList<UserStoryInteractionTypeEnum>> GetDefaultExcludedInteractionsAsync(string searchModeKey) =>
        Task.FromResult(Defaults);
}
