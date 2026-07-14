using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Recording fake for <see cref="ICustomListWriteService"/> (Feature 51, WU-CustomLists) — mirrors
/// <c>FakeSavedTagSelectionWriteService</c>'s shape: records the last call per method, returns
/// configurable canned data, and throws a configurable exception for validation-error test paths.
/// </summary>
internal sealed class FakeCustomListWriteService : ICustomListWriteService
{
    // ── Canned read data ──────────────────────────────────────────────────────
    public List<CustomListSummaryDto> MyLists { get; set; } = [];
    public CustomListDetailDto? Detail { get; set; }
    public IReadOnlyList<int> StoryIds { get; set; } = [];
    public List<CustomListSummaryDto> PublicLists { get; set; } = [];
    public List<CustomListMembershipDto> Memberships { get; set; } = [];

    // ── Recorded calls ────────────────────────────────────────────────────────
    public (string ListName, bool IsPublic)? LastCreateCall { get; private set; }
    public (int ListId, string NewName)? LastRenameCall { get; private set; }
    public (int ListId, bool IsPublic)? LastVisibilityCall { get; private set; }
    public int? LastDeletedListId { get; private set; }
    public (int ListId, int StoryId)? LastAddCall { get; private set; }
    public (int ListId, int StoryId)? LastRemoveCall { get; private set; }
    public int? LastClonedSourceId { get; private set; }
    public CustomListSortEnum? LastStoryIdsSort { get; private set; }
    public int NextId { get; set; } = 1;

    /// <summary>Throw this on the next CreateListAsync call, if set (validation-error test path).</summary>
    public Exception? ThrowOnCreate { get; set; }

    // ── Reads ─────────────────────────────────────────────────────────────────

    public Task<List<CustomListSummaryDto>> GetMyListsAsync() => Task.FromResult(MyLists);

    public Task<CustomListDetailDto?> GetListDetailAsync(int listId) => Task.FromResult(Detail);

    public Task<IReadOnlyList<int>> GetListStoryIdsAsync(int listId, CustomListSortEnum sort)
    {
        LastStoryIdsSort = sort;
        return Task.FromResult(StoryIds);
    }

    public Task<List<CustomListSummaryDto>> GetPublicListsByUserAsync(int userId) =>
        Task.FromResult(PublicLists);

    public Task<List<CustomListMembershipDto>> GetMyListMembershipsAsync(int storyId) =>
        Task.FromResult(Memberships);

    // ── Writes ────────────────────────────────────────────────────────────────

    public Task<int> CreateListAsync(string listName, bool isPublic)
    {
        if (ThrowOnCreate is not null) throw ThrowOnCreate;
        LastCreateCall = (listName, isPublic);
        return Task.FromResult(NextId);
    }

    public Task RenameListAsync(int listId, string newListName)
    {
        LastRenameCall = (listId, newListName);
        return Task.CompletedTask;
    }

    public Task SetListVisibilityAsync(int listId, bool isPublic)
    {
        LastVisibilityCall = (listId, isPublic);
        return Task.CompletedTask;
    }

    public Task DeleteListAsync(int listId)
    {
        LastDeletedListId = listId;
        return Task.CompletedTask;
    }

    public Task AddStoryAsync(int listId, int storyId)
    {
        LastAddCall = (listId, storyId);
        return Task.CompletedTask;
    }

    public Task RemoveStoryAsync(int listId, int storyId)
    {
        LastRemoveCall = (listId, storyId);
        return Task.CompletedTask;
    }

    public Task<int> CloneListAsync(int sourceListId)
    {
        LastClonedSourceId = sourceListId;
        return Task.FromResult(NextId);
    }
}
