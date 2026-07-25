using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="GroupFolderManagementPage"/> (F39 L3.5, WU-GroupsL5, 2026-07-24) —
/// the deferred group folder-management page, the only UI consumer of
/// <see cref="IGroupWriteService"/>'s four folder-write methods
/// (<c>CreateFolderAsync</c>/<c>RenameFolderAsync</c>/<c>DeleteFolderAsync</c>/<c>ReorderFolderAsync</c>).
/// Pins: the admin gate, folder-write dispatch shapes (incl. nested creation), the two-step delete
/// guard, and the reorder value-swap + boundary-disabled state. Semantic output and service-call
/// correctness only — no CSS class assertions (testing.md §"What belongs in RazorComponents").
/// Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class GroupFolderManagementPageTests : BunitContext
{
    private const int TestGroupId = 7;
    private readonly FakeGroupWriteService _fakeService = new();

    public GroupFolderManagementPageTests()
    {
        Services.AddScoped<IGroupWriteService>(_ => _fakeService);

        // AuthorizeView needs bUnit's authorization test double, not a bare cascaded
        // AuthenticationState (SeriesCreateEditPageTests, AccountStatusBannerTests — same pattern).
        // The page never resolves its own user id (see the page's "no AuthState cascade" note), so
        // no claims are needed — admin/non-admin is driven entirely by FakeGroupWriteService.Role.
        this.AddAuthorization().SetAuthorized("some-user");
    }

    private static GroupDetailDto NewGroupDetail(IReadOnlyList<GroupFolderDto> folderTree) => new(
        TestGroupId, "Test Group", "A description.", GroupAudienceType.Standard, Rating.M,
        CreatorId: 1, CreatorDisplayName: "Creator", MemberCount: 3,
        DateCreated: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        CurrentUserRole: GroupRole.Admin, FolderTree: folderTree, Stories: []);

    private static GroupFolderDto NewFolder(int id, string name, int sortOrder, Rating rating = Rating.M,
        int? parentId = null, IReadOnlyList<GroupFolderDto>? children = null) =>
        new(id, TestGroupId, parentId, name, rating, sortOrder, Stories: [], children ?? []);

    // AngleSharp compound-selector fragility (testing.md) — button text isn't a CSS selector, so
    // locate by exact TextContent like ConfirmDialogTests does, rather than a brittle :contains().
    private static IElement FindButton(IRenderedComponent<GroupFolderManagementPage> cut, string text) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == text);

    // ── Admin gate ───────────────────────────────────────────────────────────────

    [Fact]
    public void Admin_SeesFolderTreeAndCreateForm()
    {
        _fakeService.Role = GroupRole.Admin;
        _fakeService.GroupDetail = NewGroupDetail([
            NewFolder(1, "Alpha", 0),
            NewFolder(2, "Beta", 1),
            NewFolder(3, "Gamma", 2),
        ]);

        IRenderedComponent<GroupFolderManagementPage> cut = Render<GroupFolderManagementPage>(p => p
            .Add(c => c.GroupId, TestGroupId));

        cut.FindAll("li").Should().HaveCount(3, "one row per top-level folder");
        cut.Find("#new-folder-name").Should().NotBeNull("admins get the create form");
        cut.Markup.Should().NotContain("must be an admin");
    }

    [Theory]
    [InlineData(GroupRole.Member)]
    [InlineData(null)]
    public void NonAdmin_IsGated_NoCreateFormOrTree(GroupRole? role)
    {
        _fakeService.Role = role;
        _fakeService.GroupDetail = NewGroupDetail([NewFolder(1, "Alpha", 0)]);

        IRenderedComponent<GroupFolderManagementPage> cut = Render<GroupFolderManagementPage>(p => p
            .Add(c => c.GroupId, TestGroupId));

        cut.Markup.Should().Contain("must be an admin",
            "a non-admin (member or non-member) must see the forbidden message");
        cut.FindAll("#new-folder-name").Should().BeEmpty("non-admins never see the create form");
        cut.FindAll("li").Should().BeEmpty("non-admins never see the folder tree");
    }

    // ── Create ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_RootFolder_DispatchesCorrectDto()
    {
        _fakeService.Role = GroupRole.Admin;
        _fakeService.GroupDetail = NewGroupDetail([NewFolder(1, "Existing", 0)]);

        IRenderedComponent<GroupFolderManagementPage> cut = Render<GroupFolderManagementPage>(p => p
            .Add(c => c.GroupId, TestGroupId));

        cut.Find("#new-folder-name").Input("New Folder");
        cut.Find("#new-folder-rating").Change("T");
        FindButton(cut, "Create Folder").Click();

        cut.WaitForState(() => _fakeService.CreateFolderCalls.Count > 0, TimeSpan.FromSeconds(2));

        CreateFolderDto dto = _fakeService.CreateFolderCalls.Single();
        dto.GroupId.Should().Be(TestGroupId);
        dto.Name.Should().Be("New Folder");
        dto.MaxRating.Should().Be(Rating.T);
        dto.ParentFolderId.Should().BeNull("no parent selected ⇒ root folder");
        dto.SortOrder.Should().Be(1, "one existing root sibling ⇒ new folder appends at index 1");
    }

    [Fact]
    public async Task Create_WithParentSelected_DispatchesNestedDto()
    {
        _fakeService.Role = GroupRole.Admin;
        _fakeService.GroupDetail = NewGroupDetail([NewFolder(1, "Parent", 0)]);

        IRenderedComponent<GroupFolderManagementPage> cut = Render<GroupFolderManagementPage>(p => p
            .Add(c => c.GroupId, TestGroupId));

        cut.Find("#new-folder-name").Input("Child");
        cut.Find("#new-folder-parent").Change("1");
        FindButton(cut, "Create Folder").Click();

        cut.WaitForState(() => _fakeService.CreateFolderCalls.Count > 0, TimeSpan.FromSeconds(2));

        CreateFolderDto dto = _fakeService.CreateFolderCalls.Single();
        dto.ParentFolderId.Should().Be(1, "the selected parent must be carried through");
        dto.SortOrder.Should().Be(0, "the chosen parent has no children yet");
    }

    [Fact]
    public async Task Create_ValidationFailure_SurfacesViaInlineAlert()
    {
        _fakeService.Role = GroupRole.Admin;
        _fakeService.GroupDetail = NewGroupDetail([]);
        _fakeService.CreateFolderException = new GroupValidationException(["Name taken"]);

        IRenderedComponent<GroupFolderManagementPage> cut = Render<GroupFolderManagementPage>(p => p
            .Add(c => c.GroupId, TestGroupId));

        cut.Find("#new-folder-name").Input("Dup");
        FindButton(cut, "Create Folder").Click();

        cut.WaitForState(() => cut.Markup.Contains("Name taken"), TimeSpan.FromSeconds(2));

        cut.Find("[role='alert']").TextContent.Should().Contain("Name taken");
    }

    // ── Rename ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Rename_DispatchesCorrectIdAndName()
    {
        _fakeService.Role = GroupRole.Admin;
        _fakeService.GroupDetail = NewGroupDetail([NewFolder(1, "Old Name", 0)]);

        IRenderedComponent<GroupFolderManagementPage> cut = Render<GroupFolderManagementPage>(p => p
            .Add(c => c.GroupId, TestGroupId));

        FindButton(cut, "Rename").Click();
        IElement renameInput = cut.Find("input[aria-label='Rename Old Name']");
        renameInput.Input("New Name");
        FindButton(cut, "Save").Click();

        cut.WaitForState(() => _fakeService.RenameFolderCalls.Count > 0, TimeSpan.FromSeconds(2));

        (int folderId, string newName) = _fakeService.RenameFolderCalls.Single();
        folderId.Should().Be(1);
        newName.Should().Be("New Name");
    }

    // ── Delete (two-step) ────────────────────────────────────────────────────────

    [Fact]
    public void DeleteTrigger_OpensConfirmDialog_WithoutCallingService()
    {
        _fakeService.Role = GroupRole.Admin;
        _fakeService.GroupDetail = NewGroupDetail([NewFolder(1, "Doomed", 0)]);

        IRenderedComponent<GroupFolderManagementPage> cut = Render<GroupFolderManagementPage>(p => p
            .Add(c => c.GroupId, TestGroupId));

        FindButton(cut, "Delete").Click();

        _fakeService.DeleteFolderCalls.Should().BeEmpty(
            "the trash button must only open the confirm dialog, never delete directly");
        cut.Markup.Should().Contain("Delete folder?", "the confirm dialog must be open");
        cut.Markup.Should().Contain("Doomed", "the dialog names the folder being deleted");
    }

    [Fact]
    public async Task DeleteConfirmed_CallsDeleteFolderAsync()
    {
        _fakeService.Role = GroupRole.Admin;
        _fakeService.GroupDetail = NewGroupDetail([NewFolder(1, "Doomed", 0)]);

        IRenderedComponent<GroupFolderManagementPage> cut = Render<GroupFolderManagementPage>(p => p
            .Add(c => c.GroupId, TestGroupId));

        FindButton(cut, "Delete").Click();
        FindButton(cut, "Delete folder").Click();

        cut.WaitForState(() => _fakeService.DeleteFolderCalls.Count > 0, TimeSpan.FromSeconds(2));

        _fakeService.DeleteFolderCalls.Should().ContainSingle().Which.Should().Be(1);
    }

    // ── Reorder ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MoveDown_SwapsSortOrderWithNextSibling()
    {
        _fakeService.Role = GroupRole.Admin;
        _fakeService.GroupDetail = NewGroupDetail([
            NewFolder(1, "First", 0),
            NewFolder(2, "Second", 5), // non-contiguous SortOrder on purpose
        ]);

        IRenderedComponent<GroupFolderManagementPage> cut = Render<GroupFolderManagementPage>(p => p
            .Add(c => c.GroupId, TestGroupId));

        cut.Find("[aria-label='Move First down']").Click();

        cut.WaitForState(() => _fakeService.ReorderFolderCalls.Count >= 2, TimeSpan.FromSeconds(2));

        _fakeService.ReorderFolderCalls.Should().ContainInOrder(
            (1, 5), // First takes Second's old SortOrder
            (2, 0)); // Second takes First's old SortOrder
    }

    [Fact]
    public void BoundarySiblings_HaveCorrectMoveButtonsDisabled()
    {
        _fakeService.Role = GroupRole.Admin;
        _fakeService.GroupDetail = NewGroupDetail([
            NewFolder(1, "First", 0),
            NewFolder(2, "Second", 1),
        ]);

        IRenderedComponent<GroupFolderManagementPage> cut = Render<GroupFolderManagementPage>(p => p
            .Add(c => c.GroupId, TestGroupId));

        cut.Find("[aria-label='Move First up']").HasAttribute("disabled").Should().BeTrue(
            "the first sibling cannot move further up");
        cut.Find("[aria-label='Move First down']").HasAttribute("disabled").Should().BeFalse();
        cut.Find("[aria-label='Move Second up']").HasAttribute("disabled").Should().BeFalse();
        cut.Find("[aria-label='Move Second down']").HasAttribute("disabled").Should().BeTrue(
            "the last sibling cannot move further down");
    }
}

