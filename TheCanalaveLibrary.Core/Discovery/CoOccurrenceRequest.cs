namespace TheCanalaveLibrary.Core;

/// <summary>
/// HTTP-boundary request shape for <see cref="ICoOccurrenceReadService"/>'s two reads (Feature 61).
/// The <see cref="ExcludedInteractions"/> array can't ride query-string binding alongside the
/// scalar params on a POST handler (<c>layer5-wasm.md</c> "Reads with non-scalar parameters"), so
/// both reads are POST, body-bound through this record. <c>ExcludedInteractions</c> null mirrors
/// the service contract's null default — resolve the viewer's §8.7 defaults server-side.
/// </summary>
public sealed record CoOccurrenceRequest(
    int StoryId,
    int Take,
    IReadOnlyList<UserStoryInteractionTypeEnum>? ExcludedInteractions);
