using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="ITreeSearchReadService"/>. Thin pass-throughs — no business
/// logic here; both methods throw <see cref="ArgumentException"/> for a malformed request (handled
/// by the ASP.NET default unhandled-exception → 500 path, same as any other read-side
/// <see cref="ArgumentException"/> — see layer5-wasm.md's error-translation table, which only
/// applies to WRITE handlers wrapped in <c>EndpointHelpers.ExecuteWriteAsync</c>; a malformed
/// request DTO on a read is a client bug per that same doc). Public reads: Automatic Tree Search is
/// the public <c>/discover</c> traversal surface — no auth gate, same treatment as
/// <see cref="ITagReadService"/>'s public reads.
/// </summary>
public static class TreeSearchEndpoints
{
    public static WebApplication MapTreeSearchEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/tree-search");

        // POST-for-complex-read (layer5-wasm.md §"Reads with non-scalar parameters"):
        // TreeSearchRequest isn't GET-bindable. Sub-route mirrors the method name since the
        // interface has more than one such read.
        group.MapPost("/traverse", async (
                ITreeSearchReadService treeSearch, TreeSearchRequest request, CancellationToken ct) =>
            Results.Ok(await treeSearch.TraverseAsync(request, ct)));

        // SearchAsync's service signature takes TWO complex objects (TreeSearchRequest,
        // StoryFilterDto) — minimal APIs only bind one complex parameter from the body, so the pair
        // is wrapped into TreeSearchListingRequest at the HTTP boundary only (see that record's doc
        // comment in TreeSearchListingResultDto.cs).
        group.MapPost("/search", async (
                ITreeSearchReadService treeSearch, TreeSearchListingRequest body, CancellationToken ct) =>
            Results.Ok(await treeSearch.SearchAsync(body.Request, body.Filter, ct)));

        return app;
    }
}