// ── Fakes ────────────────────────────────────────────────────────────────────────────────────

internal sealed class FakeGroupWriteService : IGroupWriteService
{
    public GroupDetailDto? GroupDetail { get; set; }
    public GroupRole? Role { get; set; }
    public Exception? CreateFolderException { get; set; }

    public List<CreateFolderDto> CreateFolderCalls { get; } = [];
    public List<(int FolderId, string NewName)> RenameFolderCalls { get; } = [];
    public List<int> DeleteFolderCalls { get; } = [];
    public List<(int FolderId, int NewSortOrder)> ReorderFolderCalls { get; } = [];
    public List<int> RemoveStoryCalls { get; } = [];
    public List<(int GroupStoryId, int GroupFolderId)> AssignStoryToFolderCalls { get; } = [];
    public List<(int GroupStoryId, int GroupFolderId)> UnassignStoryFromFolderCalls { get; } = [];

    // ── Reads (stubbed — not exercised by this page beyond GetByIdAsync/GetCurrentUserRoleAsync) ──

    public Task<(GroupCardDto[] Items, int TotalCount)> GetListingsAsync(int page, int pageSize) =>
        Task.FromResult((Array.Empty<GroupCardDto>(), 0));

    public Task<GroupDetailDto?> GetByIdAsync(int groupId) => Task.FromResult(GroupDetail);

