namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of the Notifications feature cluster (Features 41, 42, 43). Inherits the read
/// interface so components/services that need both read and write can inject a single service.
///
/// <para><b>Generation API — semantic methods only.</b> The only public generation surface is
/// semantic per-event methods (<c>NotifyNew*Async</c>). There is no public generic
/// <c>CreateAsync</c>. All methods funnel through one private create-core in
/// <c>ServerNotificationWriteService</c> that owns the invariants: drop-self and dedup.
/// This keeps those invariants un-bypassable per-caller — the same principle as the
/// content-rating named query filter. See <c>cross-cutting.md</c> "Notification Creation"
/// and <c>layer2-services.md</c> "Notification Generation."</para>
///
/// <para><b>Best-effort post-commit.</b> Callers invoke these after their own
/// <c>SaveChangesAsync</c>, inside a <c>try/catch</c> that logs and swallows. A notification
/// failure must never roll back the caller's primary action.</para>
///
/// <para><b>Semantic methods land incrementally.</b> WU22 delivers the two single-recipient
/// methods whose source data is already at Stage 5 (Following). Fan-out methods
/// (<c>NotifyNewChapterAsync</c>, etc.) are added co-delivered with their triggering
/// work-units.</para>
/// </summary>
public interface INotificationWriteService : INotificationReadService
{
    // ── Read-side mutations ──────────────────────────────────────────────────────

    /// <summary>
    /// Marks a single notification as read. Silently no-ops if the notification
    /// does not belong to the current user or is already read.
    /// </summary>
    Task MarkAsReadAsync(long notificationId);

    /// <summary>
    /// Marks all unread notifications belonging to the current user as read.
    /// </summary>
    Task MarkAllAsReadAsync();

    // ── Settings ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the current user's preference override for <paramref name="notifType"/>.
    ///
    /// <para><b>Sparse model:</b> when both values match the type defaults, the override row
    /// is deleted (NULL = use default). Otherwise the row is upserted.</para>
    /// </summary>
    Task SetSettingAsync(NotificationTypeEnum notifType, bool emailEnabled, bool collapsed);

    // ── Semantic generation methods (WU22 slice) ──────────────────────────────────

    /// <summary>
    /// Creates a <c>NewFollowerOnYou</c> notification for <paramref name="recipientUserId"/>.
    /// Called by <c>ServerFollowingWriteService.FollowAsync</c> after its primary commit.
    /// </summary>
    Task NotifyNewFollowerAsync(int recipientUserId, int followerUserId);

    /// <summary>
    /// Creates a <c>NewVouchOnYou</c> notification for <paramref name="recipientUserId"/>.
    /// Called by <c>ServerFollowingWriteService.VouchAsync</c> after its primary commit.
    /// </summary>
    Task NotifyNewVouchAsync(int recipientUserId, int voucherUserId);

    // ── Semantic generation methods (WU29 slice) ──────────────────────────────────

    /// <summary>
    /// Creates a <c>HiddenGem</c> notification for the story author when a recommender designates
    /// their recommendation as a Hidden Gem. Called by
    /// <c>ServerRecommendationWriteService.SetHiddenGemAsync</c> after its primary commit.
    /// </summary>
    Task NotifyStoryHiddenGemAsync(int recipientStoryAuthorId, int sourceRecommenderId);

    // ── Semantic generation methods (WU-RecLifecycle slice) ──────────────────────

    /// <summary>
    /// Creates a <c>NewRecommendationOnYourStory</c> notification for the story author when a
    /// recommendation is submitted (recs publish immediately — WU-RecLifecycle). Called by
    /// <c>ServerRecommendationWriteService.SubmitAsync</c> after its primary commit, best-effort.
    /// </summary>
    Task NotifyNewRecommendationOnYourStoryAsync(int recipientStoryAuthorId, int sourceRecommenderId, int storyId);

    /// <summary>
    /// Creates a <c>RecommendationRevisionRequested</c> notification for the recommender when the
    /// story author sends their recommendation back for revision. The author's note travels on
    /// <c>Recommendation.RevisionRequestNote</c> (notifications carry no free text); this alerts
    /// and deep-links to the story. Called by <c>RequestRevisionAsync</c>, best-effort.
    /// </summary>
    Task NotifyRecommendationRevisionRequestedAsync(int recipientRecommenderId, int sourceStoryAuthorId, int storyId);

