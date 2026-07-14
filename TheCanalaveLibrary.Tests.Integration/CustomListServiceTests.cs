using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="ICustomListWriteService"/> / <see cref="ICustomListReadService"/>
/// (Feature 51, WU-CustomLists). Covers: list CRUD + owner-gating, per-user case-insensitive
/// duplicate-name rejection, the 100-list cap, entry add/remove idempotence + FK behavior,
/// public/private visibility (detail, ids, public-by-user), sort orders, the content-rating filter
/// on entry reads and counts, clone (visible-entries-only, private-start, name disambiguation,
/// source-visibility gate, cap), and entry cascade on list delete.
///
/// <b>Per-test seeding:</b> every test seeds users via <c>SeedUserAsync</c> and stories via
/// <c>SeedStoryAsync</c> (FK parents for <c>custom_list_entries</c>: the list row is created by the
/// service under test; the story row by <c>SeedStoryAsync</c>). Respawn resets between tests.
///
/// Tier: Integration (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class CustomListServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _ownerId;
    private int _otherUserId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _ownerId     = await SeedUserAsync("owner");
        _otherUserId = await SeedUserAsync("other");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Create
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateListAsync_Owner_InsertsRowVisibleInMyLists()
    {
        SetActiveUser(_ownerId);

        int id = await CreateAsync("Comfort re-reads", isPublic: false);

        List<CustomListSummaryDto> mine = await GetMyListsAsync();
        mine.Should().ContainSingle(l =>
            l.CustomListId == id && l.ListName == "Comfort re-reads" && !l.IsPublic && l.StoryCount == 0);
    }

    [Fact]
    public async Task CreateListAsync_Anonymous_ThrowsInvalidOperation()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());
        Func<Task> act = () => CreateAsync("Anon", false);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateListAsync_DuplicateNameForSameUser_CaseInsensitive_ThrowsValidation()
    {
        SetActiveUser(_ownerId);
        await CreateAsync("Mine", false);

        Func<Task> act = () => CreateAsync("mine", false);
        await act.Should().ThrowAsync<CustomListValidationException>();
    }

    [Fact]
    public async Task CreateListAsync_SameNameDifferentUser_Succeeds()
    {
        SetActiveUser(_ownerId);
        await CreateAsync("Shared Name", false);

        SetActiveUser(_otherUserId);
        Func<Task> act = () => CreateAsync("Shared Name", false);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CreateListAsync_AtCap_ThrowsValidation()
    {
        SetActiveUser(_ownerId);
        // Reject-at-limit: reach the cap the natural way (Respawn guarantees a clean count).
        for (int i = 0; i < CustomListValidations.MaxListsPerUser; i++)
            await CreateAsync($"List {i:D3}", false);

        Func<Task> act = () => CreateAsync("One too many", false);
        await act.Should().ThrowAsync<CustomListValidationException>();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Rename / visibility / delete — owner gating
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RenameListAsync_Owner_Renames()
    {
        SetActiveUser(_ownerId);
        int id = await CreateAsync("Old Name", false);

        await RenameAsync(id, "New Name");

        (await GetDetailAsync(id))!.ListName.Should().Be("New Name");
    }

    [Fact]
    public async Task RenameListAsync_NonOwner_ThrowsUnauthorized()
    {
        SetActiveUser(_ownerId);
        int id = await CreateAsync("Owned", false);

        SetActiveUser(_otherUserId);
        Func<Task> act = () => RenameAsync(id, "Stolen");
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RenameListAsync_DuplicateOfAnotherOwnedList_ThrowsValidation()
    {
        SetActiveUser(_ownerId);
        await CreateAsync("First", false);
        int second = await CreateAsync("Second", false);

        Func<Task> act = () => RenameAsync(second, "FIRST"); // case-insensitive
        await act.Should().ThrowAsync<CustomListValidationException>();
    }

    [Fact]
    public async Task RenameListAsync_MissingList_ThrowsKeyNotFound()
    {
        SetActiveUser(_ownerId);
        Func<Task> act = () => RenameAsync(999_999, "Ghost");
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SetListVisibilityAsync_TogglesPublicFlag()
    {
        SetActiveUser(_ownerId);
        int id = await CreateAsync("Toggle", isPublic: false);

        await SetVisibilityAsync(id, true);
        (await GetDetailAsync(id))!.IsPublic.Should().BeTrue();

        await SetVisibilityAsync(id, false);
        (await GetDetailAsync(id))!.IsPublic.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteListAsync_Owner_RemovesListAndCascadesEntries()
    {
        SetActiveUser(_ownerId);
        int storyId = await SeedStoryAsync();
        int id = await CreateAsync("Doomed", false);
        await AddStoryAsync(id, storyId);

        await DeleteAsync(id);

        (await GetDetailAsync(id)).Should().BeNull();
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.CustomListEntries.CountAsync(e => e.ListId == id)).Should().Be(0); // cascade
    }

    [Fact]
    public async Task DeleteListAsync_NonOwner_ThrowsUnauthorized()
    {
        SetActiveUser(_ownerId);
        int id = await CreateAsync("Protected", false);

        SetActiveUser(_otherUserId);
        Func<Task> act = () => DeleteAsync(id);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Entries — add / remove
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddStoryAsync_AddsEntry_AndIsIdempotent()
    {
        SetActiveUser(_ownerId);
        int storyId = await SeedStoryAsync();
        int id = await CreateAsync("Shelf", false);

        await AddStoryAsync(id, storyId);
        await AddStoryAsync(id, storyId); // idempotent — no PK violation, still one entry

        (await GetStoryIdsAsync(id, CustomListSortEnum.DateAddedDesc)).Should().Equal(storyId);
        (await GetDetailAsync(id))!.StoryCount.Should().Be(1);
    }

    [Fact]
    public async Task AddStoryAsync_MissingStory_ThrowsKeyNotFound()
    {
        SetActiveUser(_ownerId);
        int id = await CreateAsync("Shelf", false);

        Func<Task> act = () => AddStoryAsync(id, 999_999);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task AddStoryAsync_NonOwnersList_ThrowsUnauthorized()
    {
        SetActiveUser(_ownerId);
        int id = await CreateAsync("Owned", false);
        int storyId = await SeedStoryAsync();

        SetActiveUser(_otherUserId);
        Func<Task> act = () => AddStoryAsync(id, storyId);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task AddStoryAsync_MRatedStory_MatureOffOwner_StillAdds()
    {
        // The write context is unfiltered: a user can file an M-rated story they opened (e.g.
        // before turning mature off) — no rating ceiling applies to personal filing. Their own
        // READS then hide it (see GetListStoryIdsAsync_RatingFilter test below).
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_ownerId, showMatureContent: false));
        int mStory = await SeedStoryAsync(rating: Rating.M);
        int id = await CreateAsync("Mixed", false);

        Func<Task> act = () => AddStoryAsync(id, mStory);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RemoveStoryAsync_RemovesEntry_AndIsIdempotent()
    {
        SetActiveUser(_ownerId);
        int storyId = await SeedStoryAsync();
        int id = await CreateAsync("Shelf", false);
        await AddStoryAsync(id, storyId);

        await RemoveStoryAsync(id, storyId);
        await RemoveStoryAsync(id, storyId); // idempotent no-op

        (await GetStoryIdsAsync(id, CustomListSortEnum.DateAddedDesc)).Should().BeEmpty();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Visibility — detail, ids, public-by-user, memberships
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetListDetailAsync_PrivateList_InvisibleToNonOwnerAndAnonymous()
    {
        SetActiveUser(_ownerId);
        int id = await CreateAsync("Secret", isPublic: false);

        (await GetDetailAsync(id)).Should().NotBeNull(); // owner sees it

        SetActiveUser(_otherUserId);
        (await GetDetailAsync(id)).Should().BeNull();

        SetActiveUser(FakeActiveUserContext.Anonymous());
        (await GetDetailAsync(id)).Should().BeNull();
        (await GetStoryIdsAsync(id, CustomListSortEnum.DateAddedDesc)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetListDetailAsync_PublicList_VisibleToAnonymous_WithOwnerName()
    {
        SetActiveUser(_ownerId);
        int id = await CreateAsync("Open Shelf", isPublic: true);

        SetActiveUser(FakeActiveUserContext.Anonymous());
        CustomListDetailDto? detail = await GetDetailAsync(id);

        detail.Should().NotBeNull();
        detail!.OwnerUserId.Should().Be(_ownerId);
        detail.OwnerUserName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetPublicListsByUserAsync_ReturnsOnlyPublicLists()
    {
        SetActiveUser(_ownerId);
        await CreateAsync("Private Shelf", isPublic: false);
        int publicId = await CreateAsync("Public Shelf", isPublic: true);

        SetActiveUser(_otherUserId);
        List<CustomListSummaryDto> lists = await GetPublicByUserAsync(_ownerId);

        lists.Should().ContainSingle(l => l.CustomListId == publicId);
    }

    [Fact]
    public async Task GetMyListMembershipsAsync_FlagsListsContainingTheStory()
    {
        SetActiveUser(_ownerId);
        int storyId = await SeedStoryAsync();
        int withStory = await CreateAsync("Has it", false);
        int without = await CreateAsync("Empty", false);
        await AddStoryAsync(withStory, storyId);

        List<CustomListMembershipDto> memberships = await GetMembershipsAsync(storyId);

        memberships.Should().HaveCount(2);
        memberships.Single(m => m.CustomListId == withStory).ContainsStory.Should().BeTrue();
        memberships.Single(m => m.CustomListId == without).ContainsStory.Should().BeFalse();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Sorts + content-rating filtering on entry reads
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetListStoryIdsAsync_DateAddedSorts_OrderByEntryAge()
    {
        SetActiveUser(_ownerId);
        int first = await SeedStoryAsync();
        int second = await SeedStoryAsync();
        int id = await CreateAsync("Ordered", false);
        await AddStoryAsync(id, first);
        await AddStoryAsync(id, second);
        // Back-to-back adds can land on the same timestamp — pin an unambiguous gap so the test
        // asserts the sort, not the clock.
        await SetEntryDateAddedAsync(id, first, DateTime.UtcNow.AddMinutes(-10));

        (await GetStoryIdsAsync(id, CustomListSortEnum.DateAddedAsc)).Should().Equal(first, second);
        (await GetStoryIdsAsync(id, CustomListSortEnum.DateAddedDesc)).Should().Equal(second, first);
    }

    [Fact]
    public async Task GetListStoryIdsAsync_TitleSorts_OrderByStoryTitle()
    {
        SetActiveUser(_ownerId);
        // SeedStoryAsync titles are GUID-suffixed — set deterministic titles directly.
        int storyA = await SeedStoryAsync();
        int storyZ = await SeedStoryAsync();
        await SetStoryTitleAsync(storyA, "AAA First");
        await SetStoryTitleAsync(storyZ, "ZZZ Last");
        int id = await CreateAsync("Alphabetical", false);
        await AddStoryAsync(id, storyZ); // added first, so date order ≠ title order
        await AddStoryAsync(id, storyA);

        (await GetStoryIdsAsync(id, CustomListSortEnum.TitleAsc)).Should().Equal(storyA, storyZ);
        (await GetStoryIdsAsync(id, CustomListSortEnum.TitleDesc)).Should().Equal(storyZ, storyA);
    }

    [Fact]
    public async Task GetListStoryIdsAsync_MatureOffViewer_HidesMRatedEntries_AndCountMatches()
    {
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_ownerId, showMatureContent: true));
        int eStory = await SeedStoryAsync(rating: Rating.E);
        int mStory = await SeedStoryAsync(rating: Rating.M);
        int id = await CreateAsync("Mixed Ratings", isPublic: true);
        await AddStoryAsync(id, eStory);
        await AddStoryAsync(id, mStory);

        // Mature-on owner sees both.
        (await GetStoryIdsAsync(id, CustomListSortEnum.DateAddedAsc)).Should().Equal(eStory, mStory);
        (await GetDetailAsync(id))!.StoryCount.Should().Be(2);

        // Mature-off viewer sees only the E story — ids AND count agree (no phantom count).
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_otherUserId, showMatureContent: false));
        (await GetStoryIdsAsync(id, CustomListSortEnum.DateAddedAsc)).Should().Equal(eStory);
        (await GetDetailAsync(id))!.StoryCount.Should().Be(1);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Clone
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CloneListAsync_PublicList_CopiesEntries_StartsPrivate_IndependentOfSource()
    {
        SetActiveUser(_ownerId);
        int story1 = await SeedStoryAsync();
        int story2 = await SeedStoryAsync();
        int sourceId = await CreateAsync("Great Reads", isPublic: true);
        await AddStoryAsync(sourceId, story1);
        await AddStoryAsync(sourceId, story2);

        SetActiveUser(_otherUserId);
        int cloneId = await CloneAsync(sourceId);

        CustomListDetailDto? clone = await GetDetailAsync(cloneId);
        clone.Should().NotBeNull();
        clone!.OwnerUserId.Should().Be(_otherUserId);
        clone.IsPublic.Should().BeFalse(); // sharing is not transitive
        clone.ListName.Should().Be("Great Reads"); // no collision in the cloner's account
        (await GetStoryIdsAsync(cloneId, CustomListSortEnum.DateAddedAsc))
            .Should().BeEquivalentTo([story1, story2]);

        // Independence: removing from the clone leaves the source untouched.
        await RemoveStoryAsync(cloneId, story1);
        SetActiveUser(_ownerId);
        (await GetStoryIdsAsync(sourceId, CustomListSortEnum.DateAddedAsc))
            .Should().BeEquivalentTo([story1, story2]);
    }

    [Fact]
    public async Task CloneListAsync_NameCollision_Disambiguates()
    {
        SetActiveUser(_ownerId);
        int sourceId = await CreateAsync("Faves", isPublic: true);

        SetActiveUser(_otherUserId);
        await CreateAsync("Faves", false); // cloner already has this name

        int cloneId = await CloneAsync(sourceId);
        (await GetDetailAsync(cloneId))!.ListName.Should().Be("Faves (copy)");
    }

    [Fact]
    public async Task CloneListAsync_CopiesOnlyClonerVisibleEntries()
    {
        // Settled 2026-07-13: clone never smuggles rating-hidden content into the cloner's account.
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_ownerId, showMatureContent: true));
        int eStory = await SeedStoryAsync(rating: Rating.E);
        int mStory = await SeedStoryAsync(rating: Rating.M);
        int sourceId = await CreateAsync("Mixed", isPublic: true);
        await AddStoryAsync(sourceId, eStory);
        await AddStoryAsync(sourceId, mStory);

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_otherUserId, showMatureContent: false));
        int cloneId = await CloneAsync(sourceId);

        // Ground truth via the write context (unfiltered): the M entry must not EXIST in the
        // clone — not merely be hidden from the cloner's reads.
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        List<int> cloneEntryIds = await db.CustomListEntries
            .Where(e => e.ListId == cloneId).Select(e => e.StoryId).ToListAsync();
        cloneEntryIds.Should().Equal(eStory);
    }

    [Fact]
    public async Task CloneListAsync_PrivateSource_NonOwner_ThrowsValidation()
    {
        SetActiveUser(_ownerId);
        int sourceId = await CreateAsync("Secret", isPublic: false);

        SetActiveUser(_otherUserId);
        Func<Task> act = () => CloneAsync(sourceId);
        await act.Should().ThrowAsync<CustomListValidationException>();
    }

    [Fact]
    public async Task CloneListAsync_OwnPrivateList_SelfCloneAllowed()
    {
        SetActiveUser(_ownerId);
        int sourceId = await CreateAsync("Mine", isPublic: false);

        int cloneId = await CloneAsync(sourceId);

        (await GetDetailAsync(cloneId))!.ListName.Should().Be("Mine (copy)");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Story delete cascade
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeletingStory_CascadesItsListEntries()
    {
        SetActiveUser(_ownerId);
        int storyId = await SeedStoryAsync();
        int id = await CreateAsync("Shelf", false);
        await AddStoryAsync(id, storyId);

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Story story = await db.Stories.SingleAsync(s => s.StoryId == storyId);
            db.Stories.Remove(story);
            await db.SaveChangesAsync();
        }

        (await GetStoryIdsAsync(id, CustomListSortEnum.DateAddedDesc)).Should().BeEmpty();
        (await GetDetailAsync(id))!.StoryCount.Should().Be(0);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Private helpers — seeding
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Sets a deterministic StoryListing title (SeedStoryAsync titles are GUID-suffixed).</summary>
    private async Task SetStoryTitleAsync(int storyId, string title)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        StoryListing listing = await db.Set<StoryListing>().SingleAsync(l => l.StoryId == storyId);
        listing.StoryTitle = title;
        await db.SaveChangesAsync();
    }

    /// <summary>Pins an entry's DateAdded so date-sort assertions don't race the clock.</summary>
    private async Task SetEntryDateAddedAsync(int listId, int storyId, DateTime dateAdded)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        CustomListEntry entry = await db.CustomListEntries
            .SingleAsync(e => e.ListId == listId && e.StoryId == storyId);
        entry.DateAdded = dateAdded;
        await db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Private helpers — service calls
    // ─────────────────────────────────────────────────────────────────────────────

    private async Task<int> CreateAsync(string listName, bool isPublic)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ICustomListWriteService svc = scope.ServiceProvider.GetRequiredService<ICustomListWriteService>();
        return await svc.CreateListAsync(listName, isPublic);
    }

    private async Task RenameAsync(int listId, string newName)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ICustomListWriteService svc = scope.ServiceProvider.GetRequiredService<ICustomListWriteService>();
        await svc.RenameListAsync(listId, newName);
    }

    private async Task SetVisibilityAsync(int listId, bool isPublic)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ICustomListWriteService svc = scope.ServiceProvider.GetRequiredService<ICustomListWriteService>();
        await svc.SetListVisibilityAsync(listId, isPublic);
    }

    private async Task DeleteAsync(int listId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ICustomListWriteService svc = scope.ServiceProvider.GetRequiredService<ICustomListWriteService>();
        await svc.DeleteListAsync(listId);
    }

    private async Task AddStoryAsync(int listId, int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ICustomListWriteService svc = scope.ServiceProvider.GetRequiredService<ICustomListWriteService>();
        await svc.AddStoryAsync(listId, storyId);
    }

    private async Task RemoveStoryAsync(int listId, int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ICustomListWriteService svc = scope.ServiceProvider.GetRequiredService<ICustomListWriteService>();
        await svc.RemoveStoryAsync(listId, storyId);
    }

    private async Task<int> CloneAsync(int sourceListId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ICustomListWriteService svc = scope.ServiceProvider.GetRequiredService<ICustomListWriteService>();
        return await svc.CloneListAsync(sourceListId);
    }

    private async Task<CustomListDetailDto?> GetDetailAsync(int listId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ICustomListReadService svc = scope.ServiceProvider.GetRequiredService<ICustomListReadService>();
        return await svc.GetListDetailAsync(listId);
    }

    private async Task<List<CustomListSummaryDto>> GetMyListsAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ICustomListReadService svc = scope.ServiceProvider.GetRequiredService<ICustomListReadService>();
        return await svc.GetMyListsAsync();
    }

    private async Task<IReadOnlyList<int>> GetStoryIdsAsync(int listId, CustomListSortEnum sort)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ICustomListReadService svc = scope.ServiceProvider.GetRequiredService<ICustomListReadService>();
        return await svc.GetListStoryIdsAsync(listId, sort);
    }

    private async Task<List<CustomListSummaryDto>> GetPublicByUserAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ICustomListReadService svc = scope.ServiceProvider.GetRequiredService<ICustomListReadService>();
        return await svc.GetPublicListsByUserAsync(userId);
    }

    private async Task<List<CustomListMembershipDto>> GetMembershipsAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ICustomListReadService svc = scope.ServiceProvider.GetRequiredService<ICustomListReadService>();
        return await svc.GetMyListMembershipsAsync(storyId);
    }
}
