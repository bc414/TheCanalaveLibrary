using System.Globalization;
using System.Text.RegularExpressions;
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

    /// <summary>
    /// Maps an allowed MIME type to its file extension, or throws
    /// <see cref="ImageUploadValidationException"/> (user-facing: the claimed type is the uploader's).
    /// </summary>
    public static string ExtensionFor(string contentType) =>
        AllowedContentTypes.TryGetValue(contentType, out string? extension)
            ? extension
            : throw new ImageUploadValidationException(UnsupportedTypeMessage);

    /// <summary>User-facing rejection text for a claimed type outside the allow-list.</summary>
    public const string UnsupportedTypeMessage = "Images must be JPEG, PNG or WebP files.";

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
    /// The top-level key prefixes <see cref="BuildKey"/> writes under — the whole upload key space.
    /// <see cref="IImageStorageService.EnumerateAsync"/> lists these and nothing else: sprites may
    /// share the production bucket and must never reach the orphan sweep (owner ruling D14).
    /// </summary>
    public static readonly IReadOnlyList<string> KeyPrefixes = ["stories/", "users/"];

    // Declared after AllowedContentTypes on purpose: static initializers run in textual order, and
    // the extension alternation is built from that dictionary so the two can never drift.
    private static readonly Regex GeneratedKeyPattern = new(
        "^(?:stories/(?<owner>[0-9]{1,10})/cover|users/(?<owner>[0-9]{1,10})/profile)" +
        "-[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}" +
        // \z, not $: in .NET, $ also matches before a trailing newline.
        $@"\.(?:{string.Join('|', AllowedContentTypes.Values.Distinct().Select(Regex.Escape))})\z",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);

    /// <summary>
    /// True only for a key in the exact shape <see cref="BuildKey"/> emits:
    /// <c>stories/{int}/cover-{guid}.{ext}</c> or <c>users/{int}/profile-{guid}.{ext}</c>, the kind
    /// matching its folder, the guid in <see cref="Guid.ToString()"/>'s lowercase "D" format, and the
    /// extension one this service family writes. The orphan sweep deletes only keys that pass: anything
    /// else in the upload prefixes (a hand-placed file, a traversal-shaped name) is foreign, never ours
    /// to delete (owner ruling D14).
    /// </summary>
    public static bool IsGeneratedKey(string? key)
    {
        if (key is null) return false;
        Match match = GeneratedKeyPattern.Match(key);
        // The owner id must be an int, as BuildKey's is: ten digits can still overflow one.
        return match.Success && int.TryParse(match.Groups["owner"].ValueSpan, NumberStyles.None,
            CultureInfo.InvariantCulture, out _);
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
