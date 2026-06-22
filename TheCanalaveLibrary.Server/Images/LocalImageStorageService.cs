using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// MVP implementation of <see cref="IImageStorageService"/> — writes under <c>wwwroot/uploads/</c>,
/// served directly by <c>UseStaticFiles()</c>. Returns a host-relative URL (e.g.
/// <c>/uploads/stories/5/cover-{uuid}.jpg</c>) that resolves against whatever origin the app is
/// running on — localhost in dev, the real domain once deployed — see audit/ImageStorage.md "URL
/// conventions". Post-MVP swap target: <c>S3ImageStorageService</c> (AWSSDK.S3, MinIO/R2 endpoints)
/// behind this same interface — see workplan.md Post-MVP section.
/// </summary>
public class LocalImageStorageService(IWebHostEnvironment env) : IImageStorageService
{
    private const long MaxBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly Dictionary<string, string> AllowedContentTypes = new()
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp"
    };

    public async Task<string> SaveAsync(Stream content, string contentType, ImageKind kind, int ownerId)
    {
        if (!AllowedContentTypes.TryGetValue(contentType, out string? extension))
        {
            throw new ArgumentException($"Unsupported image content type: {contentType}", nameof(contentType));
        }

        if (content.CanSeek && content.Length > MaxBytes)
        {
            throw new ArgumentException($"Image exceeds the {MaxBytes / (1024 * 1024)} MB limit.", nameof(content));
        }

        // Spec §3.17 key convention — honored by both this impl and the Post-MVP S3ImageStorageService
        // so a stored path is interchangeable across implementations (no data migration on swap).
        string subfolder = kind switch
        {
            ImageKind.Cover => $"stories/{ownerId}",
            ImageKind.ProfilePicture => $"users/{ownerId}",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        string filePrefix = kind == ImageKind.Cover ? "cover" : "profile";
        string fileName = $"{filePrefix}-{Guid.NewGuid()}.{extension}";

        string directory = Path.Combine(env.WebRootPath, "uploads", subfolder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(directory);

        string fullPath = Path.Combine(directory, fileName);
        await using FileStream fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream);

        // Host-relative — never a full URL (spec §3.17 explicitly rejected storing one).
        return $"/uploads/{subfolder}/{fileName}";
    }

    public Task DeleteAsync(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || !relativePath.StartsWith("/uploads/"))
        {
            return Task.CompletedTask;
        }

        string uploadsRoot = Path.GetFullPath(Path.Combine(env.WebRootPath, "uploads"));
        string relativeDiskPath = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.GetFullPath(Path.Combine(env.WebRootPath, relativeDiskPath));

        // Defensive: reject anything that escapes wwwroot/uploads/ via "..", even though relativePath
        // should only ever be a value this class itself previously returned.
        if (!fullPath.StartsWith(uploadsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }
}
