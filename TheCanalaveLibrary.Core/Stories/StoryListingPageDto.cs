namespace TheCanalaveLibrary.Core;

/// <summary>
/// Wire-shape wrapper for <c>GetRecentListingsAsync</c>'s paged result — exists only because
/// System.Text.Json can't round-trip a bare ValueTuple (no public properties, just fields). The
/// interface itself keeps the ergonomic <c>(StoryListingDto[] Items, int TotalCount)</c> tuple return
/// (spec §3.8 — ValueTuples acceptable for 2–3 property reads); this DTO is purely the Client HTTP
/// impl's JSON shape, not part of the service contract.
/// </summary>
public record StoryListingPageDto(StoryListingDto[] Items, int TotalCount);
