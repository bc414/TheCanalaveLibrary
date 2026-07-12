using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// TPT root for site-wide and blog-post polls (Feature 37; requirements settled 2026-07-12 —
/// see <c>audit/BlogPosts.md</c> Feature 37).
/// <para>
/// <b>Lifecycle:</b> <see cref="DateOpened"/> may be in the future (scheduled open — not votable
/// until then). <see cref="DateClosed"/> is nullable: null = indefinite, open until the owner
/// manually closes (manual close stamps <c>DateClosed = now</c>; there is no separate flag).
/// Votes are changeable/retractable until closed.
/// </para>
/// <para>
/// <b>Config lock:</b> <see cref="AllowMultiple"/>, <see cref="ResultsVisibility"/>, and
/// <see cref="AnonymityMode"/> are owner-set at creation and freeze once any vote exists
/// (service-enforced) — prevents retroactive anonymity exposure and multi→single vote
/// invalidation. Name/description/options stay editable while open.
/// </para>
/// <para>
/// <b>Edit notification:</b> material edits to an open, voted-on poll stamp
/// <see cref="LastEditedAt"/>; <c>PollEditNotificationWorker</c> notifies prior voters after a
/// 30-minute quiet period (one notification per edit burst), stamping <see cref="EditNotifiedAt"/>.
/// </para>
/// </summary>
public abstract class BasePoll
{
    [Key]
    public int PollId { get; set; }

    public int OwnerId { get; set; }

    [Required] [MaxLength(256)] public string PollName { get; set; } = null!;

    [MaxLength(2048)]
    public string? Description { get; set; }

    public DateTime DateOpened { get; set; }

    /// <summary>Null = no scheduled end; poll stays open until manually closed.</summary>
    public DateTime? DateClosed { get; set; }

    /// <summary>True = voters may select multiple options (checkbox); false = single choice (radio).</summary>
    public bool AllowMultiple { get; set; }

    public PollResultsVisibility ResultsVisibility { get; set; }

    public PollAnonymityMode AnonymityMode { get; set; }

    /// <summary>Stamped on every material edit (name/description/option change) while votes exist.</summary>
    public DateTime? LastEditedAt { get; set; }

    /// <summary>When the edit-notification sweep last notified voters. Null = never notified.</summary>
    public DateTime? EditNotifiedAt { get; set; }

    public User Owner { get; set; } = null!;

    public ICollection<PollOption> PollOptions { get; set; } = new List<PollOption>();
}
