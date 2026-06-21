namespace TheCanalaveLibrary.Server;

/// <summary>
/// Home for Development-only diagnostic endpoints used to exercise service-layer code paths
/// directly during local verification (e.g. account deletion, where driving the real UI flow
/// requires logging in as the target user). Never mapped outside Development — see the
/// <c>app.Environment.IsDevelopment()</c> guard around <see cref="MapDevDiagnosticsEndpoints"/>
/// in Program.cs. Add new ad-hoc verification endpoints here rather than inline in Program.cs or
/// scattered across feature endpoint files.
/// </summary>
public static class DevDiagnosticsEndpoints
{
    public static void MapDevDiagnosticsEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder devApi = app.MapGroup("/dev");

        devApi.MapPost("/test-delete-user/{id:int}", async (int id, UserDeletionService deletionService) =>
            await deletionService.DeleteUserAsync(id) ? Results.Ok("deleted") : Results.NotFound());
    }
}
