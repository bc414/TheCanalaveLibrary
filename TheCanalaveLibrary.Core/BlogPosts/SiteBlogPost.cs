namespace TheCanalaveLibrary.Core;

/// <summary>
/// Staff-authored site announcement — TPT child of <see cref="BaseBlogPost"/>; maps to
/// <c>site_blog_posts</c>. The structural mirror of the <see cref="SitePoll"/> : <see cref="BasePoll"/>
/// split (WU-SiteNews, 2026-07-28): site-owned, not person-owned. No <c>StoryId</c> (staff
/// announcements aren't about a specific story) and no <c>HasSpoilers</c> (not meaningful for
/// staff content). Never appears on a profile's Blog tab — <see cref="ProfileBlogPost"/> and
/// <see cref="GroupBlogPost"/> own that surface.
/// </summary>
public class SiteBlogPost : BaseBlogPost
{
    public bool IsPublished { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime LastUpdatedDate { get; set; }

    /// <summary>
    /// Stays E/T in practice — the create/update DTOs don't expose a rating picker (site
    /// announcements aren't mature content) — kept only for shape parity with the shared
    /// discovery-column contract every <see cref="BaseBlogPost"/> child carries.
    /// </summary>
    public Rating Rating { get; set; }

    /// <summary>
    /// Author's choice, set at create/edit time: fan the <see cref="NotificationTypeEnum.SiteAnnouncement"/>
    /// notification out to every user when this post publishes. Not a per-recipient setting —
    /// see <see cref="NotifiedAtUtc"/> for the fire-once guard.
    /// </summary>
    public bool NotifyAllUsers { get; set; }

    /// <summary>
    /// Server-stamped the first time the <see cref="NotifyAllUsers"/> fan-out fires; null = not
    /// yet notified. Guards against re-notifying every user on a later edit — there is no
    /// "edit storm" quiet-period case here (unlike <c>BasePoll.LastEditedAt</c>/<c>EditNotifiedAt</c>):
    /// a single staff post either announces once or it doesn't.
    /// </summary>
    public DateTime? NotifiedAtUtc { get; set; }
}
