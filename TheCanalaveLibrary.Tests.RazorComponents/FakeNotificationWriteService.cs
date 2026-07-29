using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// In-memory <see cref="INotificationWriteService"/> (and, via inheritance, read service) for
/// bUnit tests — the fakes-catalog entry H5 flagged as missing (WU-TagFanon closed it). Seed
/// <see cref="Notifications"/>/<see cref="UnreadCount"/>; mutation methods record into
/// <see cref="MarkedRead"/>/<see cref="SettingCalls"/>; the server-internal generation methods
/// are recording no-ops (they're never HTTP-reachable, mirroring ClientNotificationWriteService).
/// </summary>
public sealed class FakeNotificationWriteService : INotificationWriteService
{
    public List<NotificationDto> Notifications { get; } = [];
    public int UnreadCount { get; set; }
    public NotificationSettingDto[] Settings { get; set; } = [];

    public List<long> MarkedRead { get; } = [];
    public int MarkAllReadCalls { get; private set; }
    public List<(NotificationTypeEnum Type, bool Email, bool Collapsed)> SettingCalls { get; } = [];
    public List<(IReadOnlyList<int> Recipients, int TargetTagId, int ModeratorId)> TagAdoptionCalls { get; } = [];

    // ── Read side ─────────────────────────────────────────────────────────────
    public Task<int> GetUnreadCountAsync() => Task.FromResult(UnreadCount);
    public Task<int> GetTotalCountAsync() => Task.FromResult(Notifications.Count);
    public Task<NotificationDto[]> GetNotificationsAsync(
        int page, int pageSize, NotificationFeedOrder order = NotificationFeedOrder.NewestFirst) =>
        Task.FromResult(Notifications.Skip(Math.Max(0, page - 1) * pageSize).Take(pageSize).ToArray());
    public Task<NotificationSettingDto[]> GetSettingsAsync() => Task.FromResult(Settings);

    // ── User-facing writes ────────────────────────────────────────────────────
    public Task MarkAsReadAsync(long notificationId) { MarkedRead.Add(notificationId); return Task.CompletedTask; }
    public Task MarkAllAsReadAsync() { MarkAllReadCalls++; return Task.CompletedTask; }
    public Task SetSettingAsync(NotificationTypeEnum notifType, bool emailEnabled, bool collapsed)
    { SettingCalls.Add((notifType, emailEnabled, collapsed)); return Task.CompletedTask; }

    // ── Server-internal generation methods (recording no-ops) ─────────────────
    public Task NotifyNewFollowerAsync(int recipientUserId, int followerUserId) => Task.CompletedTask;
    public Task NotifyNewVouchAsync(int recipientUserId, int voucherUserId) => Task.CompletedTask;
    public Task NotifyStoryHiddenGemAsync(int recipientStoryAuthorId, int sourceRecommenderId) => Task.CompletedTask;
    public Task NotifyNewRecommendationOnYourStoryAsync(int recipientStoryAuthorId, int sourceRecommenderId, int storyId) => Task.CompletedTask;
    public Task NotifyRecommendationRevisionRequestedAsync(int recipientRecommenderId, int sourceStoryAuthorId, int storyId) => Task.CompletedTask;
    public Task NotifyRecommendationRevisedAsync(int recipientStoryAuthorId, int sourceRecommenderId, int storyId) => Task.CompletedTask;
    public Task NotifyRecommendationApprovedAsync(int recipientRecommenderId, int sourceStoryAuthorId, int storyId) => Task.CompletedTask;
    public Task NotifyNewGroupStoryAsync(int groupId, int storyAuthorId, int sourceUserId) => Task.CompletedTask;
    public Task NotifyNewGroupBlogPostAsync(int groupId, int blogPostId, int authorId) => Task.CompletedTask;
    public Task NotifyStoryLineageRequestedAsync(int targetAuthorId, int requesterId, int sourceStoryId) => Task.CompletedTask;
    public Task NotifyStoryLineageApprovedAsync(int sourceAuthorId, int approverId, int targetStoryId) => Task.CompletedTask;
    public Task NotifyReportReceivedAsync(int reporterUserId, int moderatorSourceId) => Task.CompletedTask;
    public Task NotifyReportResolvedAsync(int reporterUserId, int moderatorSourceId) => Task.CompletedTask;
    public Task NotifyReportResolvedNoActionAsync(int reporterUserId, int moderatorSourceId) => Task.CompletedTask;
    public Task NotifyContentRemovedAsync(int contentAuthorUserId, int moderatorSourceId) => Task.CompletedTask;
    public Task NotifyStoryApprovedAsync(int storyAuthorUserId, int storyId, int moderatorSourceId) => Task.CompletedTask;
    public Task NotifyStoryRejectedAsync(int storyAuthorUserId, int storyId, int moderatorSourceId) => Task.CompletedTask;
    public Task NotifyExternalAccountVerifiedAsync(int userId, int moderatorSourceId) => Task.CompletedTask;
    public Task NotifyExternalAccountRejectedAsync(int userId, int moderatorSourceId) => Task.CompletedTask;
    public Task NotifyExternalLinkVerifiedAsync(int storyAuthorUserId, int storyId, int moderatorSourceId) => Task.CompletedTask;
    public Task NotifyExternalLinkRejectedAsync(int storyAuthorUserId, int storyId, int moderatorSourceId) => Task.CompletedTask;
    public Task NotifyAccountWarningAsync(int targetUserId, int moderatorSourceId) => Task.CompletedTask;
    public Task NotifyAccountSuspendedAsync(int targetUserId, int moderatorSourceId) => Task.CompletedTask;
    public Task NotifyAccountBannedAsync(int targetUserId, int moderatorSourceId) => Task.CompletedTask;
    public Task NotifySpotlightSlotGrantedAsync(int awardeeUserId, int grantingModeratorId) => Task.CompletedTask;
    public Task NotifyStorySpotlightedAsync(int storyAuthorUserId, int sponsorUserId, int storyId) => Task.CompletedTask;
    public Task NotifyRecommendationSpotlightedAsync(int recommenderUserId, int sponsorUserId, int storyId) => Task.CompletedTask;
    public Task NotifyPollUpdatedAsync(int pollOwnerUserId, IReadOnlyList<int> voterUserIds, int relatedEntityId) => Task.CompletedTask;
    public Task NotifyNewStoryCommentAsync(int storyAuthorId, int commenterId, int chapterId) => Task.CompletedTask;
    public Task NotifyNewBlogCommentAsync(int blogAuthorId, int commenterId, int blogPostId) => Task.CompletedTask;
    public Task NotifyNewProfileCommentAsync(int profileOwnerId, int commenterId) => Task.CompletedTask;
    public Task NotifyCommentReplyAsync(int parentAuthorId, int commenterId, int contextEntityId) => Task.CompletedTask;
    public Task NotifyNewProfileBlogPostAsync(int blogPostId, int authorId, int? storyId) => Task.CompletedTask;
    public Task NotifyTagAdoptionSuggestedAsync(IReadOnlyList<int> recipientAuthorIds, int targetTagId, int moderatorSourceId)
    { TagAdoptionCalls.Add((recipientAuthorIds, targetTagId, moderatorSourceId)); return Task.CompletedTask; }
    public Task NotifyNewSiteAnnouncementAsync(int blogPostId, int authorId) => Task.CompletedTask;
}
