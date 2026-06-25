using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="ResultsFilterPanel"/> (WU23). Covers:
/// - Show* params control which axes are visible.
/// - "Apply Filters" raises OnSearch with a correct <see cref="StoryFilterDto"/>.
/// - Relevance sort hidden from dropdown when TextQuery is empty; shown when text active.
/// - InitialFilter seeds the buffered state so Apply emits the seeded values.
/// - Interaction exclusion selections are forwarded in the DTO on Apply.
///
/// <b>Not tested here:</b> TagFilter include/exclude logic (covered by TagFilterTests);
/// UserStoryInteractionFilter toggle logic (covered by UserStoryInteractionFilterTests);
/// live visual rendering (Stage 6 sign-off required per WU8/WU13 precedent).
/// </summary>
public class ResultsFilterPanelTests : TestContext
{
    public ResultsFilterPanelTests()
    {
        // TagSelector injects ITagReadService; we provide a do-nothing fake so TagFilter can render.
        Services.AddScoped<ITagReadService>(_ => new FakeTagReadService());
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ── Show* visibility params ──────────────────────────────────────────────────────

    [Fact]
    public void ShowTextSearch_False_NoSearchInput()
    {
        IRenderedComponent<ResultsFilterPanel> cut = RenderComponent<ResultsFilterPanel>(p => p
            .Add(c => c.ShowTextSearch, false));

        cut.FindAll("input[type='search']").Should().BeEmpty("ShowTextSearch=false must hide the FTS input");
    }

    [Fact]
    public void ShowTextSearch_True_RendersSearchInput()
    {
        IRenderedComponent<ResultsFilterPanel> cut = RenderComponent<ResultsFilterPanel>(p => p
            .Add(c => c.ShowTextSearch, true));

        cut.FindAll("input[type='search']").Should().HaveCount(1);
    }

    [Fact]
    public void ShowTagFilter_False_NoTagSelectorPresent()
    {
        IRenderedComponent<ResultsFilterPanel> cut = RenderComponent<ResultsFilterPanel>(p => p
            .Add(c => c.ShowTagFilter, false));

        // TagSelector renders a typeahead <input> with placeholder; absence proves TagFilter is hidden.
        cut.FindAll("input[placeholder*='tag']").Should().BeEmpty(
            "ShowTagFilter=false must suppress the entire TagFilter axis");
    }

    [Fact]
    public void ShowInteractionFilters_False_NoCheckboxes()
    {
        IRenderedComponent<ResultsFilterPanel> cut = RenderComponent<ResultsFilterPanel>(p => p
            .Add(c => c.ShowInteractionFilters, false));

        cut.FindAll("input[type='checkbox']").Should().BeEmpty(
            "ShowInteractionFilters=false must suppress the UserStoryInteractionFilter axis");
    }

    [Fact]
    public void ShowInteractionFilters_True_RendersCheckboxes()
    {
        IRenderedComponent<ResultsFilterPanel> cut = RenderComponent<ResultsFilterPanel>(p => p
            .Add(c => c.ShowInteractionFilters, true));

        cut.FindAll("input[type='checkbox']").Count.Should().BeGreaterThan(0,
            "ShowInteractionFilters=true must render at least one checkbox");
    }

    // ── Apply emits correct DTO ──────────────────────────────────────────────────────

    [Fact]
    public async Task Apply_WithDefaultState_EmitsFilterDtoWithDefaultValues()
    {
        StoryFilterDto? emitted = null;
        IRenderedComponent<ResultsFilterPanel> cut = RenderComponent<ResultsFilterPanel>(p => p
            .Add(c => c.ShowTagFilter, false)
            .Add(c => c.ShowInteractionFilters, false)
            .Add(c => c.OnSearch, (StoryFilterDto dto) => emitted = dto));

        await cut.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        emitted.Should().NotBeNull();
        emitted!.TextQuery.Should().BeNull("default text query is empty → null in DTO");
        emitted.IncludedTagIds.Should().BeEmpty();
        emitted.ExcludedTagIds.Should().BeEmpty();
        emitted.ExcludedInteractions.Should().BeEmpty();
        emitted.Page.Should().Be(1, "Apply always resets to page 1");
    }

    [Fact]
    public async Task Apply_AfterTypingText_EmitsTrimmedTextQuery()
    {
        StoryFilterDto? emitted = null;
        IRenderedComponent<ResultsFilterPanel> cut = RenderComponent<ResultsFilterPanel>(p => p
            .Add(c => c.ShowTagFilter, false)
            .Add(c => c.ShowInteractionFilters, false)
            .Add(c => c.OnSearch, (StoryFilterDto dto) => emitted = dto));

        await cut.Find("input[type='search']").TriggerEventAsync("oninput",
            new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "  Arceus  " });

        await cut.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        emitted!.TextQuery.Should().Be("Arceus", "TextQuery must be trimmed");
    }

