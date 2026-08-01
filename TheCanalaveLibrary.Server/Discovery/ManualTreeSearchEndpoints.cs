using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IManualTreeSearchReadService"/>. Thin pass-throughs — no
/// business logic here; the service itself is the single enforcement point for the privacy model
/// (hidden favorites and incoming vouches are excluded at the query level — see the interface's
/// doc comment). Public reads: Manual Tree Search pivots are a public relationship browser over
/// public story/user pages, so there is no auth gate — same treatment as
/// <see cref="ITagReadService"/>'s public reads.
/// </summary>
public static class ManualTreeSearchEndpoints
{
    public static WebApplication MapManualTreeSearchEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/manual-tree-search");

        // POST-for-complex-read (layer5-wasm.md §"Reads with non-scalar parameters"):
        // StoryNeighborsRequest/UserNeighborsRequest aren't GET-bindable. Sub-route mirrors the
        // method name since the interface has more than one such read.
        group.MapPost("/neighbors/story", async (
                IManualTreeSearchReadService manualTreeSearch, StoryNeighborsRequest request, CancellationToken ct) =>
            Results.Ok(await manualTreeSearch.GetStoryNeighborsAsync(request, ct)));

        // Wrapped in ExecuteAsync (WU-ExploreFilterAxes): the user pivot now carries an optional
        // StoryFilterDto, whose ValidateShipShape throws the user-facing StoryValidationException
        // for malformed ship criteria. Unwrapped, that malformed input would 500 instead of 400 —
        // exactly the defect WU-ErrorHandling2 fixed on the three story-listing reads. The story
        // pivot takes no filter and stays a plain pass-through.
        group.MapPost("/neighbors/user", (
                IManualTreeSearchReadService manualTreeSearch, UserNeighborsRequest request, CancellationToken ct) =>
            EndpointHelpers.ExecuteAsync(async () =>
                Results.Ok(await manualTreeSearch.GetUserNeighborsAsync(request, ct))));

        // Both parameters are plain int collections — layer5-wasm.md's explicit GET-bindable
        // exception (scalar/enum/int[]/IReadOnlyCollection<int>) — so this stays GET despite being
        // a batch rehydration call. Repeated-key query binding: ?storyIds=1&storyIds=2&userIds=3.
        group.MapGet("/node-displays", async (
                IManualTreeSearchReadService manualTreeSearch, int[] storyIds, int[] userIds, CancellationToken ct) =>
            Results.Ok(await manualTreeSearch.GetNodeDisplaysAsync(storyIds, userIds, ct)));

        return app;
    }
}
