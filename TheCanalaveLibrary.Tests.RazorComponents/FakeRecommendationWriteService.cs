using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// In-memory stand-in for <see cref="IRecommendationWriteService"/> used by
/// <see cref="RecommendationSectionTests"/> and related render tests. Records each call so tests
/// can assert which methods were invoked without needing a host or database.
/// </summary>
public class FakeRecommendationWriteService : IRecommendationWriteService
{
    // ── Read tracking ─────────────────────────────────────────────────────────────

    private List<RecommendationDto> _getForStoryResult = [];

    public void SetGetForStoryResult(List<RecommendationDto> result) => _getForStoryResult = result;

    public List<int> GetForStoryCalls { get; } = [];

    public Task<List<RecommendationDto>> GetForStoryAsync(int storyId)
    {
        GetForStoryCalls.Add(storyId);
        return Task.FromResult(_getForStoryResult);
    }

    public Task<RecommendationDto?> GetByIdAsync(int recommendationId)
        => Task.FromResult(_getForStoryResult.FirstOrDefault(r => r.RecommendationId == recommendationId));

    public Task<IReadOnlyList<int>> GetRecommendedStoryIdsAsync() =>
        Task.FromResult<IReadOnlyList<int>>([]);

    public Task<IReadOnlyList<int>> GetHiddenGemStoryIdsAsync() =>
        Task.FromResult<IReadOnlyList<int>>([]);

    private int? _helpfulPromptRecId;
    public void SetHelpfulPromptRecommendationId(int? recId) => _helpfulPromptRecId = recId;
    public Task<int?> GetHelpfulPromptRecommendationIdAsync(int storyId) =>
        Task.FromResult(_helpfulPromptRecId);

    // ── Write tracking ────────────────────────────────────────────────────────────

    public List<RecommendationSubmitDto> SubmitCalls { get; } = [];
    public List<UpdateRecommendationDto> EditCalls { get; } = [];
    public List<int> DeleteCalls { get; } = [];
    public List<int> ToggleLikeCalls { get; } = [];
    public List<(int Id, bool IsHiddenGem)> SetHiddenGemCalls { get; } = [];
    public List<(int Id, bool IsHighlighted)> SetHighlightedCalls { get; } = [];
    public List<int> RecordSuccessCalls { get; } = [];
    public List<(int StoryId, int RecId)> RecordAttributionCalls { get; } = [];

    private RecommendationLikeResultDto _likeResult = new(0, false);

    public void SetLikeResult(RecommendationLikeResultDto result) => _likeResult = result;

    public Task<int> SubmitAsync(RecommendationSubmitDto dto)
    {
        SubmitCalls.Add(dto);
        return Task.FromResult(1);
    }

    public Task EditAsync(UpdateRecommendationDto dto)
    {
        EditCalls.Add(dto);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(int recommendationId)
    {
        DeleteCalls.Add(recommendationId);
        return Task.CompletedTask;
    }

    public Task<RecommendationLikeResultDto> ToggleLikeAsync(int recommendationId)
    {
        ToggleLikeCalls.Add(recommendationId);
        return Task.FromResult(_likeResult);
    }

    public Task SetHiddenGemAsync(int recommendationId, bool isHiddenGem)
    {
        SetHiddenGemCalls.Add((recommendationId, isHiddenGem));
        return Task.CompletedTask;
    }

    public Task SetHighlightedByAuthorAsync(int recommendationId, bool isHighlighted)
    {
        SetHighlightedCalls.Add((recommendationId, isHighlighted));
        return Task.CompletedTask;
    }

    public Task RecordSuccessAsync(int recommendationId)
    {
        RecordSuccessCalls.Add(recommendationId);
        return Task.CompletedTask;
    }

    public Task RecordAttributionSourceAsync(int storyId, int recommendationId)
    {
        RecordAttributionCalls.Add((storyId, recommendationId));
        return Task.CompletedTask;
    }
}
