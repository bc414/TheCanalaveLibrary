using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.SharedUI;

/// <summary>
/// The UI-only state every editor form's view model carries: the busy flag and the server's
/// refusal, which the form renders through <see cref="ErrorAlert"/> (error-handling.md §"The API
/// error envelope"; the WU-ErrorHandling2 follow-up, built by WU-TrackerFixes 2026-10-13).
/// <para>
/// The messages and the session flag change together, through <see cref="ShowServerError"/> and
/// <see cref="ClearServerErrors"/>, so the form never offers Sign in beside a message the session
/// did not cause. A handler's failure replaces the previous one's messages; it never appends.
/// </para>
/// </summary>
public abstract class EditorFormViewModel
{
    public bool IsLoading { get; set; }

    /// <summary>
    /// The messages the form shows. Written through <see cref="ShowServerError"/>; tests may add
    /// strings directly to check the form renders them.
    /// </summary>
    public List<string> ServerValidationErrors { get; set; } = [];

    /// <summary>True when the shown messages came from a <see cref="SessionExpiredException"/>:
    /// the form's <see cref="ErrorAlert"/> then offers Sign in.</summary>
    public bool ServerErrorIsSessionExpired { get; private set; }

    /// <summary>Replaces the shown messages with <paramref name="ex"/>'s user-facing ones
    /// (<see cref="ExceptionPresenter"/>) and records whether the session expired.</summary>
    public void ShowServerError(Exception ex)
    {
        ServerValidationErrors = [.. ExceptionPresenter.GetUserMessages(ex)];
        ServerErrorIsSessionExpired = ex is SessionExpiredException;
    }

    /// <summary>Clears the messages and the session flag (a handler calls it before it tries).</summary>
    public void ClearServerErrors()
    {
        ServerValidationErrors = [];
        ServerErrorIsSessionExpired = false;
    }
}