    /// <summary>
    /// Creates a <c>RecommendationRevised</c> notification for the story author when the
    /// recommender's edit returns a <c>NeedsRevision</c> recommendation to live (the recommender
    /// is not self-notified — their own edit caused it). Called by <c>EditAsync</c>, best-effort.
    /// </summary>
    Task NotifyRecommendationRevisedAsync(int recipientStoryAuthorId, int sourceRecommenderId, int storyId);

    /// <summary>
    /// Creates a <c>RecommendationApproved</c> notification for the recommender when the story
    /// author unblocks their removed recommendation (WU-RecLifecycle: Unblock is this type's only
    /// trigger). Called by <c>UnblockAsync</c>, best-effort.
    /// </summary>
    Task NotifyRecommendationApprovedAsync(int recipientRecommenderId, int sourceStoryAuthorId, int storyId);

    // ── Semantic generation methods (WU32 slice — group fan-out) ─────────────────

    /// <summary>
    /// Fan-out notification sent to all group members with <c>NotifyForNewStory = true</c>
    /// when a story is added to a group (type <c>NewGroupStory = 60</c>). Also sends
    /// <c>YourStoryAddedToGroup = 25</c> to the story author (drop-self handled by the
    /// create-core). Called by <c>ServerGroupWriteService.AddStoryAsync</c> after its primary
    /// commit, best-effort (try/catch wraps the call).
    /// </summary>
    /// <param name="groupId">The group the story was added to.</param>
    /// <param name="storyAuthorId">The author of the added story (receives <c>YourStoryAddedToGroup</c>).</param>
    /// <param name="sourceUserId">The member who performed the add (drop-self source).</param>
    Task NotifyNewGroupStoryAsync(int groupId, int storyAuthorId, int sourceUserId);

    /// <summary>
    /// Fan-out notification sent to all group members with <c>NotifyForNewBlogPost = true</c>
    /// when a group blog post is published (type <c>NewGroupBlogPost = 61</c>). Called by
    /// <c>ServerBlogPostWriteService.CreateGroupBlogPostAsync</c> after its primary commit.
    /// </summary>
    /// <param name="groupId">The group the blog post belongs to.</param>
    /// <param name="blogPostId">The new blog post's id (used as <c>RelatedEntityId</c>).</param>
    /// <param name="authorId">The author of the blog post (drop-self source).</param>
    Task NotifyNewGroupBlogPostAsync(int groupId, int blogPostId, int authorId);

    // ── Semantic generation methods (WU42 slice — Story Lineage) ─────────────────

    /// <summary>
    /// Sends <c>StoryLineageRequested = 50</c> to <paramref name="targetAuthorId"/> when another
    /// author requests a lineage link from their story to one of the target author's stories.
    /// <c>RelatedEntityId = sourceStoryId</c> (the requester's story, for the recipient to review).
    /// Not sent for self-owned (auto-approved) links.
    /// </summary>
    Task NotifyStoryLineageRequestedAsync(int targetAuthorId, int requesterId, int sourceStoryId);

    /// <summary>
    /// Sends <c>StoryLineageApproved = 51</c> to <paramref name="sourceAuthorId"/> when their
    /// pending lineage request is approved. <c>RelatedEntityId = targetStoryId</c> (the story that
    /// was approved as a lineage target).
    /// </summary>
    Task NotifyStoryLineageApprovedAsync(int sourceAuthorId, int approverId, int targetStoryId);

    // ── Semantic generation methods (WU34 slice — moderation) ────────────────────

    /// <summary>
    /// Sends <c>ReportReceived = 80</c> to <paramref name="reporterUserId"/> confirming receipt.
    /// <c>RelatedEntityId = 0</c> (no navigable target; the report id is not surfaced to reporters).
    /// </summary>
    Task NotifyReportReceivedAsync(int reporterUserId, int moderatorSourceId);

    /// <summary>
    /// Sends <c>ReportResolved = 81</c> (action taken) to <paramref name="reporterUserId"/>.
    /// <c>RelatedEntityId = 0</c>.
    /// </summary>
    Task NotifyReportResolvedAsync(int reporterUserId, int moderatorSourceId);

    /// <summary>
    /// Sends <c>ReportResolvedNoAction = 82</c> to <paramref name="reporterUserId"/>.
    /// <c>RelatedEntityId = 0</c>.
    /// </summary>
    Task NotifyReportResolvedNoActionAsync(int reporterUserId, int moderatorSourceId);

    /// <summary>
    /// Sends <c>ContentRemoved = 70</c> to the content author.
    /// <c>RelatedEntityId = 0</c> (polymorphic target; no single navigable entity kind).
    /// </summary>
    Task NotifyContentRemovedAsync(int contentAuthorUserId, int moderatorSourceId);

