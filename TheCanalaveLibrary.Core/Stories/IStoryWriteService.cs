namespace TheCanalaveLibrary.Core;

public interface IStoryWriteService
{
    /// <summary>
    /// Creates a new story.
    /// </summary>
    /// <param name="dto">A DTO containing the initial story properties.</param>
    /// <returns>The ID of the newly created story.</returns>
    Task<int> CreateStoryAsync(CreateStoryDTO dto);

    /// <summary>
    /// Updates an existing story's properties. Never changes status — the DTO's
    /// <c>StoryStatusId</c> is a read echo and is ignored (WU-StoryLifecycle, D1).
    /// </summary>
    /// <param name="dto">A DTO containing the story's updated properties.</param>
    Task UpdateStoryAsync(StoryUpdateDTO dto);

    /// <summary>
    /// Author-requested lifecycle move, applied through <see cref="StoryLifecycle.ResolveAuthorTransition"/>
    /// (submit, withdraw, revise-after-rejection, moves among the published statuses, unpublish).
    /// A trusted author's submit lands straight at <c>PostApprovalStatus</c>; the first move into
    /// the published set stamps <c>PublishedDate</c> (never re-stamped). Never touches
    /// <c>LastUpdatedDate</c>; never writes <c>IsTakenDown</c>, and refuses every move while it is set
    /// (a taken-down story's status is frozen). See <c>layer2-services.md</c> §"Story Lifecycle".
    /// </summary>
    /// <returns>The status the story actually landed on — lets the UI tell "Published" from
    /// "Submitted for review".</returns>
    /// <exception cref="StoryValidationException">Undefined or illegal target, no valid
    /// <c>PostApprovalStatus</c> at submit, the story is taken down, or the status changed since it
    /// was read.</exception>
    /// <exception cref="KeyNotFoundException">No story with <paramref name="storyId"/>.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not the story's author.</exception>
    Task<StoryStatusEnum> TransitionStatusAsync(int storyId, StoryStatusEnum targetStatus);

    /// <summary>
    /// Uploads a new cover image for <paramref name="storyId"/> via <c>IImageStorageService</c> and
    /// returns the resulting relative path — the caller still patches
    /// <c>StoryUpdateDTO.CoverArtRelativeUrl</c> and calls <see cref="UpdateStoryAsync"/> itself
    /// (same two-step shape <c>StoryEditorPage</c> already uses). Mirrors
    /// <see cref="IUserSettingsService.UploadProfilePictureAsync"/>'s service-owns-the-storage-call
    /// pattern, added so the WASM boundary never needs a client impl of the server-only
    /// <c>IImageStorageService</c> (layer5-wasm.md "Streams and multipart").
    /// </summary>
    /// <param name="content">The raw file stream from &lt;InputFile&gt;.</param>
    /// <param name="contentType">MIME type (e.g. "image/jpeg").</param>
    /// <param name="storyId">The story to attach the cover to — must already exist (created via
    /// <see cref="CreateStoryAsync"/> first, for the new-story flow) and be owned by the caller.</param>
    /// <returns>The new relative URL to store on the story.</returns>
    /// <exception cref="KeyNotFoundException">No story with <paramref name="storyId"/> exists.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller does not own the story.</exception>
    Task<string> UploadCoverArtAsync(Stream content, string contentType, int storyId);
}