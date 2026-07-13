using Microsoft.AspNetCore.Mvc;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IPollReadService"/> / <see cref="IPollWriteService"/>
/// (Feature 37). Thin pass-throughs: no business logic here — the viewer-relative projection
/// (results visibility, voter-name blanking) and the mod/author authorization gates live in the
/// service (single enforcement point). Every write handler wraps in the shared
/// <see cref="EndpointHelpers.ExecuteWriteAsync"/> for exception→status translation
/// (layer5-wasm.md §"The Error-Translation Contract").
/// <para>
/// Read auth: public — mirrors the public <c>/polls</c> page and the blog-post pages that render
/// attached polls; the read projection itself already blanks tallies/voter names server-side for
/// viewers who shouldn't see them (<c>ServerPollReadService.ProjectAsync</c>), so no endpoint-level
/// gate is needed.
/// </para>
/// <para>
/// Write auth: <c>RequireAuthorization()</c> on every write — site-poll create/archive additionally
/// enforce moderator/admin via <c>UnauthorizedAccessException</c> (403), blog-post-poll create and
/// poll manage (update/close/delete) enforce owner-only the same way; all are UI-convenience-only
/// gates backed by the service's own checks. Voting requires only an authenticated user.
/// </para>
/// </summary>
public static class PollEndpoints
{
    public static WebApplication MapPollEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/polls");

        // ── Reads (public — see class summary) ──

        group.MapGet("/", async (IPollReadService polls, bool includeArchived) =>
            Results.Ok(await polls.GetSitePollsAsync(includeArchived)));

        group.MapGet("/by-blog-post/{blogPostId:int}", async (IPollReadService polls, int blogPostId) =>
            Results.Ok(await polls.GetPollsForBlogPostAsync(blogPostId)));

        group.MapGet("/{pollId:int}", async (IPollReadService polls, int pollId) =>
            Results.Json(await polls.GetPollAsync(pollId)));

        // ── Writes (authenticated — mod/owner ownership enforced by the service) ──

        group.MapPost("/site", (IPollWriteService polls, PollEditDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await polls.CreateSitePollAsync(dto))))
            .RequireAuthorization();

        group.MapPost("/blog-post/{blogPostId:int}", (IPollWriteService polls, int blogPostId, PollEditDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await polls.CreateBlogPostPollAsync(blogPostId, dto))))
            .RequireAuthorization();

        group.MapPut("/{pollId:int}", (IPollWriteService polls, int pollId, PollEditDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await polls.UpdatePollAsync(pollId, dto);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        group.MapPost("/{pollId:int}/close", (IPollWriteService polls, int pollId) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await polls.ClosePollAsync(pollId);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        group.MapPost("/{pollId:int}/archive", (IPollWriteService polls, int pollId, bool archived) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await polls.SetSitePollArchivedAsync(pollId, archived);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        group.MapDelete("/{pollId:int}", (IPollWriteService polls, int pollId) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await polls.DeletePollAsync(pollId);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        // optionIds is a repeated-key query array (?optionIds=1&optionIds=2). [FromQuery] is
        // REQUIRED, not cosmetic: on GET a bare int[] infers as query-bound, but on POST it
        // infers as [FromBody] — the unattributed version demanded a JSON body the client never
        // sends and 400'd every vote ("Implicit body inferred for parameter") — found live in the
        // Global Flip browser wave. Sibling rule to StoryEndpoints' /query [FromQuery] note.
        group.MapPost("/{pollId:int}/vote", (
                IPollWriteService polls, int pollId, [FromQuery] int[] optionIds, bool voteAnonymously) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await polls.VoteAsync(pollId, optionIds, voteAnonymously))))
            .RequireAuthorization();

        return app;
    }
}