    // ── Relevance sort visibility ────────────────────────────────────────────────────

    [Fact]
    public void RelevanceSort_HiddenWhenNoTextQuery()
    {
        IRenderedComponent<ResultsFilterPanel> cut = RenderComponent<ResultsFilterPanel>(p => p
            .Add(c => c.ShowTagFilter, false)
            .Add(c => c.ShowInteractionFilters, false)
            .Add(c => c.AvailableSorts, [DefaultSortOrder.DatePublished, DefaultSortOrder.Relevance]));

        // No text entered — Relevance option must be absent from the dropdown.
        cut.FindAll("option").Select(o => o.GetAttribute("value")).Should()
            .NotContain(((short)DefaultSortOrder.Relevance).ToString(),
                "Relevance sort must be hidden when TextQuery is empty");
    }

    [Fact]
    public async Task RelevanceSort_AppearsAfterTypingText()
    {
        IRenderedComponent<ResultsFilterPanel> cut = RenderComponent<ResultsFilterPanel>(p => p
            .Add(c => c.ShowTagFilter, false)
            .Add(c => c.ShowInteractionFilters, false)
            .Add(c => c.AvailableSorts, [DefaultSortOrder.DatePublished, DefaultSortOrder.Relevance]));

        await cut.Find("input[type='search']").TriggerEventAsync("oninput",
            new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "test" });

        cut.FindAll("option").Select(o => o.GetAttribute("value")).Should()
            .Contain(((short)DefaultSortOrder.Relevance).ToString(),
                "Relevance sort must appear in the dropdown once text is entered");
    }

    // ── InitialFilter seeds the panel ────────────────────────────────────────────────

    [Fact]
    public async Task Apply_WithInitialFilter_EmitsInitialFilterValues()
    {
        StoryFilterDto? emitted = null;
        StoryFilterDto initial = new()
        {
            Sort = DefaultSortOrder.Random,
            ExcludedInteractions = [UserStoryInteractionTypeEnum.Ignore],
            Page = 3  // Apply must reset to 1
        };

        IRenderedComponent<ResultsFilterPanel> cut = RenderComponent<ResultsFilterPanel>(p => p
            .Add(c => c.ShowTagFilter, false)
            .Add(c => c.InitialFilter, initial)
            .Add(c => c.AvailableSorts, [DefaultSortOrder.DatePublished, DefaultSortOrder.Random])
            .Add(c => c.OnSearch, (StoryFilterDto dto) => emitted = dto));

        await cut.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        emitted!.Sort.Should().Be(DefaultSortOrder.Random, "InitialFilter.Sort must seed the sort dropdown");
        emitted.ExcludedInteractions.Should().Contain(UserStoryInteractionTypeEnum.Ignore,
            "InitialFilter.ExcludedInteractions must pre-check the interaction checkbox");
        emitted.Page.Should().Be(1, "Apply always resets to page 1 regardless of InitialFilter.Page");
    }
}

// ── Test double ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// No-op ITagReadService so TagFilter can render in bUnit without a real service.
/// Returns empty results for all queries.
/// </summary>
internal sealed class FakeTagReadService : ITagReadService
{
    public Task<List<TagDropDownDTO>> GetTagsByTypeAsync(TagTypeEnum type) => Empty();
    public Task<List<TagDropDownDTO>> GetAllCharacterTagsAsync() => Empty();
    public Task<List<TagDropDownDTO>> GetAllSettingTagsAsync() => Empty();
    public Task<List<TagDropDownDTO>> GetAllGenreTagsAsync() => Empty();
    public Task<List<TagDropDownDTO>> GetAllContentWarningTagsAsync() => Empty();
    public Task<List<TagChipDto>> SearchTagChipsAsync(TagTypeEnum type, string term) =>
        Task.FromResult(new List<TagChipDto>());
    public Task<List<TagChipDto>> GetTagChipsByIdsAsync(IReadOnlyList<int> tagIds) =>
        Task.FromResult(new List<TagChipDto>());
    public Task<List<TagDirectoryGroupDto>> GetTagDirectoryAsync() =>
        Task.FromResult(new List<TagDirectoryGroupDto>());

    private static Task<List<TagDropDownDTO>> Empty() => Task.FromResult(new List<TagDropDownDTO>());
}
