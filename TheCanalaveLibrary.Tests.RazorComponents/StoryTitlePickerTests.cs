using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="StoryTitlePicker"/> (WU42) — the reusable title-search typeahead
/// (Story Lineage's target picker; also retrofits Groups' add-story entry). Wraps
/// <c>CanalaveTypeahead</c> internally (in-house, Global Flip wave), same as <see cref="TagSelector"/>.
///
/// <b>What is NOT tested here:</b> picking a story via the typeahead (keyboard input → search →
/// selection → <see cref="StoryTitlePicker.OnStorySelected"/> firing) requires JavaScript
/// simulation that bUnit doesn't drive reliably — same documented limitation as
/// <c>TagSelectorTests</c>. The search itself (<c>ILike</c> substring, cap, content-rating filter)
/// is covered at the Integration tier (<c>StoryLineageServiceTests.SearchStoriesByTitle_*</c>); the
/// add-via-typeahead path is covered by manual/live-browser verification.
///
/// JSInterop is configured with <see cref="JSRuntimeMode.Loose"/> so any residual
/// JS focus calls don't cause the test to throw.
/// Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class StoryTitlePickerTests : BunitContext
{
    private readonly FakeStorySearchService _fakeStoryService = new();

    public StoryTitlePickerTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddScoped<IStoryReadService>(_ => _fakeStoryService);
    }

    [Fact]
    public void StoryTitlePicker_Renders_WithPlaceholder()
    {
        IRenderedComponent<StoryTitlePicker> cut = Render<StoryTitlePicker>(p => p
            .Add(c => c.Placeholder, "Type to search for a story...")
            .Add(c => c.OnStorySelected, (StoryTitleSearchDto _) => { }));

        cut.Find("input").GetAttribute("placeholder").Should().Be("Type to search for a story...");
    }

    [Fact]
    public void StoryTitlePicker_RendersWithoutThrowing_WhenExcludeStoryIdSet()
    {
        Action act = () => Render<StoryTitlePicker>(p => p
            .Add(c => c.ExcludeStoryId, 5)
            .Add(c => c.OnStorySelected, (StoryTitleSearchDto _) => { }));

        act.Should().NotThrow();
    }

    // A minimal fake IStoryReadService — the picker only calls SearchStoriesByTitleAsync.
    private sealed class FakeStorySearchService : IStoryReadService
    {
        public Task<StoryDetailsDTO?> GetStoryByIdAsync(int storyId) => Task.FromResult<StoryDetailsDTO?>(null);
        public Task<StoryUpdateDTO?> GetStoryForEditAsync(int storyId) => Task.FromResult<StoryUpdateDTO?>(null);
        public Task<StoryListingDto[]> GetListingsByIdsAsync(IReadOnlyList<int> storyIds) => Task.FromResult(Array.Empty<StoryListingDto>());
        public Task<(StoryListingDto[] Items, int TotalCount)> GetRecentListingsAsync(int page, int pageSize) => Task.FromResult((Array.Empty<StoryListingDto>(), 0));
        public Task<(StoryListingDto[] Items, int TotalCount)> GetListingsAsync(StoryFilterDto filter, IReadOnlyCollection<int>? restrictToStoryIds = null) => Task.FromResult((Array.Empty<StoryListingDto>(), 0));
        public Task<StoryListingDto[]> GetRandomBatchAsync(StoryFilterDto filter, int batchSize) => Task.FromResult(Array.Empty<StoryListingDto>());
        public Task<IReadOnlyList<int>> FilterCandidateIdsAsync(IReadOnlyCollection<int> candidateIds, StoryFilterDto filter) => Task.FromResult<IReadOnlyList<int>>([.. candidateIds]);
        public Task<IReadOnlyList<int>> GetStoryIdsByAuthorAsync(int authorId) => Task.FromResult<IReadOnlyList<int>>([]);
        public Task<IReadOnlyList<ExternalPlatformDto>> GetExternalPlatformsAsync() => Task.FromResult<IReadOnlyList<ExternalPlatformDto>>([]);
        public Task<long> GetStoryTotalViewsAsync(int storyId) => Task.FromResult(0L);
        public Task<IReadOnlyList<StoryTitleSearchDto>> SearchStoriesByTitleAsync(string term) => Task.FromResult<IReadOnlyList<StoryTitleSearchDto>>([]);
    }
}
