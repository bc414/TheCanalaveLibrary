namespace TheCanalaveLibrary.Core;

/// <summary>
/// Layer-5 transport envelope for <see cref="IContentImportService.Resplit"/> only
/// (layer5-wasm.md §"Streams and multipart" / §"The Contract Boundary" — "Layer 5 never changes
/// contracts"). The interface method itself stays the two-parameter shape
/// (<see cref="ImportParseResult"/> + <see cref="SplitStrategy"/>); this record exists solely
/// because a minimal-API JSON POST body can bind only one complex type, mirroring the
/// <c>SubmitReportRequest</c>/<c>TreeSearchListingRequest</c> combining-record precedent elsewhere
/// in Core.
/// </summary>
public record ResplitRequest(ImportParseResult Parsed, SplitStrategy Strategy);
