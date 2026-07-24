using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IStoryReadService"/> / <see cref="IStoryWriteService"/>.
/// Thin pass-throughs: no business logic here — validation and the author-only gate live in the
/// service (single enforcement point). Every write handler wraps in the shared
/// <see cref="EndpointHelpers.ExecuteWriteAsync"/> for exception→status translation
/// (layer5-wasm.md §"The Error-Translation Contract").
/// <para>
/// Read auth: public for every browse/listing/detail read — mirrors the public
/// <c>StoryPage</c> (<c>/story/{StoryId:int}/{*StorySlug}</c>, no <c>[Authorize]</c> — "stories are
/// publicly visible") and the public discovery surfaces that consume
/// <see cref="IStoryReadService.GetListingsAsync"/>/<see cref="IStoryReadService.GetRandomBatchAsync"/>/
/// <see cref="IStoryReadService.FilterCandidateIdsAsync"/>. This includes
/// <see cref="IStoryReadService.GetStoryIdsByAuthorAsync"/> (anonymous-callable since
/// WU-AccessGate Phase 1 — it feeds the public profile Stories tab; the service keeps the
/// ContentRating bypass owner-only and enforces the author's ProfileVisibility). Two exceptions:
/// <see cref="IStoryReadService.GetStoryForEditAsync"/>
/// feeds only <c>StoryEditorPage</c> (<c>@attribute [Authorize]</c>), so it is gated to mirror that
/// page. <see cref="IStoryReadService.SearchStoriesByTitleAsync"/> is gated too — genuinely unsure:
/// its only consumers today (the Story Lineage target picker, the Groups add-story retrofit) are
/// both authenticated-only flows, but the read itself isn't inherently sensitive (see comment at its
/// mapping below).
/// </para>
/// <para>
/// Write auth: <c>RequireAuthorization()</c> on every write — every
/// <see cref="IStoryWriteService"/> method requires an authenticated user, and
/// update/cover-upload additionally enforce story ownership via
/// <c>UnauthorizedAccessException</c>, translated to 403 by <see cref="EndpointHelpers.ExecuteWriteAsync"/>.
/// No <c>RequireRateLimiting(...)</c> — unlike Tags (the one write surface that is plain HTTP today),
/// <see cref="ServerStoryWriteService.CreateStoryAsync"/> already throttles via the transport-agnostic
/// <c>IWriteRateLimitService</c> token bucket (<c>WriteActionKind.ContentCreate</c>), which surfaces as
/// a 429 through the same <see cref="EndpointHelpers.ExecuteWriteAsync"/> path
/// (<c>WriteRateLimitExceededException</c> — see <c>security.md</c> §"Write Throttling").
/// </para>
/// </summary>
public static class StoryEndpoints
{
    public static WebApplication MapStoryEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/stories");

        // ── Reads (public unless noted — see class summary) ──

        group.MapGet("/{storyId:int}", async (IStoryReadService stories, int storyId) =>
            Results.Json(await stories.GetStoryByIdAsync(storyId)));

