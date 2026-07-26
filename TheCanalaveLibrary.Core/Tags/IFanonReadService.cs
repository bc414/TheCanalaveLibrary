namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of the fanonization pipeline (WU-TagFanon Groups 6+8): the public /fanon dashboard
/// (ranked cross-author custom-name groups, one axis per tag type), the hub's established fanon
/// tags, and the author-facing adoption pages. Grouping is by (case-insensitive trimmed
/// CustomName, base tag); rows with no CustomName never group. The minimum-reach threshold
/// (distinct authors, <see cref="SiteSettingKeys.FanonMinAuthorReach"/>) applies identically to
/// every viewer — a group not significant enough for the community is not significant for
/// moderators either. No moderator dismiss, no per-row exclusion, no undo.
/// </summary>
public interface IFanonReadService
{
    /// <summary>
    /// Ranked groups for one axis (Character = StoryCharacter custom names; any other type =
    /// StoryTag custom names of that type), ordered by author reach then story reach. Reach
    /// counts draw on visible stories only (published, not taken down) but are rating-complete
    /// for every viewer. <paramref name="search"/> filters by name substring.
    /// </summary>
    Task<IReadOnlyList<FanonGroupDto>> GetGroupsAsync(TagTypeEnum axis, string? search, int page, int pageSize);

    /// <summary>Total group count for the axis/search (pagination).</summary>
    Task<int> GetGroupCountAsync(TagTypeEnum axis, string? search);

    /// <summary>
    /// The expanded story list behind a group — viewer-consent-filtered rows plus the complete
    /// count for the count-line disclosure (content-safety.md person-scoped listing pattern).
    /// </summary>
    Task<FanonGroupStoriesDto> GetGroupStoriesAsync(TagTypeEnum axis, int baseTagId, string name);

    /// <summary>Established fanon tags (IsFanon) with story reach, for the /fanon hub.</summary>
    Task<IReadOnlyList<FanonTagDto>> GetEstablishedFanonTagsAsync();

    /// <summary>
    /// The authenticated author's affected rows for one target tag (the notification's landing
    /// page). Null when the tag doesn't exist or no fanon link targets it.
    /// </summary>
    Task<TagAdoptionPageDto?> GetMyAdoptionPageAsync(int targetTagId);

    /// <summary>
    /// The authenticated author's standing index (/tag-adoptions): every linked tag they hold
    /// matching rows or adoption state for. Required as a stable route — read notifications are
    /// deleted after 60 days, so the notification cannot be the only way in.
    /// </summary>
    Task<IReadOnlyList<MyTagAdoptionSummaryDto>> GetMyAdoptionIndexAsync();

    /// <summary>
    /// The editor nudge (Group 7.6): resolves a would-be custom name against official tags of
    /// the axis type under the same normalization as the dashboard grouping. Null = no match.
    /// </summary>
    Task<TagChipDto?> FindOfficialTagByNameAsync(TagTypeEnum axis, string name);
}
