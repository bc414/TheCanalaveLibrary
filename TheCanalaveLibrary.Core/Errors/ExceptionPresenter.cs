using System.Diagnostics;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// The single exception → user-facing-message mapper (cross-cutting.md §"Error Handling
/// Strategy" — exception-message discipline). Only typed user-facing exceptions surface their
/// own message; BCL exception messages are developer text and are never shown. Everything
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
    /// </summary>
    public static bool IsUserFacing(Exception ex) => ex
        is StoryValidationException
        or ChapterValidationException
        or CommentValidationException
        or RecommendationValidationException
        or BlogPostValidationException
        or PollValidationException
        or GroupValidationException
        or SeriesValidationException
        or StoryLineageValidationException
        or MessagingValidationException
        or TagValidationException
        or VouchLimitException
        or ContentRatingExceededException
        or MessagingPermissionException
        or WriteRateLimitExceededException
        or UnauthorizedAccessException
        or KeyNotFoundException;

    /// <summary>User-facing message list for inline display (e.g. a form's InlineAlert).</summary>
    public static IReadOnlyList<string> GetUserMessages(Exception ex) => ex switch
    {
        // Multi-message validation family — each carries its own user-written error list.
        StoryValidationException e => e.ValidationErrors,
        ChapterValidationException e => e.Errors,
        CommentValidationException e => e.Errors,
        RecommendationValidationException e => e.Errors,
        BlogPostValidationException e => e.Errors,
        PollValidationException e => e.Errors,
        GroupValidationException e => e.Errors,
        SeriesValidationException e => e.Errors,
        StoryLineageValidationException e => e.Errors,
        MessagingValidationException e => e.Errors,

        // Single-message typed exceptions whose Message is deliberately user-ready.
        TagValidationException or VouchLimitException or ContentRatingExceededException
            or MessagingPermissionException or WriteRateLimitExceededException => [ex.Message],

        // BCL types the write services use by documented convention — fixed friendly text;
        // their actual Message may be framework-generated developer text.
        UnauthorizedAccessException => [PermissionMessage],
        KeyNotFoundException => [NotFoundMessage],

        _ => [WithErrorId(GenericMessage)],
    };

    /// <summary>Single-string convenience for surfaces holding one error field.</summary>
    public static string GetUserMessage(Exception ex) => string.Join(" ", GetUserMessages(ex));

    /// <summary>
    /// Appends the ambient trace id so a user-reported generic error can be found in the logs.
    /// No ambient activity (unit tests, unsampled) → the plain message.
    /// </summary>
    public static string WithErrorId(string message)
    {
        string? traceId = Activity.Current?.TraceId.ToString();
        return traceId is null ? message : $"{message} (Error ID: {traceId})";
    }
}
