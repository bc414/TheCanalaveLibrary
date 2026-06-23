using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// In-memory stand-in for <see cref="IFollowingWriteService"/> used by <see cref="FollowButtonTests"/>
/// and <see cref="VouchButtonTests"/>. Records each call so tests can assert which methods were
/// invoked without needing a host or database.
/// </summary>
public class FakeFollowingWriteService : IFollowingWriteService
{
    public List<int> FollowCalls { get; } = [];
    public List<int> UnfollowCalls { get; } = [];
    public List<(int TargetUserId, bool ReceiveAlerts)> SetAlertsCalls { get; } = [];
    public List<(int TargetUserId, string? VouchText)> VouchCalls { get; } = [];
    public List<int> RemoveVouchCalls { get; } = [];

    public Task FollowAsync(int targetUserId) { FollowCalls.Add(targetUserId); return Task.CompletedTask; }
    public Task UnfollowAsync(int targetUserId) { UnfollowCalls.Add(targetUserId); return Task.CompletedTask; }
    public Task SetReceiveAlertsAsync(int targetUserId, bool receiveAlerts) { SetAlertsCalls.Add((targetUserId, receiveAlerts)); return Task.CompletedTask; }
    public Task VouchAsync(int targetUserId, string? vouchText) { VouchCalls.Add((targetUserId, vouchText)); return Task.CompletedTask; }
    public Task RemoveVouchAsync(int targetUserId) { RemoveVouchCalls.Add(targetUserId); return Task.CompletedTask; }

    // Read methods — return harmless defaults; not exercised by the component render tests.
    public Task<UserRelationshipStateDto> GetRelationshipStateAsync(int targetUserId) =>
        Task.FromResult(new UserRelationshipStateDto(false, false, false, 0));
    public Task<IReadOnlyList<UserCardDto>> GetFollowedUsersAsync(int userId) =>
        Task.FromResult<IReadOnlyList<UserCardDto>>([]);
    public Task<IReadOnlyList<VouchDisplayDto>> GetOutgoingVouchesAsync(int userId) =>
        Task.FromResult<IReadOnlyList<VouchDisplayDto>>([]);
    public Task<IReadOnlyList<VouchDisplayDto>> GetIncomingVouchesAsync() =>
        Task.FromResult<IReadOnlyList<VouchDisplayDto>>([]);
}
