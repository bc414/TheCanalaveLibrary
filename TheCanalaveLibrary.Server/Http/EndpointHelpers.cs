using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Shared write-endpoint exception→status translation (layer5-wasm.md §"The Error-Translation
/// Contract"). Every <c>{Feature}Endpoints.cs</c> write handler wraps its body in
/// <see cref="ExecuteWriteAsync"/> instead of hand-rolling its own try/catch — this is the single
/// copy of the mapping, extended past TagEndpoints' original 3-case version to the full set of
/// exception types thrown across the service layer.
/// </summary>
public static class EndpointHelpers
{
    public static async Task<IResult> ExecuteWriteAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (IsValidationException(ex))
        {
            // Every `{Feature}ValidationException` (13 distinct types — none share a common base;
            // matched by name suffix rather than retrofitting a marker base onto all of them) plus
            // ArgumentException/ArgumentOutOfRangeException, VouchLimitException, and
            // ImportException: all are "the request was malformed/rejected, message is user-facing."
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden);
        }
        catch (MessagingPermissionException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (ContentRatingExceededException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (KeyNotFoundException)
        {
            // Results.Problem, NOT Results.NotFound(): the app's UseStatusCodePagesWithReExecute
            // re-executes BODY-LESS error responses into the HTML /not-found route with the
            // original HTTP method — a PUT/DELETE re-executed against that GET-only page comes back
            // 405. Bodied results are skipped by that middleware. Applies to every API error status.
            return Results.Problem(statusCode: StatusCodes.Status404NotFound);
        }
        catch (WriteRateLimitExceededException ex)
        {
            // security.md §"Write Throttling". RetryAfter surfaces in the body (extensions) rather
            // than a response header — the client-side translation reads it the same way it reads
            // ProblemDetails.Detail, no header-plumbing needed.
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status429TooManyRequests,
                extensions: new Dictionary<string, object?>
                {
                    ["retryAfterSeconds"] = Math.Ceiling(ex.RetryAfter.TotalSeconds)
                });
        }
        catch (InvalidOperationException ex)
        {
            // Auth safety net. Every service method that throws this does so for "...requires an
            // authenticated user" — and every endpoint that calls such a method also carries
            // .RequireAuthorization() as defense-in-depth, so in practice the cookie handler's own
            // 401 (Program.cs OnRedirectToLogin) wins the race and this catch is rarely reached.
            // It exists so a missing .RequireAuthorization() fails as 401, not 500.
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status401Unauthorized);
        }
        // Anything else (e.g. the one domain-invariant NotSupportedException) → unhandled → 500;
        // the client surfaces it via HttpRequestException/EnsureSuccessStatusCode().
    }

    private static bool IsValidationException(Exception ex) =>
        ex.GetType().Name.EndsWith("ValidationException", StringComparison.Ordinal)
        || ex is ArgumentException or VouchLimitException or ImportException;
}
