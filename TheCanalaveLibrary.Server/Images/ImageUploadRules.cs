using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// The upload-validation rules and spec §3.17 key convention shared by every
/// <see cref="IImageStorageService"/> implementation. Extracted (WU-S3Garage) so
/// <c>LocalImageStorageService</c> and <c>S3ImageStorageService</c> cannot drift: identical
/// allow-list, identical size cap, identical keys — which is what keeps a stored
/// <c>/uploads/…</c> path interchangeable across implementations (audit/ImageStorage.md
/// "Key convention").
/// </summary>
public static class ImageUploadRules
{
    public const long MaxBytes = 10 * 1024 * 1024; // 10 MB

    /// <summary>Stored-path prefix. Local mode: static files under wwwroot. S3 mode: the
    /// <c>/uploads/{**key}</c> serving endpoint. Stored value = <c>UploadsPrefix + key</c>.</summary>
    public const string UploadsPrefix = "/uploads/";

    private static readonly Dictionary<string, string> AllowedContentTypes = new()
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp"
    };

    /// <summary>Maps an allowed MIME type to its file extension, or throws <see cref="ArgumentException"/>.</summary>
    public static string ExtensionFor(string contentType) =>
        AllowedContentTypes.TryGetValue(contentType, out string? extension)
            ? extension
            : throw new ArgumentException($"Unsupported image content type: {contentType}", nameof(contentType));

    /// <summary>Reverse lookup for serving: extension → MIME type (null when unknown).</summary>
    public static string? ContentTypeForExtension(string extension) =>
        AllowedContentTypes.FirstOrDefault(kv => kv.Value == extension.ToLowerInvariant()).Key;

    /// <summary>
    /// Builds the spec §3.17 storage key: <c>stories/{id}/cover-{uuid}.{ext}</c> /
    /// <c>users/{id}/profile-{uuid}.{ext}</c>. The stored entity value is
    /// <see cref="UploadsPrefix"/> + this key.
    /// </summary>
    public static string BuildKey(ImageKind kind, int ownerId, string extension)
    {
        (string subfolder, string filePrefix) = kind switch
        {
            ImageKind.Cover => ($"stories/{ownerId}", "cover"),
            ImageKind.ProfilePicture => ($"users/{ownerId}", "profile"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return $"{subfolder}/{filePrefix}-{Guid.NewGuid()}.{extension}";
    }

    /// <summary>
    /// Extracts the storage key from a stored relative path, returning null for anything that is
    /// not a well-formed <c>/uploads/…</c> value (wrong prefix, empty, or a <c>..</c> traversal
    /// segment). Callers treat null as "not ours — no-op", matching DeleteAsync's contract.
    /// </summary>
    public static string? TryExtractKey(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || !relativePath.StartsWith(UploadsPrefix))
        {
            return null;
        }

        string key = relativePath[UploadsPrefix.Length..];
        if (key.Length == 0 || key.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            return null;
        }
        return key;
    }
}