    /// <summary>
    /// Sends <c>StoryApproved = 75</c> to the story author.
    /// <c>RelatedEntityId = storyId</c> (navigates to the story page).
    /// </summary>
    Task NotifyStoryApprovedAsync(int storyAuthorUserId, int storyId, int moderatorSourceId);

    /// <summary>
    /// Sends <c>StoryRejected = 71</c> to the story author.
    /// <c>RelatedEntityId = storyId</c> (navigates to the story page).
    /// </summary>
    Task NotifyStoryRejectedAsync(int storyAuthorUserId, int storyId, int moderatorSourceId);

    /// <summary>
    /// Sends <c>ExternalAccountVerified = 76</c> to the user (Feature 53, WU39). <c>RelatedEntityId
    /// = 0</c> (no navigable entity — the account tier lives in Settings, a fixed route).
    /// </summary>
    Task NotifyExternalAccountVerifiedAsync(int userId, int moderatorSourceId);

    /// <summary>
    /// Sends <c>ExternalAccountRejected = 77</c> to the user (Feature 53, WU39) so they can fix
    /// and re-request. <c>RelatedEntityId = 0</c>.
    /// </summary>
    Task NotifyExternalAccountRejectedAsync(int userId, int moderatorSourceId);

    /// <summary>
    /// Sends <c>ExternalLinkVerified = 78</c> to the story's author (Feature 53, WU39).
    /// <c>RelatedEntityId = storyId</c> (navigates to the story page).
    /// </summary>
    Task NotifyExternalLinkVerifiedAsync(int storyAuthorUserId, int storyId, int moderatorSourceId);

    /// <summary>
    /// Sends <c>ExternalLinkRejected = 79</c> to the story's author (Feature 53, WU39) so they can
    /// fix and re-request. <c>RelatedEntityId = storyId</c>.
    /// </summary>
    Task NotifyExternalLinkRejectedAsync(int storyAuthorUserId, int storyId, int moderatorSourceId);

    /// <summary>
    /// Sends <c>AccountWarning = 72</c> to <paramref name="targetUserId"/>.
    /// <c>RelatedEntityId = 0</c>.
    /// </summary>
    Task NotifyAccountWarningAsync(int targetUserId, int moderatorSourceId);

    /// <summary>
    /// Sends <c>AccountSuspended = 73</c> to <paramref name="targetUserId"/>.
    /// <c>RelatedEntityId = 0</c>.
    /// </summary>
    Task NotifyAccountSuspendedAsync(int targetUserId, int moderatorSourceId);

    /// <summary>
    /// Sends <c>AccountBanned = 74</c> to <paramref name="targetUserId"/>.
    /// <c>RelatedEntityId = 0</c>.
    /// </summary>
    Task NotifyAccountBannedAsync(int targetUserId, int moderatorSourceId);

    // ── Semantic generation methods (WU-Spotlight slice) ─────────────────────────

    /// <summary>
    /// Sends <c>SpotlightSlotGranted = 90</c> to <paramref name="awardeeUserId"/>, inline at
    /// grant time (called by <c>ServerSpotlightSlotAllocator.GrantSlotAsync</c> after its primary
    /// commit). <c>RelatedEntityId = 0</c> (the redemption page is a fixed route, not an entity).
    /// </summary>
    Task NotifySpotlightSlotGrantedAsync(int awardeeUserId, int grantingModeratorId);

    /// <summary>
    /// Sends <c>StorySpotlighted = 91</c> to the story author <b>at go-live</b> (called by
    /// <c>SpotlightGoLiveWorker</c> when a placement's window opens, never at booking).
    /// <c>RelatedEntityId = storyId</c>. Drop-self covers a sponsor spotlighting… never their own
    /// story (service-enforced), so this always delivers.
    /// </summary>
    Task NotifyStorySpotlightedAsync(int storyAuthorUserId, int sponsorUserId, int storyId);

    /// <summary>
    /// Sends <c>RecommendationSpotlighted = 92</c> to the attached recommendation's recommender
    /// <b>at go-live</b>. <c>RelatedEntityId = storyId</c> (recommendations display on the story
    /// page). When the sponsor attached their own recommendation, the create-core's drop-self
    /// rule suppresses it — the standing self-generated-notification convention.
    /// </summary>
    Task NotifyRecommendationSpotlightedAsync(int recommenderUserId, int sponsorUserId, int storyId);

