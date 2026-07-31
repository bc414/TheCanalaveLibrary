using System.Diagnostics;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// The single exception → user-facing-message mapper (cross-cutting.md §"Error Handling
/// Strategy" — exception-message discipline). Only typed user-facing exceptions surface their
/// own message; the whole per-feature validation family is matched via its shared
/// <see cref="CanalaveValidationException"/> base (MA-008) rather than one arm per type;
/// BCL exception messages are developer text and are never shown. Everything
/// unexpected maps to <see cref="GenericMessage"/> suffixed with the current trace id, so what
/// the user sees can be joined to what the server logged (logging.md §"Unhandled exceptions").
///
/// Catch-site pattern (components):
/// <code>
/// catch (Exception ex)
/// {
///     if (!ExceptionPresenter.IsUserFacing(ex))
///         Logger.LogError(ex, "Saving chapter {ChapterContentId} failed unexpectedly", id);
///     _errors = ExceptionPresenter.GetUserMessages(ex);
/// }
/// </code>
/// Typed user-facing exceptions are "translate, don't log" (expected traffic); unexpected ones
/// log at Error — the outermost catcher owns the log, per the no-double-log rule.
/// </summary>
public static class ExceptionPresenter
{
    public const string GenericMessage = "Something went wrong on our end. Please try again.";
    public const string PermissionMessage = "You don't have permission to do that.";
    public const string NotFoundMessage = "That content couldn't be found — it may have been removed.";

    /// <summary>
    /// True when <see cref="GetUserMessages"/> returns a message that fully explains the failure
    /// to the user (typed domain exceptions + the two fixed-text BCL translations). False means
    /// "unexpected" — the catch site must log at Error before showing the generic message.
    /// <see cref="ServerFaultException"/> is deliberately excluded even though it carries a
    /// server-produced trace id: it IS the generic-message path (the server already logged the
    /// failure at Error when it produced the envelope), so a catch site must not log it again —
    /// see error-handling.md §"The API error envelope".
    /// </summary>
    public static bool IsUserFacing(Exception ex) => ex
        is CanalaveValidationException
        or VouchLimitException
        or ContentRatingExceededException
        or MessagingPermissionException
        or WriteRateLimitExceededException
        or SessionExpiredException
        or UnauthorizedAccessException
        or KeyNotFoundException;

    /// <summary>User-facing message list for inline display (e.g. a form's InlineAlert).</summary>
    public static IReadOnlyList<string> GetUserMessages(Exception ex) => ex switch
    {
        // The validation family — one base (MA-008), each instance carries its own
        // user-written error list.
        CanalaveValidationException e => e.Errors,

        // Single-message typed exceptions whose Message is deliberately user-ready.
        VouchLimitException or ContentRatingExceededException
            or MessagingPermissionException or WriteRateLimitExceededException
            or SessionExpiredException => [ex.Message],

        // BCL types the write services use by documented convention — fixed friendly text;
        // their actual Message may be framework-generated developer text.
        UnauthorizedAccessException => [PermissionMessage],
        KeyNotFoundException => [NotFoundMessage],

        // The server already logged this at Error when it produced the envelope — present with
        // ITS trace id (correct under both InteractiveServer and the WASM hop), never re-log.
        ServerFaultException e => [WithErrorId(GenericMessage, e.TraceId)],

        _ => [WithErrorId(GenericMessage)],
    };

    /// <summary>Single-string convenience for surfaces holding one error field.</summary>
    public static string GetUserMessage(Exception ex) => string.Join(" ", GetUserMessages(ex));

    /// <summary>
    /// Appends a trace id so a user-reported generic error can be found in the logs. Prefers
    /// <paramref name="explicitId"/> — the server-produced id off a <see cref="ServerFaultException"/>
    /// envelope, correct under the WASM hop where there is no ambient <see cref="Activity"/> — and
    /// falls back to <see cref="Activity.Current"/> (correct for the InteractiveServer in-process
    /// path, where the exception was never carried across HTTP). Neither present (unit tests,
    /// unsampled) → the plain message.
    /// </summary>
    public static string WithErrorId(string message, string? explicitId = null)
    {
        string? traceId = explicitId ?? Activity.Current?.TraceId.ToString();
        return traceId is null ? message : $"{message} (Error ID: {traceId})";
    }
}
