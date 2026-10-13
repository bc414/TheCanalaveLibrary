using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.SharedUI;

/// <summary>
/// View model for ChapterPropertiesForm. Carries UI-only state and shields the form from
/// server-only DTO fields (ChapterId, ChapterContentId, StoryId). Rating nullable = "Same as
/// story (inherit)"; non-null = explicit override subject to floor invariant.
/// </summary>
public class ChapterEditorViewModel : EditorFormViewModel
{
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Populated from EditorView.GetHtmlAsync() by the page before mapping to the write DTO.
    /// Not two-way bound — EditorView is pull-on-submit.
    /// </summary>
    public string? ChapterText { get; set; }

    public string? TopAuthorsNote { get; set; }
    public string? BottomAuthorsNote { get; set; }

    /// <summary>
    /// Null = inherit story rating (the "Same as story" UI option).
    /// Non-null = explicit override; floor invariant enforced server-side.
    /// </summary>
    public Rating? Rating { get; set; }

    public string? VersionName { get; set; }

    // IsLoading and the server-error state (ServerValidationErrors + its session flag) come from
    // EditorFormViewModel: write them through ShowServerError/ClearServerErrors.
}
