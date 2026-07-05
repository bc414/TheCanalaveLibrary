using System.Net;
using Amazon.S3;
using Microsoft.Extensions.Options;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Serves stored <c>/uploads/…</c> image URLs from the S3 bucket when the S3 storage provider is
/// active (mapped conditionally in Program.cs — in Local mode the same URLs are physical files
/// under wwwroot and static-files middleware serves them; this route never shadows a real file
/// because static files run earlier in the pipeline). Keeps the WU12 rule intact: entities store
/// one relative-path shape regardless of provider, and consumers render it directly as
/// <c>&lt;img src&gt;</c>. Keys are validated with the same <see cref="ImageUploadRules"/> guards the
/// write side uses; uuid file names make blobs immutable, hence the aggressive cache header.
/// </summary>
public static class ImageEndpoints
{
    public static void MapImageServingEndpoints(this WebApplication app)
    {
        app.MapGet("/uploads/{**key}", async (
            string key,
            HttpContext http,
            IAmazonS3 s3,
            IOptions<S3ImageStorageOptions> options,
            CancellationToken cancellationToken) =>
        {
            string? safeKey = ImageUploadRules.TryExtractKey(ImageUploadRules.UploadsPrefix + key);
            if (safeKey is null)
            {
                return Results.NotFound();
            }

            // Only extensions the write side can produce are servable — anything else 404s
            // without a bucket round-trip.
            string? contentType = ImageUploadRules.ContentTypeForExtension(
                Path.GetExtension(safeKey).TrimStart('.'));
            if (contentType is null)
            {
                return Results.NotFound();
            }

            try
            {
                var response = await s3.GetObjectAsync(options.Value.BucketName, safeKey, cancellationToken);
                http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                // Results.Stream disposes ResponseStream after writing; GetObjectResponse's own
                // Dispose only wraps that same stream, so nothing leaks.
                return Results.Stream(response.ResponseStream, contentType);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return Results.NotFound();
            }
        });
    }
}
