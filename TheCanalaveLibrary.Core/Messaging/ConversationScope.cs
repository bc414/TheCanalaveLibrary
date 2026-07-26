namespace TheCanalaveLibrary.Core;

/// <summary>
/// Which slice of the viewer's conversations a listing read returns. Scopes are disjoint —
/// there is deliberately no "all" member: no UI surface merges the two lists (MessagesPage
/// renders them as separate tabs), and a merged mode would re-open the door to the
/// fetch-everything-and-filter-client-side shape the ID-first read path exists to prevent.
/// Add a third member only when a real consumer needs one.
/// </summary>
public enum ConversationScope
{
    /// <summary>Non-archived conversations — the inbox. The default everywhere.</summary>
    Active,

    /// <summary>
    /// Conversations the viewer has archived (their own participant-row flag; the other
    /// party's view is unaffected). Fetched on demand when the Archived tab opens — this is
    /// the set that grows without bound, so it never rides along on a default load.
    /// </summary>
    Archived,
}
