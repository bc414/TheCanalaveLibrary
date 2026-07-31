namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown client-side (<c>ClientHttpHelpers</c>) when an API call comes back an unhandled 5xx
/// carrying the <c>ProblemDetails</c> envelope's <c>traceId</c> extension (the
/// <c>/api</c>-scoped <c>ApiExceptionHandler</c>, <c>Program.cs</c>) — replaces the old bare
/// <c>EnsureSuccessStatusCode()</c>/<c>HttpRequestException</c> fall-through. Carries the
/// <em>server's</em> trace id so the id a user reports is the id of the request that actually
/// failed, correct under both InteractiveServer and the WASM hop — <c>Activity.Current</c> is
/// null in WASM, so the old <c>Activity.Current</c>-only fallback showed no id at all there
/// (WU-ErrorHandling2, 2026-07-30; error-handling.md §"The API error envelope"). Deliberately NOT
/// user-facing (<see cref="ExceptionPresenter.IsUserFacing"/> excludes it — it is the generic
/// path); the failure was already logged at <c>Error</c> server-side when the envelope was
/// produced, so catch sites must not log it again.
/// </summary>
public sealed class ServerFaultException(string? traceId) : Exception("An unexpected server error occurred.")
{
    /// <summary>The failing request's server-side trace id, if the envelope carried one.</summary>
    public string? TraceId { get; } = traceId;
}
