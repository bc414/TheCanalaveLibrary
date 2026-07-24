namespace TheCanalaveLibrary.Core;

/// <summary>
/// The "exists but gated" half of a gated-existence read (WU-AccessGate; content-safety.md
/// §"The Three-Plane Access Model"). Returned by the per-feature gate reads
/// (<c>GetStoryGateAsync</c> / <c>GetGroupGateAsync</c> / <c>GetBlogPostGateAsync</c>) when a
/// detail read returned null because the viewer hasn't consented — as opposed to the item being
/// absent or taken down, which stays a true 404 (the gate reads keep the IsTakenDown filter
/// active). Carries EXACTLY the interstitial's metadata: title, author, rating — deliberately no
/// cover and no description; both can themselves be explicit and the viewer has not consented
/// (settled 2026-07-19).
/// </summary>
/// <param name="RevealTarget">What the consent action reveals — for a group blog post this is
/// the GROUP (one consent covers all group-owned content); for a story/chapter URL the story;
/// for a profile blog post the post itself.</param>
/// <param name="RevealTargetId">Id of the reveal target (may differ from the requested entity's
/// id — e.g. the group id for a group blog post URL).</param>
public sealed record GatedMetadataDto(
    RevealedEntityType RevealTarget,
    int RevealTargetId,
    string Title,
    int? AuthorId,
    string? AuthorName,
    Rating Rating);
