using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Filesystem implementation of <see cref="IImageStorageService"/> — writes under
/// <c>wwwroot/uploads/</c>, served directly by <c>UseStaticFiles()</c>. Returns a host-relative URL
/// (e.g. <c>/uploads/stories/5/cover-{uuid}.jpg</c>) that resolves against whatever origin the app
/// is running on — see audit/ImageStorage.md "URL conventions". The default provider
/// (<c>ImageStorage:Provider</c> = <c>Local</c>); <c>S3ImageStorageService</c> is the S3-compatible
/// alternative (Garage in dev via Aspire, Cloudflare R2 in prod) behind this same interface.
/// Validation rules and key convention are shared via <see cref="ImageUploadRules"/> so stored
/// paths stay interchangeable across implementations.
/// </summary>
public class LocalImageStorageService(IWebHostEnvironment env) : IImageStorageService
{
    public async Task<string> SaveAsync(Stream content, string contentType, ImageKind kind, int ownerId)
    {
        string extension = ImageUploadRules.ExtensionFor(contentType);

        if (content.CanSeek && content.Length > ImageUploadRules.MaxBytes)
        {
            throw new ArgumentException(
                $"Image exceeds the {ImageUploadRules.MaxBytes / (1024 * 1024)} MB limit.", nameof(content));
        }

        string key = ImageUploadRules.BuildKey(kind, ownerId, extension);

        string fullPath = Path.Combine(env.WebRootPath, "uploads", key.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using FileStream fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream);

        // Host-relative — never a full URL (spec §3.17 explicitly rejected storing one).
        return ImageUploadRules.UploadsPrefix + key;
    }

    public Task DeleteAsync(string relativePath)
    {
        // TryExtractKey rejects non-/uploads/ values and any ".." traversal segment — the same
        // defensive stance the pre-refactor Path.GetFullPath check provided.
        string? key = ImageUploadRules.TryExtractKey(relativePath);
        if (key is null)
        {
            return Task.CompletedTask;
        }

        string uploadsRoot = Path.GetFullPath(Path.Combine(env.WebRootPath, "uploads"));
        string fullPath = Path.GetFullPath(Path.Combine(uploadsRoot, key.Replace('/', Path.DirectorySeparatorChar)));

        // Belt-and-suspenders: even with segment validation above, never delete outside uploads/.
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
