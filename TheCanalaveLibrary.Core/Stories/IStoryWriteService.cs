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
    /// Uploads a new cover image for <paramref name="storyId"/> and <b>commits it at once</b>: stores
    /// the blob, persists it as the story's cover, then deletes the old cover's blob best-effort. No
    /// follow-up <see cref="UpdateStoryAsync"/> is needed or possible: the cover is not a property edit
    /// (owner ruling D14; <c>layer2-services.md</c> §"Image Storage — blob ownership and cleanup").
    /// Stamps <c>LastUpdatedDate</c>. Mirrors <see cref="IUserSettingsService.UploadProfilePictureAsync"/>,
    /// so the WASM boundary never needs a client impl of the server-only <c>IImageStorageService</c>
    /// (layer5-wasm.md "Streams and multipart").
    /// </summary>
    /// <param name="content">The raw file stream from &lt;InputFile&gt;.</param>
    /// <param name="contentType">MIME type (e.g. "image/jpeg").</param>
    /// <param name="storyId">The story to attach the cover to — must already exist (created via
    /// <see cref="CreateStoryAsync"/> first, for the new-story flow) and be owned by the caller.</param>
    /// <returns>The new cover's stored relative path, already persisted.</returns>
    /// <exception cref="KeyNotFoundException">No story with <paramref name="storyId"/> exists.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not the story's author — including a
    /// story whose author deleted their account.</exception>
    /// <exception cref="ImageUploadValidationException">The upload pipeline refused the file (type,
    /// size, unreadable bytes or dimensions); the message is user-facing.</exception>
    Task<string> UploadCoverArtAsync(Stream content, string contentType, int storyId);

    /// <summary>
    /// Clears the story's cover and deletes its blob best-effort (owner ruling D14: clearing is its own
    /// operation, never a null in an update DTO). Commits at once and stamps <c>LastUpdatedDate</c>; a
    /// story with no cover is a no-op.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No story with <paramref name="storyId"/> exists.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not the story's author.</exception>
    Task RemoveCoverArtAsync(int storyId);
}