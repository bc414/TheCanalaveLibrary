using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Regression test for a runtime bug found via browser verification during WU41 (Feature 9):
/// <see cref="SeriesCreateEditPage"/> maps two <c>@page</c> routes ("/series/new" and
/// "/series/{SeriesId:int}/edit") onto the same component type. After creating a series,
/// <c>HandleSubmitAsync</c> navigates from "/series/new" into "/series/{newId}/edit" — Blazor's
/// router reuses the same component instance for that transition, so <c>OnInitializedAsync</c>
/// (which loaded the create-mode blank state) never re-fires; only <c>OnParametersSetAsync</c>
/// sees the new <c>SeriesId</c>. Pre-fix, the edit page rendered as if still in create mode (no
/// name, no member list, no add-story picker). Same dispatcher-reload class of bug as
/// <c>ProfilePageTests.TabSwitch_OnSameInstance_ReloadsTabPayload</c> (WU-ComponentSoundness F1);
/// this test mirrors that one's technique — <c>cut.Render(...)</c> re-sets parameters on the same
/// rendered instance, exactly what the router does on a same-instance navigation.
///
/// Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class SeriesCreateEditPageTests : BunitContext
{
    private const int TestUserId = 4;
    private const int ExistingSeriesId = 5;
    private const string ExistingSeriesName = "Existing Series";

    private readonly FakeSeriesWriteService _seriesService = new();

    public SeriesCreateEditPageTests()
    {
        _seriesService.Series[ExistingSeriesId] = new SeriesDetailDto(
            ExistingSeriesId, ExistingSeriesName, "A description.", TestUserId, "AuthorAlpha",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), []);

        Services.AddScoped<ISeriesWriteService>(_ => _seriesService);
        Services.AddScoped<IStoryReadService>(_ => new FakeStoryReadService());
        Services.AddScoped<IToastService>(_ => new FakeToastService());

        // AuthorizeView needs bUnit's authorization test double, not a bare cascaded
        // AuthenticationState (see AccountStatusBannerTests for the same pattern). The component
        // reads ClaimTypes.NameIdentifier itself to resolve _currentUserId — set it explicitly.
        this.AddAuthorization().SetAuthorized("AuthorAlpha").SetClaims(
            new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()));
    }

    [Fact]
    public void PostCreateRedirect_OnSameInstance_ReloadsEditModeData()
    {
        // Start in create mode ("/series/new" — SeriesId is null).
        IRenderedComponent<SeriesCreateEditPage> cut = Render<SeriesCreateEditPage>(p => p
            .Add(c => c.SeriesId, (int?)null));

        cut.Markup.Should().NotContain(ExistingSeriesName,
            "create mode starts blank, no series name yet");

        // Simulate the router reusing this instance for the post-create redirect into edit mode
        // (HandleSubmitAsync's Nav.NavigateTo($"/series/{newId}/edit") — same component, new SeriesId
        // parameter — triggers SetParametersAsync -> OnParametersSetAsync, not OnInitializedAsync).
        cut.Render(p => p.Add(c => c.SeriesId, ExistingSeriesId));

        // Post-fix: OnParametersSetAsync detects the SeriesId change and loads the series data —
        // the name input now shows the loaded series' name.
        cut.Markup.Should().Contain(ExistingSeriesName,
            "OnParametersSetAsync must reload the series data for the new SeriesId");
    }
}

// ── Fakes ────────────────────────────────────────────────────────────────────────────────────

internal sealed class FakeSeriesWriteService : ISeriesWriteService
{
    public Dictionary<int, SeriesDetailDto> Series { get; } = [];

    public Task<SeriesDetailDto?> GetSeriesByIdAsync(int seriesId) =>
        Task.FromResult(Series.GetValueOrDefault(seriesId));

    public Task<IReadOnlyList<SeriesListingDto>> GetSeriesByAuthorAsync(int authorId) =>
        Task.FromResult<IReadOnlyList<SeriesListingDto>>([]);

    public Task<IReadOnlyList<StorySeriesMembershipDto>> GetMembershipsForStoryAsync(int storyId) =>
        Task.FromResult<IReadOnlyList<StorySeriesMembershipDto>>([]);

    public Task<int> CreateSeriesAsync(CreateSeriesDto dto) => Task.FromResult(1);
    public Task UpdateSeriesAsync(UpdateSeriesDto dto) => Task.CompletedTask;
    public Task DeleteSeriesAsync(int seriesId) => Task.CompletedTask;
    public Task AddStoryAsync(int seriesId, int storyId) => Task.CompletedTask;
    public Task RemoveStoryAsync(int seriesId, int storyId) => Task.CompletedTask;
    public Task ReorderAsync(int seriesId, IReadOnlyList<int> orderedStoryIds) => Task.CompletedTask;
}

// FakeStoryReadService is defined in FakeProfileTestServices.cs — reused here (same assembly/namespace).

internal sealed class FakeToastService : IToastService
{
    public event Action<ToastMessage>? OnShow;
    public void Show(string text, ToastLevel level = ToastLevel.Info, TimeSpan? duration = null) =>
        OnShow?.Invoke(new ToastMessage(Guid.NewGuid(), level, text, duration ?? TimeSpan.FromSeconds(3)));
}
