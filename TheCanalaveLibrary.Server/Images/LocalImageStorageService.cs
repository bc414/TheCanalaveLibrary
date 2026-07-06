using System.Diagnostics;
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
public class LocalImageStorageService(
    IWebHostEnvironment env,
    ImageUploadProcessor processor,
    ILogger<LocalImageStorageService> logger) : IImageStorageService
{
    private const string Provider = "local";

    public async Task<string> SaveAsync(Stream content, string contentType, ImageKind kind, int ownerId)
    {
        // Custom span: mirrors S3ImageStorageService — filesystem I/O is invisible to every
        // auto-instrument (logging.md §"Custom Instrumentation").
        using Activity? activity = CanalaveTelemetry.ImageStorage.Source.StartActivity("ImageStorage.Save");
        activity?.SetTag("canalave.image.provider", Provider);
        activity?.SetTag("canalave.image.kind", kind.ToString());

        try
        {
            // Sniff + re-encode + size/dimension caps — the shared hardening step; never write
            // caller-supplied bytes (security.md "Upload Content Pipeline"). The stored
            // extension follows the SNIFFED format, not the browser's claimed content type.
            using ProcessedImage processed = await processor.ProcessAsync(content, contentType);

            string key = ImageUploadRules.BuildKey(kind, ownerId, processed.Extension);

            string fullPath = Path.Combine(env.WebRootPath, "uploads", key.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            long sizeBytes;
            await using (FileStream fileStream = File.Create(fullPath))
            {
                await processed.Content.CopyToAsync(fileStream);
                sizeBytes = fileStream.Length;
            }

            activity?.SetTag("canalave.image.size_bytes", sizeBytes);
            CanalaveTelemetry.ImageStorage.RecordUpload(kind, Provider, sizeBytes);
            logger.LogInformation(
                "Saved {ImageKind} image {ImageKey} ({SizeBytes} bytes) to local uploads",
                kind, key, sizeBytes);

            // Host-relative — never a full URL (spec §3.17 explicitly rejected storing one).
            return ImageUploadRules.UploadsPrefix + key;
        }
        catch (Exception ex)
        {
            // Recorded on the span; not logged here — the exception propagates and is logged
            // (with this scope's trace context) wherever it surfaces (logging.md: no double-log).
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public Task DeleteAsync(string relativePath)
    {
        using Activity? activity = CanalaveTelemetry.ImageStorage.Source.StartActivity("ImageStorage.Delete");
        activity?.SetTag("canalave.image.provider", Provider);

        // TryExtractKey rejects non-/uploads/ values and any ".." traversal segment — the same
        // defensive stance the pre-refactor Path.GetFullPath check provided.
        string? key = ImageUploadRules.TryExtractKey(relativePath);
        if (key is null)
        {
            // Not a value this service family produced — no-op, per the interface contract. Still
            // a data-shape surprise worth a trace (a stored URL nothing here wrote).
            logger.LogWarning(
                "Image delete no-op: {ImagePath} is not an uploads key this service family produces",
                relativePath);
            activity?.SetTag("canalave.image.delete_noop", true);
            return Task.CompletedTask;
        }

        string uploadsRoot = Path.GetFullPath(Path.Combine(env.WebRootPath, "uploads"));
        string fullPath = Path.GetFullPath(Path.Combine(uploadsRoot, key.Replace('/', Path.DirectorySeparatorChar)));

        // Belt-and-suspenders: even with segment validation above, never delete outside uploads/.
        if (!fullPath.StartsWith(uploadsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Image delete no-op: {ImagePath} resolved outside the uploads root", relativePath);
            activity?.SetTag("canalave.image.delete_noop", true);
            return Task.CompletedTask;
        }

        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }

        return Task.CompletedTask;
    }
}
