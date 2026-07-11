using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="ISeriesWriteService"/> / <see cref="ISeriesReadService"/>
/// (Feature 9, WU41). Covers: series CRUD + owner-gating, duplicate-name rejection (per-author,
/// not global), membership add/remove/reorder (own-stories-only rule), cascade delete, and the
/// viewer-visible-counting rule for <see cref="StorySeriesMembershipDto"/> (Position/Count/Prev/Next
/// must reflect only members surviving the viewer's ContentRating filter — the settled decision
/// recorded in <c>audit/Stories.md</c> Feature 9).
///
/// <b>Per-test seeding:</b> every test seeds users and stories via <c>SeedUserAsync</c> /
/// <c>SeedStoryAsync</c>; Respawn resets the DB between every test — see testing.md.
///
/// Tier: Integration (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class SeriesServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _authorId;
    private int _otherUserId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorId    = await SeedUserAsync("author");
        _otherUserId = await SeedUserAsync("other");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Series CRUD
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSeries_Owner_InsertsSeriesRow()
    {
        SetActiveUser(_authorId);

        int seriesId = await CreateSeriesAsync(new CreateSeriesDto { Name = "The Kanto Chronicles" });

        SeriesDetailDto? detail = await GetSeriesAsync(seriesId);
        detail.Should().NotBeNull();
        detail!.Name.Should().Be("The Kanto Chronicles");
        detail.AuthorId.Should().Be(_authorId);
        detail.OrderedStoryIds.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateSeries_Anonymous_ThrowsInvalidOperation()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());
        Func<Task> act = () => CreateSeriesAsync(new CreateSeriesDto { Name = "Anon Series" });
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateSeries_EmptyName_ThrowsSeriesValidationException()
    {
        SetActiveUser(_authorId);
        Func<Task> act = () => CreateSeriesAsync(new CreateSeriesDto { Name = "" });
        await act.Should().ThrowAsync<SeriesValidationException>();
    }

    [Fact]
    public async Task CreateSeries_DuplicateNameForSameAuthor_ThrowsSeriesValidationException()
    {
        SetActiveUser(_authorId);
        await CreateSeriesAsync(new CreateSeriesDto { Name = "Kanto Chronicles" });

        Func<Task> act = () => CreateSeriesAsync(new CreateSeriesDto { Name = "kanto chronicles" }); // case-insensitive
        await act.Should().ThrowAsync<SeriesValidationException>();
    }

    [Fact]
    public async Task CreateSeries_SameNameDifferentAuthor_Succeeds()
    {
        SetActiveUser(_authorId);
        await CreateSeriesAsync(new CreateSeriesDto { Name = "Shared Name" });

        SetActiveUser(_otherUserId);
        // Uniqueness is per-author, not global.
        Func<Task> act = () => CreateSeriesAsync(new CreateSeriesDto { Name = "Shared Name" });
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpdateSeries_Owner_ChangesNameAndDescription()
    {
        SetActiveUser(_authorId);
        int seriesId = await CreateSeriesAsync(new CreateSeriesDto { Name = "Original Name" });

        await UpdateSeriesAsync(new UpdateSeriesDto
        {
            SeriesId    = seriesId,
            Name        = "Updated Name",
            Description = "<p>New description.</p>"
        });

        SeriesDetailDto? detail = await GetSeriesAsync(seriesId);
        detail!.Name.Should().Be("Updated Name");
        detail.Description.Should().Contain("New description.");
    }

    [Fact]
    public async Task UpdateSeries_NonOwner_ThrowsUnauthorizedAccess()
    {
        SetActiveUser(_authorId);
        int seriesId = await CreateSeriesAsync(new CreateSeriesDto { Name = "Locked Series" });

        SetActiveUser(_otherUserId);
        Func<Task> act = () => UpdateSeriesAsync(new UpdateSeriesDto { SeriesId = seriesId, Name = "Hacked Name" });
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task DeleteSeries_Owner_RemovesSeriesAndCascadesEntries()
    {
        SetActiveUser(_authorId);
        int seriesId = await CreateSeriesAsync(new CreateSeriesDto { Name = "Temp Series" });
        int storyId = await SeedStoryAsync(authorId: _authorId);
        await AddStoryAsync(seriesId, storyId);

        await DeleteSeriesAsync(seriesId);

        (await GetSeriesAsync(seriesId)).Should().BeNull();

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.SeriesEntries.AnyAsync(se => se.SeriesId == seriesId)).Should().BeFalse(
            "SeriesEntry rows must cascade when the Series is deleted");
        (await db.Stories.AnyAsync(s => s.StoryId == storyId)).Should().BeTrue(
            "deleting a series must not delete its member stories");
    }

    [Fact]
    public async Task DeleteSeries_NonOwner_ThrowsUnauthorizedAccess()
    {
        SetActiveUser(_authorId);
        int seriesId = await CreateSeriesAsync(new CreateSeriesDto { Name = "Series" });

        SetActiveUser(_otherUserId);
        Func<Task> act = () => DeleteSeriesAsync(seriesId);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Membership — add / remove / reorder
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddStory_OwnStory_AppendsAtNextOrderIndex()
    {
        SetActiveUser(_authorId);
        int seriesId = await CreateSeriesAsync(new CreateSeriesDto { Name = "Series" });
        int story1 = await SeedStoryAsync(authorId: _authorId);
        int story2 = await SeedStoryAsync(authorId: _authorId);

        await AddStoryAsync(seriesId, story1);
        await AddStoryAsync(seriesId, story2);

        SeriesDetailDto? detail = await GetSeriesAsync(seriesId);
        detail!.OrderedStoryIds.Should().Equal(story1, story2);
    }

    [Fact]
    public async Task AddStory_NonOwnerOfSeries_ThrowsUnauthorizedAccess()
    {
        SetActiveUser(_authorId);
        int seriesId = await CreateSeriesAsync(new CreateSeriesDto { Name = "Series" });
        int storyId = await SeedStoryAsync(authorId: _otherUserId);

        SetActiveUser(_otherUserId);
        Func<Task> act = () => AddStoryAsync(seriesId, storyId);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task AddStory_StoryOwnedByDifferentAuthor_ThrowsUnauthorizedAccess()
    {
        // A series holds only the owner's own stories (WU41 settled decision) — even the series
        // owner cannot add someone else's story.
        SetActiveUser(_authorId);
        int seriesId = await CreateSeriesAsync(new CreateSeriesDto { Name = "Series" });
        int otherStoryId = await SeedStoryAsync(authorId: _otherUserId);

        Func<Task> act = () => AddStoryAsync(seriesId, otherStoryId);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task AddStory_Idempotent_DoesNotDuplicate()
    {
        SetActiveUser(_authorId);
        int seriesId = await CreateSeriesAsync(new CreateSeriesDto { Name = "Series" });
        int storyId = await SeedStoryAsync(authorId: _authorId);

        await AddStoryAsync(seriesId, storyId);
        await AddStoryAsync(seriesId, storyId); // second call — idempotent

        SeriesDetailDto? detail = await GetSeriesAsync(seriesId);
        detail!.OrderedStoryIds.Should().ContainSingle();
    }

    [Fact]
    public async Task RemoveStory_Owner_RemovesEntry()
    {
        SetActiveUser(_authorId);
        int seriesId = await CreateSeriesAsync(new CreateSeriesDto { Name = "Series" });
        int storyId = await SeedStoryAsync(authorId: _authorId);
        await AddStoryAsync(seriesId, storyId);

        await RemoveStoryAsync(seriesId, storyId);

        SeriesDetailDto? detail = await GetSeriesAsync(seriesId);
        detail!.OrderedStoryIds.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveStory_Idempotent_NonMember_NoThrow()
    {
        SetActiveUser(_authorId);
        int seriesId = await CreateSeriesAsync(new CreateSeriesDto { Name = "Series" });
        int storyId = await SeedStoryAsync(authorId: _authorId);

        // storyId was never added — Remove should be a no-op.
        Func<Task> act = () => RemoveStoryAsync(seriesId, storyId);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Reorder_Owner_RewritesOrderIndex()
    {
        SetActiveUser(_authorId);
        int seriesId = await CreateSeriesAsync(new CreateSeriesDto { Name = "Series" });
        int story1 = await SeedStoryAsync(authorId: _authorId);
        int story2 = await SeedStoryAsync(authorId: _authorId);
        int story3 = await SeedStoryAsync(authorId: _authorId);
        await AddStoryAsync(seriesId, story1);
        await AddStoryAsync(seriesId, story2);
        await AddStoryAsync(seriesId, story3);

        await ReorderAsync(seriesId, [story3, story1, story2]);

        SeriesDetailDto? detail = await GetSeriesAsync(seriesId);
        detail!.OrderedStoryIds.Should().Equal(story3, story1, story2);
    }

    [Fact]
    public async Task Reorder_MismatchedIdSet_ThrowsSeriesValidationException()
    {
        SetActiveUser(_authorId);
        int seriesId = await CreateSeriesAsync(new CreateSeriesDto { Name = "Series" });
        int story1 = await SeedStoryAsync(authorId: _authorId);
        int story2 = await SeedStoryAsync(authorId: _authorId);
        await AddStoryAsync(seriesId, story1);
        await AddStoryAsync(seriesId, story2);

        // Missing story2, and includes an id that was never a member.
        int intruder = await SeedStoryAsync(authorId: _authorId);
        Func<Task> act = () => ReorderAsync(seriesId, [story1, intruder]);
        await act.Should().ThrowAsync<SeriesValidationException>();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Reads — GetSeriesByAuthorAsync
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSeriesByAuthor_ReturnsOnlyThatAuthorsSeries()
    {
        SetActiveUser(_authorId);
        int seriesId = await CreateSeriesAsync(new CreateSeriesDto { Name = "Mine" });

        SetActiveUser(_otherUserId);
        await CreateSeriesAsync(new CreateSeriesDto { Name = "Theirs" });

        IReadOnlyList<SeriesListingDto> mine = await GetSeriesByAuthorAsync(_authorId);
        mine.Should().ContainSingle(s => s.SeriesId == seriesId && s.Name == "Mine");
    }

    [Fact]
    public async Task GetSeriesByAuthor_StoryCount_ReflectsMemberCount()
    {
        SetActiveUser(_authorId);
        int seriesId = await CreateSeriesAsync(new CreateSeriesDto { Name = "Series" });
        int storyId = await SeedStoryAsync(authorId: _authorId);
        await AddStoryAsync(seriesId, storyId);

        IReadOnlyList<SeriesListingDto> mine = await GetSeriesByAuthorAsync(_authorId);
        mine.Should().ContainSingle(s => s.SeriesId == seriesId).Which.StoryCount.Should().Be(1);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Reads — GetMembershipsForStoryAsync (viewer-visible Position/Count/Prev/Next)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMemberships_StoryNotInAnySeries_ReturnsEmpty()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);

        IReadOnlyList<StorySeriesMembershipDto> memberships = await GetMembershipsAsync(storyId);
        memberships.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMemberships_SingleSeries_ReturnsPositionCountPrevNext()
    {
        SetActiveUser(_authorId);
        int seriesId = await CreateSeriesAsync(new CreateSeriesDto { Name = "Trilogy" });
        int story1 = await SeedStoryAsync(authorId: _authorId);
        int story2 = await SeedStoryAsync(authorId: _authorId);
        int story3 = await SeedStoryAsync(authorId: _authorId);
        await AddStoryAsync(seriesId, story1);
        await AddStoryAsync(seriesId, story2);
        await AddStoryAsync(seriesId, story3);

        IReadOnlyList<StorySeriesMembershipDto> memberships = await GetMembershipsAsync(story2);

        memberships.Should().ContainSingle();
        StorySeriesMembershipDto m = memberships[0];
        m.SeriesId.Should().Be(seriesId);
        m.SeriesName.Should().Be("Trilogy");
        m.Position.Should().Be(2);
        m.Count.Should().Be(3);
        m.PrevStoryId.Should().Be(story1);
        m.NextStoryId.Should().Be(story3);
    }

    [Fact]
    public async Task GetMemberships_FirstStory_HasNoPrev()
    {
        SetActiveUser(_authorId);
        int seriesId = await CreateSeriesAsync(new CreateSeriesDto { Name = "Series" });
        int story1 = await SeedStoryAsync(authorId: _authorId);
        int story2 = await SeedStoryAsync(authorId: _authorId);
        await AddStoryAsync(seriesId, story1);
        await AddStoryAsync(seriesId, story2);

        StorySeriesMembershipDto m = (await GetMembershipsAsync(story1)).Single();
        m.Position.Should().Be(1);
        m.PrevStoryId.Should().BeNull();
        m.NextStoryId.Should().Be(story2);
    }

    [Fact]
    public async Task GetMemberships_LastStory_HasNoNext()
    {
        SetActiveUser(_authorId);
        int seriesId = await CreateSeriesAsync(new CreateSeriesDto { Name = "Series" });
        int story1 = await SeedStoryAsync(authorId: _authorId);
        int story2 = await SeedStoryAsync(authorId: _authorId);
        await AddStoryAsync(seriesId, story1);
        await AddStoryAsync(seriesId, story2);

        StorySeriesMembershipDto m = (await GetMembershipsAsync(story2)).Single();
        m.Position.Should().Be(2);
        m.NextStoryId.Should().BeNull();
        m.PrevStoryId.Should().Be(story1);
    }

    [Fact]
    public async Task GetMemberships_StoryInMultipleSeries_ReturnsOneEntryPerSeries()
    {
        // WU41 settled decision: a story may belong to more than one series.
        SetActiveUser(_authorId);
        int storyId = await SeedStoryAsync(authorId: _authorId);

        int seriesA = await CreateSeriesAsync(new CreateSeriesDto { Name = "Series A" });
        int seriesB = await CreateSeriesAsync(new CreateSeriesDto { Name = "Series B" });
        await AddStoryAsync(seriesA, storyId);
        await AddStoryAsync(seriesB, storyId);

        IReadOnlyList<StorySeriesMembershipDto> memberships = await GetMembershipsAsync(storyId);

        memberships.Should().HaveCount(2);
        memberships.Select(m => m.SeriesId).Should().BeEquivalentTo([seriesA, seriesB]);
    }

    [Fact]
    public async Task GetMemberships_MatureMemberHiddenFromMatureDisabledViewer_ExcludedFromPositionCountAndNext()
    {
        // The settled counting rule (audit/Stories.md Feature 9): Position/Count/Prev/Next reflect
        // only members the *viewer* can see, not the raw SeriesEntry set. Series order: E, M, T.
        SetActiveUser(_authorId);
        int seriesId = await CreateSeriesAsync(new CreateSeriesDto { Name = "Mixed Rating Series" });
        int eStory = await SeedStoryAsync(authorId: _authorId, rating: Rating.E);
        int mStory = await SeedStoryAsync(authorId: _authorId, rating: Rating.M);
        int tStory = await SeedStoryAsync(authorId: _authorId, rating: Rating.T);
        await AddStoryAsync(seriesId, eStory);
        await AddStoryAsync(seriesId, mStory);
        await AddStoryAsync(seriesId, tStory);

        // A different, mature-disabled viewer looks at the E story.
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_otherUserId, showMatureContent: false));
        StorySeriesMembershipDto m = (await GetMembershipsAsync(eStory)).Single();

        m.Position.Should().Be(1, "the M-rated middle entry is invisible to this viewer");
        m.Count.Should().Be(2, "only E and T are visible — M is filtered out");
        m.PrevStoryId.Should().BeNull();
        m.NextStoryId.Should().Be(tStory, "Next must skip the hidden M-rated entry, never link to it");
    }

    [Fact]
    public async Task GetMemberships_MatureMemberVisible_WhenViewerShowsMatureContent()
    {
        SetActiveUser(_authorId);
        int seriesId = await CreateSeriesAsync(new CreateSeriesDto { Name = "Mixed Rating Series" });
        int eStory = await SeedStoryAsync(authorId: _authorId, rating: Rating.E);
        int mStory = await SeedStoryAsync(authorId: _authorId, rating: Rating.M);
        await AddStoryAsync(seriesId, eStory);
        await AddStoryAsync(seriesId, mStory);

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_otherUserId, showMatureContent: true));
        StorySeriesMembershipDto m = (await GetMembershipsAsync(eStory)).Single();

        m.Count.Should().Be(2);
        m.NextStoryId.Should().Be(mStory);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Private helpers — service calls
    // ─────────────────────────────────────────────────────────────────────────────

    private async Task<int> CreateSeriesAsync(CreateSeriesDto dto)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ISeriesWriteService svc = scope.ServiceProvider.GetRequiredService<ISeriesWriteService>();
        return await svc.CreateSeriesAsync(dto);
    }

    private async Task UpdateSeriesAsync(UpdateSeriesDto dto)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ISeriesWriteService svc = scope.ServiceProvider.GetRequiredService<ISeriesWriteService>();
        await svc.UpdateSeriesAsync(dto);
    }

    private async Task DeleteSeriesAsync(int seriesId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ISeriesWriteService svc = scope.ServiceProvider.GetRequiredService<ISeriesWriteService>();
        await svc.DeleteSeriesAsync(seriesId);
    }

    private async Task AddStoryAsync(int seriesId, int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ISeriesWriteService svc = scope.ServiceProvider.GetRequiredService<ISeriesWriteService>();
        await svc.AddStoryAsync(seriesId, storyId);
    }

    private async Task RemoveStoryAsync(int seriesId, int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ISeriesWriteService svc = scope.ServiceProvider.GetRequiredService<ISeriesWriteService>();
        await svc.RemoveStoryAsync(seriesId, storyId);
    }

    private async Task ReorderAsync(int seriesId, IReadOnlyList<int> orderedStoryIds)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ISeriesWriteService svc = scope.ServiceProvider.GetRequiredService<ISeriesWriteService>();
        await svc.ReorderAsync(seriesId, orderedStoryIds);
    }

    private async Task<SeriesDetailDto?> GetSeriesAsync(int seriesId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ISeriesReadService svc = scope.ServiceProvider.GetRequiredService<ISeriesReadService>();
        return await svc.GetSeriesByIdAsync(seriesId);
    }

    private async Task<IReadOnlyList<SeriesListingDto>> GetSeriesByAuthorAsync(int authorId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ISeriesReadService svc = scope.ServiceProvider.GetRequiredService<ISeriesReadService>();
        return await svc.GetSeriesByAuthorAsync(authorId);
    }

    private async Task<IReadOnlyList<StorySeriesMembershipDto>> GetMembershipsAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ISeriesReadService svc = scope.ServiceProvider.GetRequiredService<ISeriesReadService>();
        return await svc.GetMembershipsForStoryAsync(storyId);
    }
}
