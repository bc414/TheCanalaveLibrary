using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Backstop for exceptions that escape every endpoint's own translation
/// (<see cref="EndpointHelpers.ExecuteAsync"/>), scoped to the JSON API surface only —
/// error-handling.md §"The API error envelope", layer5-wasm.md §"The Error-Translation
/// Contract" (WU-ErrorHandling2, 2026-07-30). Requests outside <c>/api</c> return
/// <see langword="false"/> so the pipeline falls through to the existing HTML
/// <c>UseExceptionHandler("/Error")</c> path (<c>Error.razor</c>) untouched — an API JSON
/// contract must never answer with an HTML document, and vice versa.
/// </summary>
public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Path.StartsWithSegments("/api"))
            return false;

        // Same "outermost catcher owns the log" rule as EndpointHelpers/CanalaveErrorBoundary
        // (logging.md §"Unhandled exceptions") — nothing upstream of here has logged this yet.
        string traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
        logger.LogError(exception, "Unhandled exception on {Path} ({TraceId})",
            httpContext.Request.Path, traceId);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                // The client reconstructs ServerFaultException(traceId) from this extension —
                // ClientHttpHelpers' default arm, error-handling.md §"The API error envelope".
                Extensions = { ["traceId"] = traceId },
            },
        });
    }
}
