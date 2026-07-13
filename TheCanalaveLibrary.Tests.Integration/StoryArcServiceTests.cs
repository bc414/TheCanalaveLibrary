using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="IStoryArcWriteService"/> / <see cref="IStoryArcReadService"/>
/// (Feature 8, WU45). Covers: arc CRUD + author-gating, the WU45 range rules (Start ≥ 1,
/// Start ≤ End, no overlap incl. touching-boundary cases, duplicate-title-per-story), the
/// update-excludes-self overlap check, reading-order by StartChapterNumber (SortOrder was
/// eliminated in WU45), and delete leaving covered chapters as plain gap chapters.
///
/// <b>Per-test seeding:</b> every test seeds users/stories via <c>SeedUserAsync</c> /
/// <c>SeedStoryAsync</c>; arcs reference only the story FK — no chapter rows are required for
/// range validation (ranges may extend past existing chapters by design; see
/// ServerStoryArcWriteService header). Respawn resets between tests (testing.md).
///
/// Tier: Integration (Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class StoryArcServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _authorId;
    private int _otherUserId;
    private int _storyId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorId    = await SeedUserAsync("author");
        _otherUserId = await SeedUserAsync("other");
        _storyId     = await SeedStoryAsync(_authorId);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Create
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateArc_Author_InsertsAndReadsBack()
    {
        SetActiveUser(_authorId);

        int arcId = await CreateArcAsync(new CreateStoryArcDto(_storyId, "Book 1: Indigo", 1, 25));

        IReadOnlyList<StoryArcDto> arcs = await GetArcsAsync(_storyId);
        arcs.Should().ContainSingle();
        arcs[0].StoryArcId.Should().Be(arcId);
        arcs[0].Title.Should().Be("Book 1: Indigo");
        arcs[0].StartChapterNumber.Should().Be(1);
        arcs[0].EndChapterNumber.Should().Be(25);
    }

    [Fact]
    public async Task CreateArc_NonAuthor_ThrowsUnauthorized()
    {
        SetActiveUser(_otherUserId);
        Func<Task> act = () => CreateArcAsync(new CreateStoryArcDto(_storyId, "Book 1", 1, 5));
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task CreateArc_Anonymous_ThrowsInvalidOperation()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());
        Func<Task> act = () => CreateArcAsync(new CreateStoryArcDto(_storyId, "Book 1", 1, 5));
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateArc_MissingStory_ThrowsKeyNotFound()
    {
        SetActiveUser(_authorId);
        Func<Task> act = () => CreateArcAsync(new CreateStoryArcDto(999_999, "Book 1", 1, 5));
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Theory]
    [InlineData("", 1, 5)]      // empty title
    [InlineData("Book 1", 0, 5)] // start below 1
    [InlineData("Book 1", 6, 5)] // end before start
    public async Task CreateArc_InvalidInput_ThrowsValidation(string title, int start, int end)
    {
        SetActiveUser(_authorId);
        Func<Task> act = () => CreateArcAsync(new CreateStoryArcDto(_storyId, title, start, end));
        await act.Should().ThrowAsync<StoryArcValidationException>();
    }

    [Fact]
    public async Task CreateArc_SingleChapterRange_IsValid()
    {
        SetActiveUser(_authorId);
        // Start == End is a legal one-chapter arc.
        await CreateArcAsync(new CreateStoryArcDto(_storyId, "Interlude", 7, 7));
        (await GetArcsAsync(_storyId)).Should().ContainSingle();
    }

    [Fact]
    public async Task CreateArc_DuplicateTitleSameStory_ThrowsValidation()
    {
        SetActiveUser(_authorId);
        await CreateArcAsync(new CreateStoryArcDto(_storyId, "Book 1", 1, 5));

        Func<Task> act = () => CreateArcAsync(new CreateStoryArcDto(_storyId, "book 1", 6, 10)); // case-insensitive
        await act.Should().ThrowAsync<StoryArcValidationException>();
    }

    [Fact]
    public async Task CreateArc_SameTitleDifferentStory_Succeeds()
    {
        SetActiveUser(_authorId);
        int otherStoryId = await SeedStoryAsync(_authorId);
        await CreateArcAsync(new CreateStoryArcDto(_storyId, "Book 1", 1, 5));

        await CreateArcAsync(new CreateStoryArcDto(otherStoryId, "Book 1", 1, 5));
        (await GetArcsAsync(otherStoryId)).Should().ContainSingle();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Overlap rules (no-overlap, gaps-allowed — WU45)
    // ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(5, 15)]  // straddles existing end
    [InlineData(1, 30)]  // fully contains existing
    [InlineData(12, 18)] // fully inside existing
    [InlineData(10, 10)] // touches existing end exactly
    [InlineData(3, 10)]  // touches existing start range edge
    public async Task CreateArc_OverlappingRange_ThrowsValidation(int start, int end)
    {
        SetActiveUser(_authorId);
        await CreateArcAsync(new CreateStoryArcDto(_storyId, "Book 1", 10, 20));

        Func<Task> act = () => CreateArcAsync(new CreateStoryArcDto(_storyId, "Book 2", start, end));
        await act.Should().ThrowAsync<StoryArcValidationException>();
    }

    [Fact]
    public async Task CreateArc_AdjacentAndGappedRanges_Succeed()
    {
        SetActiveUser(_authorId);
        await CreateArcAsync(new CreateStoryArcDto(_storyId, "Book 1", 10, 20));

        // Immediately adjacent (21 starts right after 20) — legal.
        await CreateArcAsync(new CreateStoryArcDto(_storyId, "Book 2", 21, 30));
        // Gapped (chapters 31–39 belong to no arc) — legal by design.
        await CreateArcAsync(new CreateStoryArcDto(_storyId, "Book 3", 40, 50));
        // Before the first arc — legal.
        await CreateArcAsync(new CreateStoryArcDto(_storyId, "Prologue Arc", 1, 5));

        (await GetArcsAsync(_storyId)).Should().HaveCount(4);
    }

    [Fact]
    public async Task GetArcs_OrdersByStartChapterNumber()
    {
        SetActiveUser(_authorId);
        // Created deliberately out of reading order — order must derive from StartChapterNumber.
        await CreateArcAsync(new CreateStoryArcDto(_storyId, "Book 3", 40, 50));
        await CreateArcAsync(new CreateStoryArcDto(_storyId, "Book 1", 1, 10));
        await CreateArcAsync(new CreateStoryArcDto(_storyId, "Book 2", 11, 39));

        IReadOnlyList<StoryArcDto> arcs = await GetArcsAsync(_storyId);
        arcs.Select(a => a.Title).Should().ContainInOrder("Book 1", "Book 2", "Book 3");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Update
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateArc_OwnRangeChange_ExcludesSelfFromOverlapCheck()
    {
        SetActiveUser(_authorId);
        int arcId = await CreateArcAsync(new CreateStoryArcDto(_storyId, "Book 1", 1, 10));

        // Growing the same arc "overlaps" its own old range — must be allowed.
        await UpdateArcAsync(new UpdateStoryArcDto(arcId, "Book 1 (extended)", 1, 15));

        IReadOnlyList<StoryArcDto> arcs = await GetArcsAsync(_storyId);
        arcs.Should().ContainSingle();
        arcs[0].Title.Should().Be("Book 1 (extended)");
        arcs[0].EndChapterNumber.Should().Be(15);
    }

    [Fact]
    public async Task UpdateArc_OverlapsSibling_ThrowsValidation()
    {
        SetActiveUser(_authorId);
        int arcId = await CreateArcAsync(new CreateStoryArcDto(_storyId, "Book 1", 1, 10));
        await CreateArcAsync(new CreateStoryArcDto(_storyId, "Book 2", 11, 20));

        Func<Task> act = () => UpdateArcAsync(new UpdateStoryArcDto(arcId, "Book 1", 1, 11));
        await act.Should().ThrowAsync<StoryArcValidationException>();
    }

    [Fact]
    public async Task UpdateArc_NonAuthor_ThrowsUnauthorized()
    {
        SetActiveUser(_authorId);
        int arcId = await CreateArcAsync(new CreateStoryArcDto(_storyId, "Book 1", 1, 10));

        SetActiveUser(_otherUserId);
        Func<Task> act = () => UpdateArcAsync(new UpdateStoryArcDto(arcId, "Hijacked", 1, 10));
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Delete
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteArc_RemovesOnlyThatArc()
    {
        SetActiveUser(_authorId);
        int arc1 = await CreateArcAsync(new CreateStoryArcDto(_storyId, "Book 1", 1, 10));
        await CreateArcAsync(new CreateStoryArcDto(_storyId, "Book 2", 11, 20));

        await DeleteArcAsync(arc1);

        IReadOnlyList<StoryArcDto> arcs = await GetArcsAsync(_storyId);
        arcs.Should().ContainSingle();
        arcs[0].Title.Should().Be("Book 2");
    }

    [Fact]
    public async Task DeleteArc_NonAuthor_ThrowsUnauthorized()
    {
        SetActiveUser(_authorId);
        int arcId = await CreateArcAsync(new CreateStoryArcDto(_storyId, "Book 1", 1, 10));

        SetActiveUser(_otherUserId);
        Func<Task> act = () => DeleteArcAsync(arcId);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task DeleteArc_Missing_ThrowsKeyNotFound()
    {
        SetActiveUser(_authorId);
        Func<Task> act = () => DeleteArcAsync(999_999);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Private helpers — service calls
    // ─────────────────────────────────────────────────────────────────────────────

    private async Task<int> CreateArcAsync(CreateStoryArcDto dto)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryArcWriteService svc = scope.ServiceProvider.GetRequiredService<IStoryArcWriteService>();
        return await svc.CreateArcAsync(dto);
    }

    private async Task UpdateArcAsync(UpdateStoryArcDto dto)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryArcWriteService svc = scope.ServiceProvider.GetRequiredService<IStoryArcWriteService>();
        await svc.UpdateArcAsync(dto);
    }

    private async Task DeleteArcAsync(int storyArcId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryArcWriteService svc = scope.ServiceProvider.GetRequiredService<IStoryArcWriteService>();
        await svc.DeleteArcAsync(storyArcId);
    }

    private async Task<IReadOnlyList<StoryArcDto>> GetArcsAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryArcReadService svc = scope.ServiceProvider.GetRequiredService<IStoryArcReadService>();
        return await svc.GetArcsForStoryAsync(storyId);
    }
}
