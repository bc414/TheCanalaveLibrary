namespace TheCanalaveLibrary.Core;

/// <summary>
/// One "this story is a {TypeName} of {TargetTitle}" link for the public story-page display
/// (Feature 10, WU42). Returned only for <see cref="StoryLineageStatus.Approved"/> rows where the
/// queried story is the <see cref="StoryLineage.SourceStoryId"/> — one-way display per spec §939
/// (absence of a reverse row means "don't show on the target"). <see cref="TargetTitle"/> is
/// resolved through an explicit join on <c>Story</c>, so a link is only ever returned when its
/// target still survives the viewer's <c>ContentRating</c>/<c>IsTakenDown</c> read filters (mirrors
/// <c>ServerSeriesReadService.GetMembershipsForStoryAsync</c>'s join-not-bare-projection rule).
/// </summary>
public record StoryLineageDto(
    short TypeId,
    string TypeName,
    int TargetStoryId,
    string TargetTitle);
