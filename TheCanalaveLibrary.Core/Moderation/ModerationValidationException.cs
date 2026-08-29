namespace TheCanalaveLibrary.Core;

/// <summary>
/// Thrown by moderator write operations when an action cannot be applied for a reason the
/// moderator can act on — e.g. a report whose content has no resolvable author. Mirrors
/// <see cref="ChapterValidationException"/>.
/// <para>Being a <see cref="CanalaveValidationException"/> is the point: these messages reach the
/// moderator verbatim through <c>ExceptionPresenter</c>, where the <c>InvalidOperationException</c>
/// this replaced was flattened to "Something went wrong on our end."</para>
/// </summary>
public class ModerationValidationException(List<string> errors)
    : CanalaveValidationException(string.Join("; ", errors), errors);
