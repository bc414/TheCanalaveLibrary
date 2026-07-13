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
    /// Updates an existing story's properties.
    /// </summary>
    /// <param name="dto">A DTO containing the story's updated properties.</param>
    Task UpdateStoryAsync(StoryUpdateDTO dto);

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