    // ── Semantic generation methods (WU-Polls slice) ─────────────────────────────

    /// <summary>
    /// Fan-out <c>PollUpdated = 100</c> to a poll's current voters after the 30-minute
    /// quiet-period edit batch (called by <c>PollEditNotificationSweeper</c>, never inline from
    /// the write service — edits burst). <paramref name="relatedEntityId"/> is the owning
    /// blog post's id for blog-post polls (navigates to the post) or 0 for site polls (no
    /// per-poll page; the notification is informational). Drop-self covers the owner having
    /// voted on their own poll.
    /// </summary>
    Task NotifyPollUpdatedAsync(int pollOwnerUserId, IReadOnlyList<int> voterUserIds, int relatedEntityId);

    // ── Semantic generation methods (WU-B2 slice — comments & profile blog posts) ─

    /// <summary>
    /// Sends <c>NewStoryComment = 24</c> to the story author when a chapter comment is posted.
    /// <c>RelatedEntityId = chapterId</c> (deep-links to the chapter the comment sits on).
    /// Called by <c>ServerCommentWriteService.PostChapterCommentAsync</c> after its primary commit.
    /// </summary>
    Task NotifyNewStoryCommentAsync(int storyAuthorId, int commenterId, int chapterId);

    /// <summary>
    /// Sends <c>NewCommentOnBlog = 33</c> to the blog post's author when a blog-post comment is
    /// posted (profile or group post — owner resolves via the TPT root).
    /// <c>RelatedEntityId = blogPostId</c> (navigates to <c>/blog/{id}</c>).
    /// Called by <c>ServerCommentWriteService.PostBlogPostCommentAsync</c> after its primary commit.
    /// </summary>
    Task NotifyNewBlogCommentAsync(int blogAuthorId, int commenterId, int blogPostId);

    /// <summary>
    /// Sends <c>NewCommentOnYourProfile = 31</c> to the profile owner when a profile-wall comment
    /// is posted. <c>RelatedEntityId = profileOwnerId</c> (the owner and the related entity are the
    /// same user — navigates to <c>/user/{id}</c>).
    /// Called by <c>ServerCommentWriteService.PostUserProfileCommentAsync</c> after its primary commit.
    /// </summary>
    Task NotifyNewProfileCommentAsync(int profileOwnerId, int commenterId);

    /// <summary>
    /// Sends <c>CommentReply = 34</c> to the parent comment's author when a reply is posted, in
    /// any comment context. <paramref name="contextEntityId"/> is the containing entity's id
    /// (chapterId / blogPostId / groupId / profileOwnerId) — <b>never the comment id</b>:
    /// <c>Notification.RelatedEntityId</c> is <c>int</c> while <c>CommentId</c> is <c>long</c>.
    /// Accepted dedup consequence: two unread replies from one user to the recipient's different
    /// comments in the same context collapse to one notification (matches the generic
    /// "replied to your comment" presenter text). The type is non-navigating
    /// (<c>KindFor → None</c> — one cross-context type cannot map to one table).
    /// </summary>
    Task NotifyCommentReplyAsync(int parentAuthorId, int commenterId, int contextEntityId);

    /// <summary>
    /// Fan-out fired when a profile blog post transitions to published
    /// (<c>IsPublished</c> false→true in <c>UpdateBlogPostAsync</c> — never on draft create).
    /// Resolves four recipient sets, made disjoint by precedence 13 &gt; 14 &gt; 15 &gt; 16
    /// (most-direct relationship wins; each user gets exactly one notification per publish):
    /// <list type="bullet">
    ///   <item><c>NewBlogPostByFollowedUser = 13</c> — author-followers with
    ///   <c>FollowedUser.ReceiveAlerts = true</c>.</item>
    ///   <item><c>NewBlogPostOnFollowedStory = 14</c> / <c>OnFavoritedStory = 15</c> /
    ///   <c>OnReadItLaterStory = 16</c> — when <paramref name="storyId"/> is non-null, users whose
    ///   <c>UserStoryInteraction</c> has the matching flag (no per-row opt-in exists — presence of
    ///   the flag is the signal; hidden favorites included, notifications are personal-plane).</item>
    /// </list>
    /// <c>RelatedEntityId = blogPostId</c> for all four types. Republish re-notifies (unread-dedup
    /// absorbs back-to-back duplicates) — intentional.
    /// </summary>
    Task NotifyNewProfileBlogPostAsync(int blogPostId, int authorId, int? storyId);
}