        // Author-only editor read — wrapped in ExecuteWriteAsync (unlike the other reads) because
        // GetStoryForEditAsync enforces the author gate and throws UnauthorizedAccessException for
        // a non-author; the shared helper translates that to 403 so ClientStoryReadService's
        // 403→UnauthorizedAccessException mapping works over WASM (same wire shape as the
        // ChapterEndpoints /edit route, MA-301 precedent).
        group.MapGet("/{storyId:int}/edit", (IStoryReadService stories, int storyId) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Json(await stories.GetStoryForEditAsync(storyId))))
            .RequireAuthorization();

        // Repeated-key query binding (?storyIds=1&storyIds=2), same shape as ITagReadService's
        // GetTagChipsByIdsAsync — order-preserving, matching the service's reorder-to-input contract.
        group.MapGet("/by-ids", async (IStoryReadService stories, int[] storyIds) =>
            Results.Ok(await stories.GetListingsByIdsAsync(storyIds)));

        group.MapGet("/recent", async (IStoryReadService stories, int page, int pageSize) =>
        {
            (StoryListingDto[] Items, int TotalCount) result =
                await stories.GetRecentListingsAsync(page, pageSize);
            return Results.Ok(new PagedResult<StoryListingDto>(result.Items, result.TotalCount));
        });

        // POST-for-complex-read (layer5-wasm.md §"Reads with non-scalar parameters"): StoryFilterDto
        // isn't GET-bindable. restrictToStoryIds still binds from the query string (repeated-key
        // array) alongside the JSON-bound filter. [FromQuery] is REQUIRED here, not cosmetic:
        // RequestDelegateFactory's automatic array-from-query inference doesn't fire once another
        // parameter in the same handler is already being inferred as [FromBody] — left unattributed,
        // it resolves to an un-bindable "UNKNOWN" source and throws at app startup (every endpoint's
        // metadata is built eagerly for the AuthorizationPolicyCache, so this one bad handler took
        // down every Integration test — caught via `dotnet test`, not `dotnet build`, since MSBuild
        // has no static check for minimal-API parameter-source inference).
        group.MapPost("/query", async (
            IStoryReadService stories, StoryFilterDto filter, [FromQuery] int[]? restrictToStoryIds,
            [FromQuery] bool personalScope = false) =>
        {
            // personalScope: Personal-plane hydration for the WASM bookshelf/owner-list pass —
            // see IStoryReadService doc (only effective with a restrict set; a forged flag is a
            // deliberate API call per the Intentionality Doctrine, not a browse leak).
            (StoryListingDto[] Items, int TotalCount) result =
                await stories.GetListingsAsync(filter, restrictToStoryIds, personalScope);
            return Results.Ok(new PagedResult<StoryListingDto>(result.Items, result.TotalCount));
        });

        // Mature count-line disclosure reads (WU-AccessGate) — interstitial-grade metadata only.
        group.MapGet("/gated-cards", async (IStoryReadService stories, [FromQuery] int[] storyIds) =>
            Results.Ok(await stories.GetGatedCardsAsync(storyIds)));

        group.MapGet("/gated-by-author/{authorId:int}", async (IStoryReadService stories, int authorId) =>
            Results.Ok(await stories.GetGatedStoriesByAuthorAsync(authorId)));

        group.MapPost("/random-batch", async (
                IStoryReadService stories, StoryFilterDto filter, int batchSize) =>
            Results.Ok(await stories.GetRandomBatchAsync(filter, batchSize)));

        // Same [FromQuery] requirement as /query above — candidateIds sits alongside the
        // body-inferred filter DTO in one handler.
        group.MapPost("/filter-candidates", async (
                IStoryReadService stories, [FromQuery] int[] candidateIds, StoryFilterDto filter) =>
            Results.Ok(await stories.FilterCandidateIdsAsync(candidateIds, filter)));

        // Gated: bypasses the content-rating filter so authors always see their own mature stories
        // Anonymous-callable (WU-AccessGate Phase 1): feeds the PUBLIC profile Stories tab, which
        // 401-crashed on the anonymous WASM pass under the old RequireAuthorization gate (the
        // recurring MA-302 split-brain class). Safe: the service gives non-owners the
        // rating-filtered set only (the ContentRating bypass stays keyed to viewer == author,
        // never the client-supplied id) and enforces the author's ProfileVisibility.
        group.MapGet("/by-author/{authorId:int}", async (IStoryReadService stories, int authorId) =>
            Results.Ok(await stories.GetStoryIdsByAuthorAsync(authorId)));

        // Gated-existence read (WU-AccessGate): interstitial metadata (title/author/rating only)
        // for an M story the viewer hasn't consented to; JSON null for absent/taken-down. Backs
        // the WASM pass of the story/chapter interstitial pages. Public by design — it reveals
        // exactly what the interstitial page itself acknowledges.
        group.MapGet("/{storyId:int}/gate", async (IStoryReadService stories, int storyId) =>
            Results.Json(await stories.GetStoryGateAsync(storyId)));

        // Seeded lookup table (platform name + URL auto-detect pattern) — same public-lookup
        // treatment as ITagReadService.GetTagDirectoryAsync; not sensitive on its own.
        group.MapGet("/external-platforms", async (IStoryReadService stories) =>
            Results.Ok(await stories.GetExternalPlatformsAsync()));

        group.MapGet("/{storyId:int}/total-views", async (IStoryReadService stories, int storyId) =>
            Results.Ok(await stories.GetStoryTotalViewsAsync(storyId)));

        // Gated — genuinely unsure. The read itself has the same content-rating filters as any
        // other browse read, but every current consumer (StoryTitlePicker: Story Lineage's target
        // picker, Groups add-story) is an authenticated-only composition flow, not a public browse
        // page. Defaulting to RequireAuthorization() per layer5-wasm.md's "when genuinely unsure"
        // rule; revisit if a public consumer appears.
        group.MapGet("/search-by-title", async (IStoryReadService stories, string term) =>
                Results.Ok(await stories.SearchStoriesByTitleAsync(term)))
            .RequireAuthorization();

        // ── Writes (authenticated — ownership enforced by the service, translated here) ──

        group.MapPost("/", (IStoryWriteService stories, CreateStoryDTO dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await stories.CreateStoryAsync(dto))))
            .RequireAuthorization();

        group.MapPut("/{storyId:int}", (IStoryWriteService stories, int storyId, StoryUpdateDTO dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    storyId != dto.StoryId
                        ? Results.Problem(detail: "Route storyId does not match body StoryId.",
                            statusCode: StatusCodes.Status400BadRequest)
                        : await UpdateAndRespondAsync(stories, dto)))
            .RequireAuthorization();

        // Multipart (layer5-wasm.md §"Streams and multipart"): the antiforgery middleware
        // (Program.cs UseAntiforgery()) auto-requires a token on any endpoint that binds form data
        // unless explicitly disabled — this is a stateless API call authenticated by the same-origin
        // Identity cookie, not a Razor form post, so DisableAntiforgery() is correct here.
        group.MapPost("/{storyId:int}/cover", (IStoryWriteService stories, int storyId, IFormFile file) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await stories.UploadCoverArtAsync(
                        file.OpenReadStream(), file.ContentType, storyId))))
            .RequireAuthorization()
            .DisableAntiforgery();

        return app;
    }

    private static async Task<IResult> UpdateAndRespondAsync(IStoryWriteService stories, StoryUpdateDTO dto)
    {
        await stories.UpdateStoryAsync(dto);
        return Results.NoContent();
    }
}
