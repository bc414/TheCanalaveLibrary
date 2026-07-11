using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="ISavedTagSelectionWriteService"/> / <see
/// cref="ISavedTagSelectionReadService"/> (Feature 15, WU43). Covers: CRUD + owner-gating,
/// per-user duplicate-nickname rejection, <c>IsExcluded</c> persisted both ways, wholesale entry
/// replacement on update, sort orders, public/private visibility, copy-on-write independence, and
/// the <c>SavedTagSelection.UserId</c> Cascade on user delete.
///
/// <b>Per-test seeding:</b> every test seeds users via <c>SeedUserAsync</c> and tags via the local
/// <see cref="SeedTagAsync"/> helper; Respawn resets the DB between every test — see testing.md.
///
/// Tier: Integration (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class SavedTagSelectionServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
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
    public async Task CreateAsync_Owner_InsertsRowWithIncludedAndExcludedEntries()
    {
        int includeTag = await SeedTagAsync();
        int excludeTag = await SeedTagAsync();
        SetActiveUser(_ownerId);

        int id = await CreateAsync(new SavedTagSelectionInput(
            "Fluff Combo", "cozy no-angst", false, [includeTag], [excludeTag]));

        SavedTagSelectionDetailDto? detail = await GetDetailAsync(id);
        detail.Should().NotBeNull();
        detail!.Nickname.Should().Be("Fluff Combo");
        detail.Description.Should().Be("cozy no-angst");
        detail.OwnerUserId.Should().Be(_ownerId);
        detail.IncludedTags.Should().ContainSingle(t => t.TagId == includeTag);
        detail.ExcludedTags.Should().ContainSingle(t => t.TagId == excludeTag);
    }

    [Fact]
    public async Task CreateAsync_Anonymous_ThrowsInvalidOperation()
    {
        int tagId = await SeedTagAsync();
        SetActiveUser(FakeActiveUserContext.Anonymous());

        Func<Task> act = () => CreateAsync(new SavedTagSelectionInput("Anon", null, false, [tagId], []));
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_EmptyTagSet_ThrowsValidationException()
    {
        SetActiveUser(_ownerId);
        Func<Task> act = () => CreateAsync(new SavedTagSelectionInput("Empty", null, false, [], []));
        await act.Should().ThrowAsync<SavedTagSelectionValidationException>();
    }

    [Fact]
    public async Task CreateAsync_EmptyNickname_ThrowsValidationException()
    {
        int tagId = await SeedTagAsync();
        SetActiveUser(_ownerId);
        Func<Task> act = () => CreateAsync(new SavedTagSelectionInput("", null, false, [tagId], []));
        await act.Should().ThrowAsync<SavedTagSelectionValidationException>();
    }

    [Fact]
    public async Task CreateAsync_DuplicateNicknameForSameUser_ThrowsValidationException()
    {
        int tag1 = await SeedTagAsync();
        int tag2 = await SeedTagAsync();
        SetActiveUser(_ownerId);
        await CreateAsync(new SavedTagSelectionInput("Mine", null, false, [tag1], []));

        Func<Task> act = () => CreateAsync(new SavedTagSelectionInput("mine", null, false, [tag2], [])); // case-insensitive
        await act.Should().ThrowAsync<SavedTagSelectionValidationException>();
    }

    [Fact]
    public async Task CreateAsync_SameNicknameDifferentUser_Succeeds()
    {
        int tag1 = await SeedTagAsync();
        int tag2 = await SeedTagAsync();
        SetActiveUser(_ownerId);
        await CreateAsync(new SavedTagSelectionInput("Shared Name", null, false, [tag1], []));

        SetActiveUser(_otherUserId);
        Func<Task> act = () => CreateAsync(new SavedTagSelectionInput("Shared Name", null, false, [tag2], []));
        await act.Should().NotThrowAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Update — owner gating + wholesale entry replacement
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_Owner_ReplacesEntriesWholesale()
    {
        int tagA = await SeedTagAsync();
        int tagB = await SeedTagAsync();
        SetActiveUser(_ownerId);
        int id = await CreateAsync(new SavedTagSelectionInput("Original", null, false, [tagA], []));

        await UpdateAsync(id, new SavedTagSelectionInput("Renamed", "new note", true, [tagB], []));

        SavedTagSelectionDetailDto? detail = await GetDetailAsync(id);
        detail!.Nickname.Should().Be("Renamed");
        detail.Description.Should().Be("new note");
        detail.IsPublic.Should().BeTrue();
        detail.IncludedTags.Should().ContainSingle(t => t.TagId == tagB);
        detail.IncludedTags.Should().NotContain(t => t.TagId == tagA, "update replaces entries wholesale, not a merge");
    }

    [Fact]
    public async Task UpdateAsync_NonOwner_ThrowsUnauthorizedAccess()
    {
        int tagId = await SeedTagAsync();
        SetActiveUser(_ownerId);
        int id = await CreateAsync(new SavedTagSelectionInput("Locked", null, false, [tagId], []));

        SetActiveUser(_otherUserId);
        Func<Task> act = () => UpdateAsync(id, new SavedTagSelectionInput("Hacked", null, false, [tagId], []));
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Delete — owner gating + cascade
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_Owner_RemovesRowAndCascadesEntries()
    {
        int tagId = await SeedTagAsync();
        SetActiveUser(_ownerId);
        int id = await CreateAsync(new SavedTagSelectionInput("Temp", null, false, [tagId], []));

        await DeleteAsync(id);

        (await GetDetailAsync(id)).Should().BeNull();

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.SavedTagSelectionEntries.AnyAsync(e => e.SavedTagSelectionId == id)).Should().BeFalse(
            "entries must cascade when the selection is deleted");
        (await db.Tags.AnyAsync(t => t.TagId == tagId)).Should().BeTrue(
            "deleting a selection must not delete the referenced tag");
    }

    [Fact]
    public async Task DeleteAsync_NonOwner_ThrowsUnauthorizedAccess()
    {
        int tagId = await SeedTagAsync();
        SetActiveUser(_ownerId);
        int id = await CreateAsync(new SavedTagSelectionInput("Mine", null, false, [tagId], []));

        SetActiveUser(_otherUserId);
        Func<Task> act = () => DeleteAsync(id);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Reads — GetMySelectionsAsync (user-scoping + sort)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMySelectionsAsync_ReturnsOnlyCallersOwnSelections()
    {
        int tag1 = await SeedTagAsync();
        int tag2 = await SeedTagAsync();
        SetActiveUser(_ownerId);
        int mineId = await CreateAsync(new SavedTagSelectionInput("Mine", null, false, [tag1], []));

        SetActiveUser(_otherUserId);
        await CreateAsync(new SavedTagSelectionInput("Theirs", null, false, [tag2], []));

        SetActiveUser(_ownerId);
        List<SavedTagSelectionSummaryDto> mine = await GetMySelectionsAsync(SavedTagSelectionSortEnum.DateCreatedDesc);
        mine.Should().ContainSingle(s => s.Id == mineId && s.Nickname == "Mine");
    }

    [Fact]
    public async Task GetMySelectionsAsync_Anonymous_ReturnsEmpty()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());
        List<SavedTagSelectionSummaryDto> result = await GetMySelectionsAsync(SavedTagSelectionSortEnum.DateCreatedDesc);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMySelectionsAsync_NicknameAsc_OrdersAlphabetically()
    {
        int tagId = await SeedTagAsync();
        SetActiveUser(_ownerId);
        await CreateAsync(new SavedTagSelectionInput("Zebra", null, false, [tagId], []));
        await CreateAsync(new SavedTagSelectionInput("Apple", null, false, [tagId], []));

        List<SavedTagSelectionSummaryDto> result = await GetMySelectionsAsync(SavedTagSelectionSortEnum.NicknameAsc);
        result.Select(s => s.Nickname).Should().Equal("Apple", "Zebra");
    }

    [Fact]
    public async Task GetMySelectionsAsync_NicknameDesc_OrdersReverseAlphabetically()
    {
        int tagId = await SeedTagAsync();
        SetActiveUser(_ownerId);
        await CreateAsync(new SavedTagSelectionInput("Apple", null, false, [tagId], []));
        await CreateAsync(new SavedTagSelectionInput("Zebra", null, false, [tagId], []));

        List<SavedTagSelectionSummaryDto> result = await GetMySelectionsAsync(SavedTagSelectionSortEnum.NicknameDesc);
        result.Select(s => s.Nickname).Should().Equal("Zebra", "Apple");
    }

    [Fact]
    public async Task GetMySelectionsAsync_IncludedAndExcludedCounts_ReflectEntrySplit()
    {
        int inc1 = await SeedTagAsync();
        int inc2 = await SeedTagAsync();
        int exc1 = await SeedTagAsync();
        SetActiveUser(_ownerId);
        await CreateAsync(new SavedTagSelectionInput("Counted", null, false, [inc1, inc2], [exc1]));

        SavedTagSelectionSummaryDto row = (await GetMySelectionsAsync(SavedTagSelectionSortEnum.DateCreatedDesc)).Single();
        row.IncludedCount.Should().Be(2);
        row.ExcludedCount.Should().Be(1);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Reads — GetSelectionDetailAsync visibility gate
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSelectionDetailAsync_PrivateSelection_NonOwnerViewer_ReturnsNull()
    {
        int tagId = await SeedTagAsync();
        SetActiveUser(_ownerId);
        int id = await CreateAsync(new SavedTagSelectionInput("Private", null, false, [tagId], []));

        SetActiveUser(_otherUserId);
        (await GetDetailAsync(id)).Should().BeNull();
    }

    [Fact]
    public async Task GetSelectionDetailAsync_PublicSelection_NonOwnerViewer_ReturnsDetail()
    {
        int tagId = await SeedTagAsync();
        SetActiveUser(_ownerId);
        int id = await CreateAsync(new SavedTagSelectionInput("Public", null, true, [tagId], []));

        SetActiveUser(_otherUserId);
        (await GetDetailAsync(id)).Should().NotBeNull();
    }

    [Fact]
    public async Task GetSelectionDetailAsync_UnknownId_ReturnsNull()
    {
        SetActiveUser(_ownerId);
        (await GetDetailAsync(int.MaxValue)).Should().BeNull();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Reads — GetPublicSelectionsByUserAsync (profile Tag Selections tab)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPublicSelectionsByUserAsync_ReturnsOnlyPublicOnes()
    {
        int tag1 = await SeedTagAsync();
        int tag2 = await SeedTagAsync();
        SetActiveUser(_ownerId);
        int publicId = await CreateAsync(new SavedTagSelectionInput("Public One", null, true, [tag1], []));
        await CreateAsync(new SavedTagSelectionInput("Private One", null, false, [tag2], []));

        List<SavedTagSelectionDetailDto> result = await GetPublicByUserAsync(_ownerId);
        result.Should().ContainSingle(s => s.Id == publicId);
    }

    [Fact]
    public async Task GetPublicSelectionsByUserAsync_NoPublicSelections_ReturnsEmpty()
    {
        int tagId = await SeedTagAsync();
        SetActiveUser(_ownerId);
        await CreateAsync(new SavedTagSelectionInput("Private Only", null, false, [tagId], []));

        (await GetPublicByUserAsync(_ownerId)).Should().BeEmpty();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Copy-on-write share
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CopyPublicSelectionAsync_PublicSource_CreatesIndependentCopyOwnedByCopier()
    {
        int tagId = await SeedTagAsync();
        SetActiveUser(_ownerId);
        int sourceId = await CreateAsync(new SavedTagSelectionInput("Shareable", "note", true, [tagId], []));

        SetActiveUser(_otherUserId);
        int copyId = await CopyAsync(sourceId);

        SavedTagSelectionDetailDto? copy = await GetDetailAsync(copyId);
        copy.Should().NotBeNull();
        copy!.OwnerUserId.Should().Be(_otherUserId);
        copy.IsPublic.Should().BeFalse("sharing is not transitive — a copy starts private regardless of the source");
        copy.IncludedTags.Should().ContainSingle(t => t.TagId == tagId);
    }

    [Fact]
    public async Task CopyPublicSelectionAsync_EditingCopy_DoesNotAffectSource()
    {
        int tag1 = await SeedTagAsync();
        int tag2 = await SeedTagAsync();
        SetActiveUser(_ownerId);
        int sourceId = await CreateAsync(new SavedTagSelectionInput("Shareable", null, true, [tag1], []));

        SetActiveUser(_otherUserId);
        int copyId = await CopyAsync(sourceId);
        await UpdateAsync(copyId, new SavedTagSelectionInput("Edited Copy", null, false, [tag2], []));

        SetActiveUser(_ownerId);
        SavedTagSelectionDetailDto? source = await GetDetailAsync(sourceId);
        source!.Nickname.Should().Be("Shareable", "editing the copy must never affect the source");
        source.IncludedTags.Should().ContainSingle(t => t.TagId == tag1);
    }

    [Fact]
    public async Task CopyPublicSelectionAsync_DeletingSource_DoesNotAffectCopy()
    {
        int tagId = await SeedTagAsync();
        SetActiveUser(_ownerId);
        int sourceId = await CreateAsync(new SavedTagSelectionInput("Shareable", null, true, [tagId], []));

        SetActiveUser(_otherUserId);
        int copyId = await CopyAsync(sourceId);

        SetActiveUser(_ownerId);
        await DeleteAsync(sourceId);

        SetActiveUser(_otherUserId);
        (await GetDetailAsync(copyId)).Should().NotBeNull("deleting the source must not delete the copier's independent copy");
    }

    [Fact]
    public async Task CopyPublicSelectionAsync_NoNicknameCollision_KeepsSourceNicknameVerbatim()
    {
        // Copying is a verbatim-nickname copy UNLESS it would collide with the copier's own
        // existing nicknames — no collision here, so no "(copy)" suffix is added.
        int tagId = await SeedTagAsync();
        SetActiveUser(_ownerId);
        int sourceId = await CreateAsync(new SavedTagSelectionInput("Unique Name", null, true, [tagId], []));

        SetActiveUser(_otherUserId);
        int copyId = await CopyAsync(sourceId);
        (await GetDetailAsync(copyId))!.Nickname.Should().Be("Unique Name");
    }

    [Fact]
    public async Task CopyPublicSelectionAsync_NicknameCollidesWithSource_AppendsCopySuffix()
    {
        int tag1 = await SeedTagAsync();
        int tag2 = await SeedTagAsync();
        SetActiveUser(_ownerId);
        int sourceId = await CreateAsync(new SavedTagSelectionInput("Fluff", null, true, [tag1], []));

        SetActiveUser(_otherUserId);
        // Copier already has a selection with the exact same nickname as the source.
        await CreateAsync(new SavedTagSelectionInput("Fluff", null, false, [tag2], []));

        int copyId = await CopyAsync(sourceId);
        SavedTagSelectionDetailDto? copy = await GetDetailAsync(copyId);
        copy!.Nickname.Should().Be("Fluff (copy)");
    }

    [Fact]
    public async Task CopyPublicSelectionAsync_NicknameAndFirstCopySuffixBothTaken_EscalatesSuffix()
    {
        int tag1 = await SeedTagAsync();
        int tag2 = await SeedTagAsync();
        int tag3 = await SeedTagAsync();
        SetActiveUser(_ownerId);
        int sourceId = await CreateAsync(new SavedTagSelectionInput("Fluff", null, true, [tag1], []));

        SetActiveUser(_otherUserId);
        await CreateAsync(new SavedTagSelectionInput("Fluff", null, false, [tag2], []));
        await CreateAsync(new SavedTagSelectionInput("Fluff (copy)", null, false, [tag3], []));

        int copyId = await CopyAsync(sourceId);
        SavedTagSelectionDetailDto? copy = await GetDetailAsync(copyId);
        copy!.Nickname.Should().Be("Fluff (copy 2)");
    }

    [Fact]
    public async Task CopyPublicSelectionAsync_PrivateSourceNotOwned_ThrowsValidationException()
    {
        int tagId = await SeedTagAsync();
        SetActiveUser(_ownerId);
        int sourceId = await CreateAsync(new SavedTagSelectionInput("Private", null, false, [tagId], []));

        SetActiveUser(_otherUserId);
        Func<Task> act = () => CopyAsync(sourceId);
        await act.Should().ThrowAsync<SavedTagSelectionValidationException>();
    }

    [Fact]
    public async Task CopyPublicSelectionAsync_UnknownSource_ThrowsValidationException()
    {
        SetActiveUser(_ownerId);
        Func<Task> act = () => CopyAsync(int.MaxValue);
        await act.Should().ThrowAsync<SavedTagSelectionValidationException>();
    }

    [Fact]
    public async Task CopyPublicSelectionAsync_OwnPrivateSelection_Succeeds()
    {
        // The owner copying their own selection (public or not) is allowed — only a stranger needs
        // IsPublic to be true.
        int tagId = await SeedTagAsync();
        SetActiveUser(_ownerId);
        int sourceId = await CreateAsync(new SavedTagSelectionInput("Mine", null, false, [tagId], []));

        int copyId = await CopyAsync(sourceId);
        (await GetDetailAsync(copyId))!.OwnerUserId.Should().Be(_ownerId);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Cascade — SavedTagSelection.UserId (Cascade on User delete)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteUser_CascadesIntoSavedTagSelectionsAndEntries()
    {
        int tagId = await SeedTagAsync();
        int userId = await SeedUserAsync("to-delete");
        SetActiveUser(userId);
        int selectionId = await CreateAsync(new SavedTagSelectionInput("Doomed", null, false, [tagId], []));

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            UserDeletionService sut = scope.ServiceProvider.GetRequiredService<UserDeletionService>();
            (await sut.DeleteUserAsync(userId)).Should().BeTrue();
        }

        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.SavedTagSelections.AnyAsync(s => s.SavedTagSelectionId == selectionId)).Should().BeFalse(
            "SavedTagSelection.UserId has an OnDelete Cascade — it must be gone when the user is gone");
        (await db.SavedTagSelectionEntries.AnyAsync(e => e.SavedTagSelectionId == selectionId)).Should().BeFalse();
        (await db.Tags.AnyAsync(t => t.TagId == tagId)).Should().BeTrue(
            "deleting the user's selection must not delete the referenced tag");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Seeding helper — local to this class (mirrors TreeSearchComposeTests.SeedTagAsync)
    // ─────────────────────────────────────────────────────────────────────────────

    private async Task<int> SeedTagAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        Tag tag = new() { TagName = $"STS-Tag-{suffix}", TagTypeId = TagTypeEnum.Genre };
        db.Tags.Add(tag);
        await db.SaveChangesAsync();
        return tag.TagId;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Private helpers — service calls
    // ─────────────────────────────────────────────────────────────────────────────

    private async Task<int> CreateAsync(SavedTagSelectionInput input)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ISavedTagSelectionWriteService svc = scope.ServiceProvider.GetRequiredService<ISavedTagSelectionWriteService>();
        return await svc.CreateAsync(input);
    }

    private async Task UpdateAsync(int id, SavedTagSelectionInput input)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ISavedTagSelectionWriteService svc = scope.ServiceProvider.GetRequiredService<ISavedTagSelectionWriteService>();
        await svc.UpdateAsync(id, input);
    }

    private async Task DeleteAsync(int id)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ISavedTagSelectionWriteService svc = scope.ServiceProvider.GetRequiredService<ISavedTagSelectionWriteService>();
        await svc.DeleteAsync(id);
    }

    private async Task<int> CopyAsync(int sourceId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ISavedTagSelectionWriteService svc = scope.ServiceProvider.GetRequiredService<ISavedTagSelectionWriteService>();
        return await svc.CopyPublicSelectionAsync(sourceId);
    }

    private async Task<SavedTagSelectionDetailDto?> GetDetailAsync(int id)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ISavedTagSelectionReadService svc = scope.ServiceProvider.GetRequiredService<ISavedTagSelectionReadService>();
        return await svc.GetSelectionDetailAsync(id);
    }

    private async Task<List<SavedTagSelectionSummaryDto>> GetMySelectionsAsync(SavedTagSelectionSortEnum sort)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ISavedTagSelectionReadService svc = scope.ServiceProvider.GetRequiredService<ISavedTagSelectionReadService>();
        return await svc.GetMySelectionsAsync(sort);
    }

    private async Task<List<SavedTagSelectionDetailDto>> GetPublicByUserAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ISavedTagSelectionReadService svc = scope.ServiceProvider.GetRequiredService<ISavedTagSelectionReadService>();
        return await svc.GetPublicSelectionsByUserAsync(userId);
    }
}
