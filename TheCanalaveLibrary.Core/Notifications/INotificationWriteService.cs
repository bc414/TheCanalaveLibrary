namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of the Notifications feature cluster (Features 41, 42, 43). Inherits the read
/// interface so components/services that need both read and write can inject a single service.
///
/// <para><b>Generation API — semantic methods only.</b> The only public generation surface is
/// semantic per-event methods (<c>Notify*Async</c>). There is no public generic
/// <c>CreateAsync</c>. All methods funnel through one private create-core in
/// <c>ServerNotificationWriteService</c> that owns the invariants: drop-self and dedup.
/// This keeps those invariants un-bypassable per-caller — the same principle as the
/// content-rating named query filter. See <c>layer2-services.md</c> §"Notification Generation".</para>
///
/// <para><b>Best-effort post-commit.</b> Callers invoke these after their own
/// <c>SaveChangesAsync</c>, inside a <c>try/catch</c> that logs and swallows. A notification
/// failure must never roll back the caller's primary action.</para>
///
/// <para><b>Actor-free methods (owner rulings D4/D5).</b> Methods with no source parameter create a
/// null-sourced row: no actor exists, or the actor (a moderator) must not be named to the recipient.
/// <b>No method in the moderation band 70–82, nor the tag-adoption invitation (26), may take a
/// moderator id parameter</b> — the type-level enforcement that stops a future call site from
/// shipping a moderator's identity in <c>NotificationDto</c>. A null source never drop-selfs, so a
/// caller must not route a self-caused moderator action through these (the guardrail:
/// <c>ApplyAccountActionToUserAsync</c> never sends 80/81).</para>
///
/// <para><b>Semantic methods land with their triggering work-units.</b> Types seeded but not yet
/// produced: tracker B24.</para>
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

    // ── Semantic generation methods (WU32 slice — group fan-out; D16) ────────────

    /// <summary>
    /// A story was added to a group. Sends <c>NewGroupStory = 60</c> to every member with
    /// <c>NotifyForNewStory = true</c> <b>except the story's author</b>, and
    /// <c>YourStoryAddedToGroup = 25</c> to the author when there is one — one event, one
    /// notification per person. Both carry <paramref name="groupStoryId"/>, the junction row's id
    /// (owner ruling D16: the single most specific entity of the event — the enricher resolves the
    /// group name and the story title from it). The adder is the source, so drop-self keeps the
    /// adder out of both. Called by <c>ServerGroupWriteService.AddStoryAsync</c> only when the story
    /// was actually added (never on the idempotent re-add path), best-effort post-commit.
    /// </summary>
    /// <param name="groupId">The group the story was added to (member resolution).</param>
    /// <param name="groupStoryId">The new <c>GroupStory</c> row's id (used as <c>RelatedEntityId</c>).</param>
    /// <param name="storyAuthorId">The story's author; <c>null</c> for an authorless story (members are still notified).</param>
    /// <param name="sourceUserId">The member who performed the add.</param>
    Task NotifyNewGroupStoryAsync(int groupId, int groupStoryId, int? storyAuthorId, int sourceUserId);

    /// <summary>
    /// Fan-out notification sent to all group members with <c>NotifyForNewBlogPost = true</c>
    /// when a group blog post is published (type <c>NewGroupBlogPost = 61</c>). Called by
    /// <c>ServerBlogPostWriteService.CreateGroupBlogPostAsync</c> after its primary commit.
    /// </summary>
    /// <param name="groupId">The group the blog post belongs to.</param>
    /// <param name="blogPostId">The new blog post's id (used as <c>RelatedEntityId</c>).</param>
    /// <param name="authorId">The author of the blog post (drop-self source).</param>
    Task NotifyNewGroupBlogPostAsync(int groupId, int blogPostId, int authorId);

    // ── Semantic generation methods (WU-InertFeatures — new-chapter fan-out) ─────

    /// <summary>
    /// Sends <c>NewChapterOnFollowedStory = 10</c> to every user whose interaction with
    /// <paramref name="storyId"/> has <c>IsFollowed</c> (no per-row opt-in; a hidden favorite does not
    /// enter — no private-follow flag exists, D17). No rating filter on recipients (Personal plane).
    /// <c>RelatedEntityId = chapterId</c>; the author is the source, so a self-follow is dropped.
    /// Called by <c>IChapterWriteService.SetPublishedAsync</c> only on the call that stamps the
    /// chapter's <c>FirstPublishedDate</c> (first publication only, permanently — D1/D2), and only
    /// while the story is publicly published (a default — <c>roadmap.md</c> row 17).
    /// </summary>
    Task NotifyNewChapterAsync(int storyId, int chapterId, int authorId);

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

    /// <summary>
    /// Sends <c>NewStoryAcknowledgement = 52</c> to <paramref name="acknowledgedUserId"/> when a
    /// story's author credits them (WU-StatBadgeProducers). <c>RelatedEntityId = storyId</c>.
    /// Not sent on self-credit (rejected outright by the write service before this would fire).
    /// </summary>
    Task NotifyStoryAcknowledgedAsync(int acknowledgedUserId, int authorId, int storyId);

    // ── Semantic generation methods (WU34 slice — moderation; actor-free, D4/D5) ──

    /// <summary>
    /// Sends <c>ReportReceived = 80</c> to <paramref name="reporterUserId"/> confirming receipt.
    /// Null-sourced (D4: at submission time no moderator exists — the old moderator-id parameter is
    /// what made the call pass the reporter as their own source, so drop-self deleted every receipt).
    /// <c>RelatedEntityId = reportId</c>, so two reports produce two receipts. Never sent on the
    /// moderator-initiated account-action path.
    /// </summary>
    Task NotifyReportReceivedAsync(int reporterUserId, long reportId);

    /// <summary>
    /// Sends <c>ReportResolved = 81</c> (action taken) to <paramref name="reporterUserId"/>.
    /// Null-sourced (D5). <c>RelatedEntityId = reportId</c>.
    /// </summary>
    Task NotifyReportResolvedAsync(int reporterUserId, long reportId);

    /// <summary>
    /// Sends <c>ReportResolvedNoAction = 82</c> to <paramref name="reporterUserId"/>.
    /// Null-sourced (D5). <c>RelatedEntityId = reportId</c>.
    /// </summary>
    Task NotifyReportResolvedNoActionAsync(int reporterUserId, long reportId);

    /// <summary>
    /// Sends <c>ContentRemoved = 70</c> to the content author. Null-sourced (D5).
    /// <c>RelatedEntityId = reportId</c> — the report row is this event's single anchor (a reported
    /// entity's own id would collapse story 5 with comment 5); the type stays non-navigating.
    /// </summary>
    Task NotifyContentRemovedAsync(int contentAuthorUserId, long reportId);

    /// <summary>
    /// Sends <c>StoryApproved = 75</c> to the story author. Null-sourced (D5 — good news too).
    /// <c>RelatedEntityId = storyId</c> (navigates to the story page).
    /// </summary>
    Task NotifyStoryApprovedAsync(int storyAuthorUserId, int storyId);

    /// <summary>
    /// Sends <c>StoryRejected = 71</c> to the story author. Null-sourced (D5).
    /// <c>RelatedEntityId = storyId</c> (navigates to the story page).
    /// </summary>
    Task NotifyStoryRejectedAsync(int storyAuthorUserId, int storyId);

    /// <summary>
    /// Sends <c>ExternalAccountVerified = 76</c> to the user (Feature 53, WU39). Null-sourced (D5);
    /// exempt from cross-existing dedup (D4 — no related entity). <c>RelatedEntityId = 0</c> (the
    /// account tier lives in Settings, a fixed route).
    /// </summary>
    Task NotifyExternalAccountVerifiedAsync(int userId);

    /// <summary>
    /// Sends <c>ExternalAccountRejected = 77</c> to the user (Feature 53, WU39) so they can fix
    /// and re-request. Null-sourced (D5); dedup-exempt (D4). <c>RelatedEntityId = 0</c>.
    /// </summary>
    Task NotifyExternalAccountRejectedAsync(int userId);

    /// <summary>
    /// Sends <c>ExternalLinkVerified = 78</c> to the story's author (Feature 53, WU39).
    /// Null-sourced (D5). <c>RelatedEntityId = storyId</c> (navigates to the story page).
    /// </summary>
    Task NotifyExternalLinkVerifiedAsync(int storyAuthorUserId, int storyId);

    /// <summary>
    /// Sends <c>ExternalLinkRejected = 79</c> to the story's author (Feature 53, WU39) so they can
    /// fix and re-request. Null-sourced (D5). <c>RelatedEntityId = storyId</c>.
    /// </summary>
    Task NotifyExternalLinkRejectedAsync(int storyAuthorUserId, int storyId);

    /// <summary>
    /// Sends <c>AccountWarning = 72</c> to <paramref name="targetUserId"/>. Null-sourced (D5);
    /// dedup-exempt (D4 — two warnings while the first is unread are two rows).
    /// <c>RelatedEntityId = 0</c>.
    /// </summary>
    Task NotifyAccountWarningAsync(int targetUserId);

    /// <summary>
    /// Sends <c>AccountSuspended = 73</c> to <paramref name="targetUserId"/>. Null-sourced (D5);
    /// dedup-exempt (D4). <c>RelatedEntityId = 0</c>.
    /// </summary>
    Task NotifyAccountSuspendedAsync(int targetUserId);

    /// <summary>
    /// Sends <c>AccountBanned = 74</c> to <paramref name="targetUserId"/>. Null-sourced (D5);
    /// dedup-exempt (D4). <c>RelatedEntityId = 0</c>.
    /// </summary>
    Task NotifyAccountBannedAsync(int targetUserId);

    // ── Semantic generation methods (WU-Spotlight slice) ─────────────────────────

    /// <summary>
    /// Sends <c>SpotlightSlotGranted = 90</c> to <paramref name="awardeeUserId"/>, inline at
    /// grant time (called by <c>ServerSpotlightSlotAllocator.GrantSlotAsync</c> after its primary
    /// commit). <c>RelatedEntityId = 0</c> (the redemption page is a fixed route, not an entity), so
    /// the type is exempt from cross-existing dedup (D4). Keeps its granting-moderator source: it sits
    /// outside D5's stated 70–82 band (unruled).
    /// </summary>
    Task NotifySpotlightSlotGrantedAsync(int awardeeUserId, int grantingModeratorId);

    /// <summary>
    /// Sends <c>StorySpotlighted = 91</c> to the story author <b>at go-live</b> (called by
    /// <c>SpotlightGoLiveWorker</c> when a placement's window opens, never at booking).
    /// <c>RelatedEntityId = storyId</c>. Drop-self covers a sponsor spotlighting their own story,
    /// which the service already refuses, so this always delivers.
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
    /// (chapterId / blogPostId / groupId / profileOwnerId) — current behavior, not a physical limit:
    /// <c>RelatedEntityId</c> is <c>long</c> since WU-InertFeatures, and re-pointing this type at the
    /// comment through the <c>BaseComments</c> TPT root is on D16's conformance list (tracker B23).
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
    ///   the flag is the signal). Type 15 goes to <c>IsFavorite || IsHiddenFavorite</c>: hidden
    ///   favorites included.</item>
    /// </list>
    /// <para><b>The plane, not the flag, decides (owner ruling D17).</b> A hidden favorite suppresses
    /// public-plane consequences only — what <em>other people</em> see — never a notification
    /// delivered to the favoriter alone.</para>
    /// <c>RelatedEntityId = blogPostId</c> for all four types. Republish re-notifies (unread-dedup
    /// absorbs back-to-back duplicates) — intentional.
    /// <para>A <c>Private</c> author's post notifies nobody: a profile post is as visible as its
    /// author's profile (Class A), and no recipient is the author. <c>UsersOnly</c> needs no check —
    /// every recipient is signed in.</para>
    /// </summary>
    Task NotifyNewProfileBlogPostAsync(int blogPostId, int authorId, int? storyId);

    /// <summary>
    /// Tag-adoption invitation (WU-TagFanon, <c>TagUpdateSuggestion = 26</c>): a moderator linked
    /// a custom-name group to an official tag; the affected authors are invited to adopt it.
    /// Null-sourced — the invitation is a moderation act, so the moderator is not named (D5's
    /// routed sub-edge, taken per the owner's recommendation).
    /// <c>RelatedEntityId = targetTagId</c> — the notification deep-links to
    /// <c>/tag-adoptions/{tagId}</c>. The never-twice-per-(author, tag) rule is enforced by the
    /// CALLER via <see cref="TagAdoptionState.DateNotified"/>, not by unread-dedup (an author who
    /// read and moved on must never be re-notified for the same tag).
    /// </summary>
    Task NotifyTagAdoptionSuggestedAsync(IReadOnlyList<int> recipientAuthorIds, int targetTagId);

    /// <summary>
    /// Site-wide announcement fan-out (WU-SiteNews, <c>SiteAnnouncement = 0</c> — seeded and
    /// tested since the notification catalogue's original build, unproduced until now). Every
    /// user on the site is a target (drop-self excludes the posting moderator/admin). Called only
    /// when a <c>SiteBlogPost</c> publishes with <c>NotifyAllUsers = true</c>, and only once per
    /// post — the caller (<c>ServerBlogPostWriteService.CreateSiteBlogPostAsync</c>/
    /// <c>UpdateSiteBlogPostAsync</c>) stamps <c>SiteBlogPost.NotifiedAtUtc</c> after a successful
    /// call so a later edit never re-fires it. <c>RelatedEntityId = blogPostId</c>.
    /// </summary>
    Task NotifyNewSiteAnnouncementAsync(int blogPostId, int authorId);
}
