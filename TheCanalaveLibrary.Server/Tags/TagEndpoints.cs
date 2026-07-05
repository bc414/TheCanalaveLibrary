using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="ITagReadService"/> / <see cref="ITagWriteService"/>.
/// Thin pass-throughs: no business logic here — validation and the mod/admin gate live in the
/// service (single enforcement point). The endpoint's only job beyond routing is translating the
/// service contract's typed exceptions into status codes so the client impls can translate them
/// back (see ClientTagWriteService): TagValidationException → 400 (ProblemDetails.Detail carries
/// the message verbatim — it is user-facing), UnauthorizedAccessException → 403,
/// KeyNotFoundException → 404. Unauthenticated callers get 401 from the cookie handler config
/// (Program.cs OnRedirectToLogin) without reaching the service.
/// </summary>
public static class TagEndpoints
{
    public static WebApplication MapTagEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/tags");

        // ── Reads (public, no auth gate — mirrors the public /tags browse page) ──

        group.MapGet("/directory", async (ITagReadService tags) =>
            Results.Ok(await tags.GetTagDirectoryAsync()));

        // Covers GetTagsByTypeAsync and its four GetAll{Type}TagsAsync convenience wrappers —
        // the wrappers are client-side sugar over the same query (same shape as the server impl).
        group.MapGet("/", async (ITagReadService tags, TagTypeEnum type) =>
            Results.Ok(await tags.GetTagsByTypeAsync(type)));

        group.MapGet("/chips/search", async (ITagReadService tags, TagTypeEnum type, string term) =>
            Results.Ok(await tags.SearchTagChipsAsync(type, term)));

        // Repeated-key query binding (?ids=1&ids=2) — order-preserving, matching the service's
        // reorder-to-input contract.
        group.MapGet("/chips/by-ids", async (ITagReadService tags, int[] ids) =>
            Results.Ok(await tags.GetTagChipsByIdsAsync(ids)));

        // ── Writes (mod/admin — enforced by the service's RequireMod, translated here) ──

        group.MapPost("/", (ITagWriteService tags, CreateTagDto dto) =>
            ExecuteWriteAsync(async () => Results.Ok(await tags.CreateTagAsync(dto))));

        group.MapPut("/{tagId:int}", (ITagWriteService tags, int tagId, UpdateTagDto dto) =>
            ExecuteWriteAsync(async () =>
                tagId != dto.TagId
                    ? Results.Problem(detail: "Route tagId does not match body TagId.",
                        statusCode: StatusCodes.Status400BadRequest)
                    // Body is the raw sprite-warning string (or JSON null) — the service contract
                    // returns string?, and Layer 5 is a body-swap: no new wrapper DTO is minted.
                    : Results.Json(await tags.UpdateTagAsync(dto))));

        group.MapDelete("/{tagId:int}", (ITagWriteService tags, int tagId) =>
            ExecuteWriteAsync(async () =>
            {
                await tags.DeleteTagAsync(tagId);
                return Results.NoContent();
            }));

        return app;
    }

    private static async Task<IResult> ExecuteWriteAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (TagValidationException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (KeyNotFoundException)
        {
            // Results.Problem, NOT Results.NotFound(): the app's UseStatusCodePagesWithReExecute
            // re-executes BODY-LESS error responses into the HTML /not-found route with the
            // original HTTP method — a PUT/DELETE re-executed against that GET-only page comes
            // back 405. Bodied results are skipped by that middleware. Applies to every API
            // error status; see layer5-wasm.md §"The Error-Translation Contract".
            return Results.Problem(statusCode: StatusCodes.Status404NotFound);
        }
    }
}
