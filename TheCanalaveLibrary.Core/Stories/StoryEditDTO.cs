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
}
