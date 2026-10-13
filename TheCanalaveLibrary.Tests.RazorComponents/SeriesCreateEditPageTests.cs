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

    /// <summary>
    /// WU-ThrottleCoverage browser pass (2026-10-13): the dispatcher kept a create flag and an edit id
    /// that nothing cleared, so a route this instance had already visited was skipped on return. In
    /// the browser, Write → New Series from an edit page showed "Create a New Series" with the edited
    /// series' name still in the field (create → edit → create on one instance).
    /// </summary>
    [Fact]
    public void CreateEditCreate_OnSameInstance_ShowsABlankCreateForm()
    {
        IRenderedComponent<SeriesCreateEditPage> cut = Render<SeriesCreateEditPage>(p => p
            .Add(c => c.SeriesId, (int?)null));
        cut.Render(p => p.Add(c => c.SeriesId, ExistingSeriesId));
        cut.Find("#series-name").GetAttribute("value").Should().Be(ExistingSeriesName);

        cut.Render(p => p.Add(c => c.SeriesId, (int?)null));

        cut.Find("h1").TextContent.Should().Contain("Create a New Series");
        cut.Find("#series-name").GetAttribute("value").Should().BeNullOrEmpty(
            "returning to /series/new must load the blank create state, not keep the edited series' name");
    }

    /// <summary>The same defect's other direction: edit A → new → edit A skipped the reload of A and
    /// left the blank create state under an "Edit Series" heading.</summary>
    [Fact]
    public void EditCreateEdit_SameSeries_OnSameInstance_ReloadsTheSeries()
    {
        IRenderedComponent<SeriesCreateEditPage> cut = Render<SeriesCreateEditPage>(p => p
            .Add(c => c.SeriesId, ExistingSeriesId));
        cut.Render(p => p.Add(c => c.SeriesId, (int?)null));
        cut.Find("#series-name").GetAttribute("value").Should().BeNullOrEmpty();

        cut.Render(p => p.Add(c => c.SeriesId, ExistingSeriesId));

        cut.Find("#series-name").GetAttribute("value").Should().Be(ExistingSeriesName,
            "returning to a series' edit route must reload that series");
    }

    [Fact]
    public void AllFields_HaveAccessibleNames()
    {
        // WU-A11y (Structure), 2026-07-31. Edit mode (not create) so the Series Name/Description
        // fields and the "Add a story" picker (when AddableStories resolves non-empty) are both
        // exercised. See AccessibleNameAssertions for what this catches beyond check-a11y.ps1's
        // static gates.
        IRenderedComponent<SeriesCreateEditPage> cut = Render<SeriesCreateEditPage>(p => p
            .Add(c => c.SeriesId, ExistingSeriesId));

        AccessibleNameAssertions.AllFieldsHaveAccessibleNames(cut);
    }

    /// <summary>
    /// WU-ThrottleCoverage (2026-10-13): series create spends a <c>ContentCreate</c> token (owner ruling
    /// D20), and the page's catch-all used to answer every non-validation failure with a fixed "An
    /// unexpected error occurred" — so a throttled author was never told to wait. The catch now follows
    /// the <c>ExceptionPresenter</c> catch-site pattern.
    /// </summary>
    [Fact]
    public void Create_Throttled_ShowsTheWaitMessage_NotTheGenericError()
    {
        _seriesService.CreateException =
            new WriteRateLimitExceededException(WriteActionKind.ContentCreate, TimeSpan.FromSeconds(90));
        IRenderedComponent<SeriesCreateEditPage> cut = Render<SeriesCreateEditPage>(p => p
            .Add(c => c.SeriesId, (int?)null));

        cut.Find("#series-name").Change("Fresh Series");
        cut.Find("button[type=button]").Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("please wait 90 seconds"));
        cut.Markup.Should().NotContain("An unexpected error occurred");
        _seriesService.CreateCalls.Should().Be(1);
    }

    [Fact]
    public void Create_AnUnexpectedFailure_ShowsTheGenericMessage()
    {
        _seriesService.CreateException = new NotSupportedException("developer text");
        IRenderedComponent<SeriesCreateEditPage> cut = Render<SeriesCreateEditPage>(p => p
            .Add(c => c.SeriesId, (int?)null));

        cut.Find("#series-name").Change("Fresh Series");
        cut.Find("button[type=button]").Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain(ExceptionPresenter.GenericMessage));
        cut.Markup.Should().NotContain("developer text");
    }
}

// ── Fakes ────────────────────────────────────────────────────────────────────────────────────

internal sealed class FakeSeriesWriteService : ISeriesWriteService
{
    public Dictionary<int, SeriesDetailDto> Series { get; } = [];

    /// <summary>When set, <see cref="CreateSeriesAsync"/> throws it (WU-ThrottleCoverage).</summary>
    public Exception? CreateException { get; set; }

    public int CreateCalls { get; private set; }

    public Task<SeriesDetailDto?> GetSeriesByIdAsync(int seriesId) =>
        Task.FromResult(Series.GetValueOrDefault(seriesId));

    public Task<IReadOnlyList<SeriesListingDto>> GetSeriesByAuthorAsync(int authorId) =>
        Task.FromResult<IReadOnlyList<SeriesListingDto>>([]);

    public Task<IReadOnlyList<StorySeriesMembershipDto>> GetMembershipsForStoryAsync(int storyId) =>
        Task.FromResult<IReadOnlyList<StorySeriesMembershipDto>>([]);

    public Task<int> CreateSeriesAsync(CreateSeriesDto dto)
    {
        CreateCalls++;
        return CreateException is null ? Task.FromResult(1) : Task.FromException<int>(CreateException);
    }
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
