namespace TheCanalaveLibrary.Core;

/// <summary>
/// Site-wide poll — created and managed by moderators/admins only, inline on <c>/polls</c>.
/// (Home-page surfacing is an open intent — homepage-sections decision, middle_plan_v2 row 2.)
/// </summary>
public class SitePoll : BasePoll
{
    /// <summary>
    /// Display-only retirement: moves the poll from the active list to the <c>/polls</c> archive.
    /// Orthogonal to closed — a poll can be closed-but-shown or archived-but-still-open.
    /// </summary>
    public bool IsArchived { get; set; }
}
