namespace TheCanalaveLibrary.Core;

/// <summary>
/// Draft-access authorization — a registered user granted permission to read/comment on a story
/// pre-publication. Sits beside <see cref="CoAuthor"/> in the permissions family, not the credit
/// family (see <c>Server/ReferenceSQL/CanalaveDBCreation.sql</c>'s original table grouping). This is
/// a different concept from <see cref="StoryAcknowledgment"/>'s Beta Reader role, which is public
/// *credit*, not access — a story can have a beta reader who is never publicly credited, and credit
/// a beta reader who never had draft access (e.g. helped before the story was posted here). Dormant:
/// no service reads or writes this entity, like <see cref="CoAuthor"/>.
/// </summary>
public partial class BetaReader
{
    public int StoryId { get; set; }

    public int BetaReaderUserId { get; set; }

    public DateTime DateAdded { get; set; }

    public virtual User BetaReaderUser { get; set; } = null!;

    public virtual Story Story { get; set; } = null!;
}
