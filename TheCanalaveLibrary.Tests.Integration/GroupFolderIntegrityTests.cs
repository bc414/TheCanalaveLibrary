using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Group folder integrity and <c>group_stories</c> set semantics (WU-SchemaHardening, schema audit §2.4,
/// service audit §2.10, worksheet D23's <c>group_stories</c> row). Before the WU: a deleted folder's
/// sub-folders dangled out of the tree, any <c>ParentFolderId</c> was stored unchecked, names were
/// unvalidated, two top-level folders could share a name, and a concurrent double add filed a story
/// twice. Rules: <c>layer2-services.md</c> §"Group Folder Integrity".
/// <para>
/// <b>Per-test seeding:</b> two users in <see cref="InitializeAsync"/>. Groups come from
/// <c>CreateGroupAsync</c> (the creator becomes the group's Admin), stories from <c>SeedStoryAsync</c>.
/// FK parents: every folder row's group exists before it is created; a folder assignment needs the
/// <c>GroupStory</c> row <c>AddStoryAsync</c> writes. The race tests use
/// <see cref="InterleavingCommandInterceptor"/> to land the competing write between the service's check
/// and its write (testing.md §"Testing a check-then-act guard: interleave, don't race").
/// </para>
/// Tier: Integration (Testcontainers Postgres).
/// </summary>
[Collection("Postgres")]
public class GroupFolderIntegrityTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _adminId;
    private int _otherId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _adminId = await SeedUserAsync("FolderAdmin");
        _otherId = await SeedUserAsync("OtherAdmin");
        SetActiveUser(_adminId);
    }

    // ── Names ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateFolder_DuplicateTopLevelName_ThrowsGroupValidation()
    {
        int groupId = await CreateGroupAsync();
        await CreateFolderAsync(groupId, "Favorites");

        Func<Task> act = () => CreateFolderAsync(groupId, "Favorites");

        (await act.Should().ThrowAsync<GroupValidationException>(
                "two top-level folders could share a name under the old NULLS DISTINCT index"))
            .Which.Errors.Should().ContainSingle().Which.Should().Be(GroupFolderValidations.DuplicateName("Favorites"));
        (await FoldersAsync(groupId)).Should().ContainSingle();
    }

    [Fact]
    public async Task CreateFolder_DuplicateSiblingName_ThrowsGroupValidation()
    {
        int groupId = await CreateGroupAsync();
        int parentId = await CreateFolderAsync(groupId, "Parent");
        await CreateFolderAsync(groupId, "Child", parentId);

        Func<Task> act = () => CreateFolderAsync(groupId, "Child", parentId);

        await act.Should().ThrowAsync<GroupValidationException>("a duplicate sibling used to be a raw 500");
    }

    [Fact]
    public async Task CreateFolder_SameNameAtTopLevelAndUnderDifferentParents_Succeeds()
    {
        int groupId = await CreateGroupAsync();
        int a = await CreateFolderAsync(groupId, "A");
        int b = await CreateFolderAsync(groupId, "B");

        await CreateFolderAsync(groupId, "Shared");
        await CreateFolderAsync(groupId, "Shared", a);
        await CreateFolderAsync(groupId, "Shared", b);

        (await FoldersAsync(groupId)).Count(f => f.Name == "Shared").Should().Be(3,
            "uniqueness is per sibling set, and each parent (and the top level) is its own set");
    }

    [Fact]
    public async Task CreateFolder_CaseVariantOfASiblingName_IsAllowed()
    {
        // Byte-wise on purpose: case-insensitive names are worksheet D29 (pending), and a
        // case-insensitive pre-check over the case-sensitive index would be the race service §2.12
        // reports for saved selections.
        int groupId = await CreateGroupAsync();
        await CreateFolderAsync(groupId, "Favorites");

        await FluentActions.Awaiting(() => CreateFolderAsync(groupId, "favorites")).Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    public async Task CreateFolder_BlankName_ThrowsGroupValidation(string name)
    {
        int groupId = await CreateGroupAsync();

        Func<Task> act = () => CreateFolderAsync(groupId, name);

        (await act.Should().ThrowAsync<GroupValidationException>("a blank name used to persist"))
            .Which.Errors.Should().Contain(GroupFolderValidations.NameRequired);
        (await FoldersAsync(groupId)).Should().BeEmpty();
    }

    [Fact]
    public async Task CreateFolder_NameOverTheLimit_ThrowsGroupValidation()
    {
        int groupId = await CreateGroupAsync();

        Func<Task> act = () => CreateFolderAsync(groupId, new string('x', GroupConstants.MaxFolderNameLength + 1));

        (await act.Should().ThrowAsync<GroupValidationException>("an over-long name used to be a raw DbUpdateException"))
            .Which.Errors.Should().Contain(GroupFolderValidations.NameTooLong);
    }

    [Fact]
    public async Task CreateFolder_StoresTheTrimmedName()
    {
        int groupId = await CreateGroupAsync();

        int folderId = await CreateFolderAsync(groupId, "  Padded  ");

        (await FoldersAsync(groupId)).Single(f => f.GroupFolderId == folderId).Name.Should().Be("Padded");
    }

    [Fact]
    public async Task RenameFolder_ToASiblingsName_ThrowsGroupValidation()
    {
        int groupId = await CreateGroupAsync();
        await CreateFolderAsync(groupId, "Taken");
        int folderId = await CreateFolderAsync(groupId, "Mine");

        Func<Task> act = () => RenameFolderAsync(folderId, "Taken");

        await act.Should().ThrowAsync<GroupValidationException>();
        (await FoldersAsync(groupId)).Single(f => f.GroupFolderId == folderId).Name.Should().Be("Mine");
    }

    [Fact]
    public async Task RenameFolder_ToItsOwnName_IsANoOp()
    {
        int groupId = await CreateGroupAsync();
        int folderId = await CreateFolderAsync(groupId, "Same");

        await FluentActions.Awaiting(() => RenameFolderAsync(folderId, "  Same ")).Should().NotThrowAsync();

        (await FoldersAsync(groupId)).Single().Name.Should().Be("Same");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task RenameFolder_BlankName_ThrowsGroupValidation(string? name)
    {
        // A null name used to throw a NullReferenceException from newName.Trim() (a 500).
        int groupId = await CreateGroupAsync();
        int folderId = await CreateFolderAsync(groupId, "Keep");

        Func<Task> act = () => RenameFolderAsync(folderId, name!);

        await act.Should().ThrowAsync<GroupValidationException>();
        (await FoldersAsync(groupId)).Single().Name.Should().Be("Keep");
    }

    // ── Parent ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateFolder_NonexistentParent_ThrowsKeyNotFound()
    {
        int groupId = await CreateGroupAsync();

        Func<Task> act = () => CreateFolderAsync(groupId, "Orphan", parentId: 999_999);

        (await act.Should().ThrowAsync<KeyNotFoundException>()).WithMessage("Folder 999999 not found.");
        (await FoldersAsync(groupId)).Should().BeEmpty();
    }

    [Fact]
    public async Task CreateFolder_ParentFromAnotherGroup_ThrowsKeyNotFound_WithTheSameMessage()
    {
        SetActiveUser(_otherId);
        int otherGroupId = await CreateGroupAsync();
        int foreignFolderId = await CreateFolderAsync(otherGroupId, "Theirs");

        SetActiveUser(_adminId);
        int groupId = await CreateGroupAsync();

        Func<Task> act = () => CreateFolderAsync(groupId, "Hijack", parentId: foreignFolderId);

        (await act.Should().ThrowAsync<KeyNotFoundException>(
                "another group's folder is answered exactly like a missing one (D3.1 non-disclosure)"))
            .WithMessage($"Folder {foreignFolderId} not found.");
        (await FoldersAsync(groupId)).Should().BeEmpty();
    }

    // ── Delete: sub-folders move to the top level ───────────────────────────────────

    [Fact]
    public async Task DeleteFolder_WithSubFolders_MovesThemToTheTopLevel_AfterTheExistingOnes_WithTheirStories()
    {
        int groupId = await CreateGroupAsync();
        int first = await CreateFolderAsync(groupId, "First", sortOrder: 0);
        int parent = await CreateFolderAsync(groupId, "Parent", sortOrder: 1);
        int last = await CreateFolderAsync(groupId, "Last", sortOrder: 2);
        int childA = await CreateFolderAsync(groupId, "Child A", parent, sortOrder: 0);
        int childB = await CreateFolderAsync(groupId, "Child B", parent, sortOrder: 1);

        int storyInChild = await SeedStoryAsync();
        int storyInParent = await SeedStoryAsync();
        await AddStoryAsync(groupId, storyInChild, folderId: childA);
        await AddStoryAsync(groupId, storyInParent, folderId: parent);

        await DeleteFolderAsync(parent);

        GroupDetailDto detail = (await GetGroupAsync(groupId))!;
        detail.FolderTree.Select(f => f.GroupFolderId).Should().Equal([first, last, childA, childB],
            "the sub-folders join the top level after the existing folders, in their own order — before the " +
            "WU they pointed at a deleted parent and vanished from the tree");
        detail.FolderTree.Single(f => f.GroupFolderId == childA).Stories
            .Should().ContainSingle(s => s.StoryId == storyInChild, "a promoted folder keeps its story assignments");
        detail.Stories.Select(s => s.StoryId).Should().BeEquivalentTo([storyInChild, storyInParent],
            "deleting a folder never removes a story from the group");
        detail.FolderTree.SelectMany(f => f.Stories).Should().NotContain(s => s.StoryId == storyInParent,
            "the deleted folder's own assignments go with it");
    }

    [Fact]
    public async Task DeleteFolder_SubFolderOfASubFolder_MovesToTheTopLevel_NotToTheGrandparent()
    {
        // SET NULL semantics (spec §Group Tables): children become top-level, whatever the depth.
        int groupId = await CreateGroupAsync();
        int a = await CreateFolderAsync(groupId, "A");
        int b = await CreateFolderAsync(groupId, "B", a);
        int c = await CreateFolderAsync(groupId, "C", b);

        await DeleteFolderAsync(b);

        List<GroupFolder> folders = await FoldersAsync(groupId);
        folders.Single(f => f.GroupFolderId == c).ParentFolderId.Should().BeNull();
        folders.Select(f => f.GroupFolderId).Should().BeEquivalentTo([a, c]);
    }

    [Fact]
    public async Task DeleteFolder_SubFolderNamedLikeItsTopLevelParent_TakesTheParentsName()
    {
        // The database moves the sub-folder up only after the parent row is gone, so the freed
        // top-level name is available to it. A tracked EF delete would update the child first and trip
        // the unique index on its own parent.
        int groupId = await CreateGroupAsync();
        int parent = await CreateFolderAsync(groupId, "Pokemon");
        int child = await CreateFolderAsync(groupId, "Pokemon", parent);

        await DeleteFolderAsync(parent);

        GroupFolder only = (await FoldersAsync(groupId)).Single();
        only.GroupFolderId.Should().Be(child);
        only.ParentFolderId.Should().BeNull();
        only.Name.Should().Be("Pokemon");
    }

    [Fact]
    public async Task DeleteFolder_SubFolderNameCollidesWithATopLevelFolder_ThrowsAndDeletesNothing()
    {
        int groupId = await CreateGroupAsync();
        int parent = await CreateFolderAsync(groupId, "Fanfic");
        int child = await CreateFolderAsync(groupId, "Favorites", parent);
        await CreateFolderAsync(groupId, "Favorites");

        Func<Task> act = () => DeleteFolderAsync(parent);

        (await act.Should().ThrowAsync<GroupValidationException>())
            .Which.Errors.Should().ContainSingle()
            .Which.Should().Be(GroupFolderValidations.DeleteWouldDuplicate("Fanfic", "Favorites"));
        List<GroupFolder> folders = await FoldersAsync(groupId);
        folders.Should().HaveCount(3, "a refused delete deletes nothing");
        folders.Single(f => f.GroupFolderId == child).ParentFolderId.Should().Be(parent);
    }

    // ── group_stories is a set (D23) ─────────────────────────────────────────────────

    [Fact]
    public async Task AddStory_Twice_LeavesOneGroupStoryRow()
    {
        int groupId = await CreateGroupAsync();
        int storyId = await SeedStoryAsync();

        await AddStoryAsync(groupId, storyId);
        await AddStoryAsync(groupId, storyId);

        (await GroupStoryCountAsync(groupId, storyId)).Should().Be(1);
    }

    // ── Race backstops: the competing write lands between the check and the write ─────

    [Fact]
    public async Task AddStory_DuplicateLandingBetweenTheCheckAndTheInsert_IsAlreadyAdded_NoFolderNoNotification()
    {
        int groupId = await CreateGroupAsync();
        int folderId = await CreateFolderAsync(groupId, "Shelf");
        int storyId = await SeedStoryAsync(authorId: _otherId);

        InterleavingCommandInterceptor interceptor = new(ConnectionString, "INSERT INTO group_stories",
            interleavedSql: $"INSERT INTO group_stories (group_id, story_id, date_added) VALUES ({groupId}, {storyId}, now());",
            interceptReaders: true);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerGroupWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerGroupWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            await FluentActions.Awaiting(() => svc.AddStoryAsync(new AddGroupStoryDto
                    { GroupId = groupId, StoryId = storyId, GroupFolderId = folderId }))
                .Should().NotThrowAsync("the unique index refuses the second row, and that counts as already added");
            writeDb.ChangeTracker.Entries<GroupStory>().Should().BeEmpty(
                "the rejected row is detached, so a later save in the same circuit never retries it");
        }
        interceptor.Fired.Should().BeTrue();

        (await GroupStoryCountAsync(groupId, storyId)).Should().Be(1);
        using IServiceScope verify = Factory.Services.CreateScope();
        ApplicationDbContext db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM group_folder_group_story")
                .SingleAsync()).Should().Be(0, "the losing add files nothing into the folder");
        (await db.Notifications.CountAsync(n => n.RecipientUserId == _otherId)).Should().Be(0,
            "the losing add sends no YourStoryAddedToGroup to the author");
    }

    [Fact]
    public async Task CreateFolder_DuplicateLandingBetweenTheCheckAndTheInsert_ThrowsGroupValidation()
    {
        int groupId = await CreateGroupAsync();

        InterleavingCommandInterceptor interceptor = new(ConnectionString, "INSERT INTO group_folders",
            interleavedSql: $"INSERT INTO group_folders (group_id, name, max_rating, sort_order) VALUES ({groupId}, 'Late', 2, 0);",
            interceptReaders: true);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerGroupWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerGroupWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            (await FluentActions.Awaiting(() => svc.CreateFolderAsync(new CreateFolderDto { GroupId = groupId, Name = "Late" }))
                    .Should().ThrowAsync<GroupValidationException>("the race is answered like the check — never a raw 500"))
                .Which.Errors.Should().ContainSingle().Which.Should().Be(GroupFolderValidations.DuplicateName("Late"));
            writeDb.ChangeTracker.Entries<GroupFolder>().Should().BeEmpty("the rejected row is detached");
        }
        interceptor.Fired.Should().BeTrue();
        (await FoldersAsync(groupId)).Should().ContainSingle();
    }

    [Fact]
    public async Task CreateFolder_ParentDeletedBetweenTheCheckAndTheInsert_ThrowsKeyNotFound()
    {
        int groupId = await CreateGroupAsync();
        int parentId = await CreateFolderAsync(groupId, "Vanishing");

        InterleavingCommandInterceptor interceptor = new(ConnectionString, "INSERT INTO group_folders",
            interleavedSql: $"DELETE FROM group_folders WHERE group_folder_id = {parentId};",
            interceptReaders: true);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerGroupWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerGroupWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            (await FluentActions.Awaiting(() => svc.CreateFolderAsync(
                        new CreateFolderDto { GroupId = groupId, ParentFolderId = parentId, Name = "Child" }))
                    .Should().ThrowAsync<KeyNotFoundException>("the FK refusal is answered like a missing parent"))
                .WithMessage($"Folder {parentId} not found.");
        }
        interceptor.Fired.Should().BeTrue();
        (await FoldersAsync(groupId)).Should().BeEmpty();
    }

    [Fact]
    public async Task RenameFolder_DuplicateLandingBetweenTheCheckAndTheUpdate_ThrowsGroupValidation()
    {
        int groupId = await CreateGroupAsync();
        int folderId = await CreateFolderAsync(groupId, "Before");

        InterleavingCommandInterceptor interceptor = new(ConnectionString, "UPDATE group_folders",
            interleavedSql: $"INSERT INTO group_folders (group_id, name, max_rating, sort_order) VALUES ({groupId}, 'After', 2, 1);",
            interceptReaders: true);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerGroupWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerGroupWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            await FluentActions.Awaiting(() => svc.RenameFolderAsync(folderId, "After"))
                .Should().ThrowAsync<GroupValidationException>();
            writeDb.ChangeTracker.Entries<GroupFolder>().Should().BeEmpty("the rejected rename is detached");
        }
        interceptor.Fired.Should().BeTrue();
        (await FoldersAsync(groupId)).Single(f => f.GroupFolderId == folderId).Name.Should().Be("Before");
    }

    [Fact]
    public async Task DeleteFolder_CollidingTopLevelFolderLandingBeforeTheDelete_ThrowsAndDeletesNothing()
    {
        int groupId = await CreateGroupAsync();
        int parent = await CreateFolderAsync(groupId, "Fanfic");
        int child = await CreateFolderAsync(groupId, "Favorites", parent, sortOrder: 4);

        // The check passed (no top-level "Favorites" yet); one appears just before the DELETE, so the
        // database's SET NULL trips the unique index and the whole transaction rolls back.
        InterleavingCommandInterceptor interceptor = new(ConnectionString, "DELETE FROM group_folders",
            interleavedSql: $"INSERT INTO group_folders (group_id, name, max_rating, sort_order) VALUES ({groupId}, 'Favorites', 2, 9);");
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerGroupWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerGroupWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            (await FluentActions.Awaiting(() => svc.DeleteFolderAsync(parent))
                    .Should().ThrowAsync<GroupValidationException>("the race is answered like the check — never a raw 500"))
                .Which.Errors.Should().ContainSingle()
                .Which.Should().Be(GroupFolderValidations.DeleteWouldDuplicate("Fanfic", "Favorites"));
        }
        interceptor.Fired.Should().BeTrue();

        List<GroupFolder> folders = await FoldersAsync(groupId);
        folders.Should().Contain(f => f.GroupFolderId == parent, "the delete rolled back");
        GroupFolder stillChild = folders.Single(f => f.GroupFolderId == child);
        stillChild.ParentFolderId.Should().Be(parent);
        stillChild.SortOrder.Should().Be(4, "the sort-order move rolled back with the delete");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<int> CreateGroupAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IGroupWriteService>()
            .CreateGroupAsync(new CreateGroupDto { GroupName = $"Folders {Guid.NewGuid():N}"[..24] });
    }

    private async Task<int> CreateFolderAsync(int groupId, string name, int? parentId = null, int sortOrder = 0)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IGroupWriteService>().CreateFolderAsync(new CreateFolderDto
            { GroupId = groupId, Name = name, ParentFolderId = parentId, SortOrder = sortOrder });
    }

    private async Task RenameFolderAsync(int folderId, string name)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IGroupWriteService>().RenameFolderAsync(folderId, name);
    }

    private async Task DeleteFolderAsync(int folderId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IGroupWriteService>().DeleteFolderAsync(folderId);
    }

    private async Task AddStoryAsync(int groupId, int storyId, int? folderId = null)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IGroupWriteService>().AddStoryAsync(new AddGroupStoryDto
            { GroupId = groupId, StoryId = storyId, GroupFolderId = folderId });
    }

    private async Task<GroupDetailDto?> GetGroupAsync(int groupId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IGroupReadService>().GetByIdAsync(groupId);
    }

    private async Task<List<GroupFolder>> FoldersAsync(int groupId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().GroupFolders
            .AsNoTracking().Where(f => f.GroupId == groupId).ToListAsync();
    }

    private async Task<int> GroupStoryCountAsync(int groupId, int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().GroupStories
            .CountAsync(gs => gs.GroupId == groupId && gs.StoryId == storyId);
    }
}
