namespace TheCanalaveLibrary.Core;

/// <summary>
/// What <see cref="IStoryReadService.GetStoryForEditAsync"/> returns: the editable properties
/// (<see cref="StoryUpdateDTO"/>) plus the story's current cover as a <b>read echo</b>. The cover is not
/// a property edit (owner ruling D14): the write path never reads this field. The editor shows it, sets
/// it through <see cref="IStoryWriteService.UploadCoverArtAsync"/> (which persists it) and clears it
/// through <see cref="IStoryWriteService.RemoveCoverArtAsync"/>. Passing a <see cref="StoryEditDTO"/> to
/// <see cref="IStoryWriteService.UpdateStoryAsync"/> is harmless: the cover rides along unread.
/// </summary>
public class StoryEditDTO : StoryUpdateDTO
{
    /// <summary>The stored relative path of the current cover, or null. Read echo only.</summary>
    public string? CoverArtRelativeUrl { get; set; }

    /// <summary>
    /// Read echo only: a moderator has taken the story down. The author's edit read still returns such
    /// a story (owner ruling D15: a takedown never blocks the author's delete), and the editor then shows
    /// only its Delete story section — no property edits, no lifecycle moves (a taken-down story's status
    /// is frozen), no download (the export 404s). <c>ChapterEditorPage</c> treats it as not found.
    /// </summary>
    public bool IsTakenDown { get; set; }
}