    public Task<GatedMetadataDto?> GetGroupGateAsync(int groupId) => Task.FromResult<GatedMetadataDto?>(null);

    public Task<GroupRole?> GetCurrentUserRoleAsync(int groupId) => Task.FromResult(Role);

    public Task<(GroupMemberDto[] Members, int TotalCount)> GetMembersAsync(int groupId, int page, int pageSize) =>
        Task.FromResult((Array.Empty<GroupMemberDto>(), 0));

    // ── Writes not exercised by this page ─────────────────────────────────────────

    public Task<int> CreateGroupAsync(CreateGroupDto dto) => Task.FromResult(0);
    public Task UpdateGroupAsync(UpdateGroupDto dto) => Task.CompletedTask;
    public Task JoinAsync(int groupId) => Task.CompletedTask;
    public Task LeaveAsync(int groupId) => Task.CompletedTask;
    public Task AddStoryAsync(AddGroupStoryDto dto) => Task.CompletedTask;

    public Task RemoveStoryAsync(int groupStoryId)
    {
        RemoveStoryCalls.Add(groupStoryId);
        return Task.CompletedTask;
    }

    public Task AssignStoryToFolderAsync(int groupStoryId, int groupFolderId)
    {
        AssignStoryToFolderCalls.Add((groupStoryId, groupFolderId));
        return Task.CompletedTask;
    }

    public Task UnassignStoryFromFolderAsync(int groupStoryId, int groupFolderId)
    {
        UnassignStoryFromFolderCalls.Add((groupStoryId, groupFolderId));
        return Task.CompletedTask;
    }

    // ── Folder writes (what this page actually exercises) ────────────────────────

    public Task<int> CreateFolderAsync(CreateFolderDto dto)
    {
        if (CreateFolderException is not null) throw CreateFolderException;
        CreateFolderCalls.Add(dto);
        return Task.FromResult(99);
    }

    public Task RenameFolderAsync(int groupFolderId, string newName)
    {
        RenameFolderCalls.Add((groupFolderId, newName));
        return Task.CompletedTask;
    }

    public Task DeleteFolderAsync(int groupFolderId)
    {
        DeleteFolderCalls.Add(groupFolderId);
        return Task.CompletedTask;
    }

    public Task ReorderFolderAsync(int groupFolderId, int newSortOrder)
    {
        ReorderFolderCalls.Add((groupFolderId, newSortOrder));
        return Task.CompletedTask;
    }
}
