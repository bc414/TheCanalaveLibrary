namespace TheCanalaveLibrary.Core;

/// <summary>
/// Which user-upload slot an image is being saved for — drives the storage key convention (spec
/// §3.17): <c>stories/{ownerId}/cover-{uuid}.{ext}</c> for <see cref="Cover"/>,
/// <c>users/{ownerId}/profile-{uuid}.{ext}</c> for <see cref="ProfilePicture"/>.
/// </summary>
public enum ImageKind
{
    Cover,
    ProfilePicture
}

/// <summary>
/// Write-side blob storage for user-uploaded images (cover art, profile pictures) — minted WU12, a
/// cross-cutting cluster (Core/Images/) with no owning feature; see audit/ImageStorage.md. Distinct
/// from <c>ISpriteReadService</c>, which only RESOLVES keys for git-managed static assets — this
/// service WRITES new blobs and returns the relative path the entity then stores verbatim
/// (<c>StoryListing.CoverArtRelativeUrl</c>, <c>User.ProfilePictureRelativeUrl</c>). Never returns a
/// full URL (spec §3.17 — a CDN domain change would otherwise require rewriting every row).
/// </summary>
public interface IImageStorageService
{
    /// <summary>
    /// Saves an uploaded image and returns the relative path to store on the entity. Does not dispose
    /// <paramref name="content"/> — that remains the caller's responsibility.
    /// </summary>
    /// <param name="content">The image bytes.</param>
    /// <param name="contentType">MIME type, validated against an allow-list.</param>
    /// <param name="kind">Which upload slot — drives the key convention.</param>
    /// <param name="ownerId">StoryId for <see cref="ImageKind.Cover"/>, UserId for
    /// <see cref="ImageKind.ProfilePicture"/>.</param>
    /// <returns>A relative path (e.g. <c>/uploads/stories/5/cover-{uuid}.jpg</c>), never a full URL.</returns>
    /// <exception cref="ImageUploadValidationException">The upload pipeline refused the file (type,
    /// size, unreadable bytes or dimensions); the message is user-facing.</exception>
    Task<string> SaveAsync(Stream content, string contentType, ImageKind kind, int ownerId);

    /// <summary>Deletes a previously saved image by its stored relative path. No-ops if not found.</summary>
    Task DeleteAsync(string relativePath);

    /// <summary>
    /// Streams every stored upload under the upload key space only (<c>stories/</c> and
    /// <c>users/</c>) — never anything else the store holds, such as sprites in a shared bucket — and
    /// never materializes the store: S3 pages <c>ListObjectsV2</c>, Local walks the folders lazily.
    /// Each item carries the store-reported last-modified time. Added for the orphan sweep (owner
    /// ruling D14, WU-BlobCleanup 2026-10-13), as a recorded amendment to this frozen interface; see
    /// audit/ImageStorage.md "Contract".
    /// </summary>
    IAsyncEnumerable<StoredImage> EnumerateAsync(CancellationToken cancellationToken = default);
}
