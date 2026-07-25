namespace TheCanalaveLibrary.Core;

/// <summary>
/// A story's presence in a group (or a specific folder within one) — the join-row reference every
/// write action operating on a <see cref="GroupStory"/> needs (<c>AssignStoryToFolderAsync</c>,
/// <c>UnassignStoryFromFolderAsync</c>, <c>RemoveStoryAsync</c> are all keyed by
/// <see cref="GroupStoryId"/>, not <see cref="StoryId"/>). Carried by both
/// <see cref="GroupDetailDto.Stories"/> (every story in the group) and
/// <see cref="GroupFolderDto.Stories"/> (scoped to that folder) — the single source of truth for
/// story↔folder membership; there is no separate/parallel read for it (WU-GroupsL5b, 2026-07-24).
/// </summary>
public record GroupStoryDto(int GroupStoryId, int StoryId);
