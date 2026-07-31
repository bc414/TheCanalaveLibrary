using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="ITreeSearchReadService"/>. Thin pass-throughs — no business
/// logic here; both methods validate the request and throw <see cref="ArgumentException"/> for a
/// malformed one (<c>ServerTreeSearchReadService.Validate</c>), so both wrap in the shared
/// <see cref="EndpointHelpers.ExecuteAsync"/> — the general read/write mapping, not just a write
/// rule (layer5-wasm.md §"The Error-Translation Contract"; WU-ErrorHandling2, 2026-07-30). Public
/// reads: Automatic Tree Search is the public <c>/discover</c> traversal surface — no auth gate,
/// same treatment as <see cref="ITagReadService"/>'s public reads.
/// </summary>
public static class TreeSearchEndpoints
{
    public static WebApplication MapTreeSearchEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/tree-search");

        // POST-for-complex-read (layer5-wasm.md §"Reads with non-scalar parameters"):
        // TreeSearchRequest isn't GET-bindable. Sub-route mirrors the method name since the
        // interface has more than one such read.
        group.MapPost("/traverse", (
                ITreeSearchReadService treeSearch, TreeSearchRequest request, CancellationToken ct) =>
            EndpointHelpers.ExecuteAsync(async () =>
                Results.Ok(await treeSearch.TraverseAsync(request, ct))));

        // SearchAsync's service signature takes TWO complex objects (TreeSearchRequest,
        // StoryFilterDto) — minimal APIs only bind one complex parameter from the body, so the pair
        // is wrapped into TreeSearchListingRequest at the HTTP boundary only (see that record's doc
        // comment in TreeSearchListingResultDto.cs).
        group.MapPost("/search", (
                ITreeSearchReadService treeSearch, TreeSearchListingRequest body, CancellationToken ct) =>
            EndpointHelpers.ExecuteAsync(async () =>
                Results.Ok(await treeSearch.SearchAsync(body.Request, body.Filter, ct))));

        return app;
    }
}
