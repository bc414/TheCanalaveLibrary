using System.Diagnostics;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// S3-compatible implementation of <see cref="IImageStorageService"/> — one implementation, two
/// endpoints: Garage in dev (via Aspire), Cloudflare R2 in production (spec §3.17's "same AWS SDK
/// code, different endpoint config", with the wire-format constraints below that make that claim
/// actually hold). Bucket keys follow <see cref="ImageUploadRules.BuildKey"/>; the returned stored
/// value is <c>/uploads/{key}</c> — identical in shape to <see cref="LocalImageStorageService"/>'s,
/// so entity rows never care which provider wrote them. In S3 mode those URLs are served by the
/// <c>/uploads/{**key}</c> endpoint (ImageEndpoints.cs).
///
/// <b>R2 interchangeability constraints (verified 2026-07-05, audit/ImageStorage.md):</b>
/// <list type="bullet">
/// <item><c>UseChunkEncoding = false</c> on every upload — R2 does not implement SigV4 streaming
/// (aws-chunked) payloads, which the SDK otherwise emits by default; unchunked signed payloads
/// work on both R2 and Garage, over both http and https.</item>
/// <item><c>RequestChecksumCalculation/ResponseChecksumValidation = WHEN_REQUIRED</c> — opts out
/// of the SDK v4 "default integrity protections" trailers; keeps one deterministic wire format
/// across providers (Garage ≥ 2.0 would tolerate the defaults; R2's support is header-only).</item>
/// <item><c>ForcePathStyle = true</c> — required for Garage on localhost, harmless on R2.</item>
/// </list>
/// </summary>
public class S3ImageStorageService(
    IAmazonS3 s3,
    IOptions<S3ImageStorageOptions> options,
    ImageUploadProcessor processor,
    ILogger<S3ImageStorageService> logger) : IImageStorageService
{
    private const string Provider = "s3";

    /// <summary>
    /// Builds the one client configuration that works against both Garage and R2. Static and
    /// public so Program.cs (DI singleton) and the integration-test fixture construct byte-for-byte
    /// the same client the app runs — the interchangeability constraints live in exactly one place.
    /// </summary>
    public static AmazonS3Client CreateClient(S3ImageStorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ServiceUrl) ||
            string.IsNullOrWhiteSpace(options.AccessKey) ||
            string.IsNullOrWhiteSpace(options.SecretKey) ||
            string.IsNullOrWhiteSpace(options.BucketName))
        {
            throw new InvalidOperationException(
                $"ImageStorage:Provider is 'S3' but '{S3ImageStorageOptions.SectionName}' is incomplete — " +
                "ServiceUrl, AccessKey, SecretKey, and BucketName are all required.");
        }

        return new AmazonS3Client(
            new BasicAWSCredentials(options.AccessKey, options.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = options.ServiceUrl,
                AuthenticationRegion = options.Region,
                ForcePathStyle = true,
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            });
    }

    public async Task<string> SaveAsync(Stream content, string contentType, ImageKind kind, int ownerId)
    {
        // Custom span: HttpClient instrumentation sees only the raw PUT to the S3 endpoint —
        // nothing names "one cover-image save" as a unit (logging.md §"Custom Instrumentation").
        using Activity? activity = CanalaveTelemetry.ImageStorage.Source.StartActivity("ImageStorage.Save");
        activity?.SetTag("canalave.image.provider", Provider);
        activity?.SetTag("canalave.image.kind", kind.ToString());

        try
        {
            // Sniff + re-encode + size/dimension caps — the shared hardening step; never write
            // caller-supplied bytes (security.md "Upload Content Pipeline"). Also satisfies the
            // unchunked-signed-payload need for a known length up front: the processed buffer is
            // seekable with a fixed size. Stored extension + object ContentType follow the
            // SNIFFED format, not the browser's claimed content type.
            using ProcessedImage processed = await processor.ProcessAsync(content, contentType);

            string key = ImageUploadRules.BuildKey(kind, ownerId, processed.Extension);

            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = options.Value.BucketName,
                Key = key,
                InputStream = processed.Content,
                ContentType = processed.ContentType,
                AutoCloseStream = false,       // the using block owns the buffer
                UseChunkEncoding = false,      // R2: no SigV4 streaming — see class doc
            });

            activity?.SetTag("canalave.image.size_bytes", processed.Content.Length);
            CanalaveTelemetry.ImageStorage.RecordUpload(kind, Provider, processed.Content.Length);
            logger.LogInformation(
                "Saved {ImageKind} image {ImageKey} ({SizeBytes} bytes) via S3",
                kind, key, processed.Content.Length);

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

    public async Task DeleteAsync(string relativePath)
    {
        using Activity? activity = CanalaveTelemetry.ImageStorage.Source.StartActivity("ImageStorage.Delete");
        activity?.SetTag("canalave.image.provider", Provider);

        string? key = ImageUploadRules.TryExtractKey(relativePath);
        if (key is null)
        {
            // Not a value this service family produced — no-op, per the interface contract. Still
            // a data-shape surprise worth a trace (a stored URL nothing here wrote).
            logger.LogWarning(
                "Image delete no-op: {ImagePath} is not an uploads key this service family produces",
                relativePath);
            activity?.SetTag("canalave.image.delete_noop", true);
            return;
        }

        try
        {
            // S3 DeleteObject is idempotent: deleting a nonexistent key succeeds (204), which
            // matches the interface's "no-ops if not found" contract without a HEAD round-trip.
            await s3.DeleteObjectAsync(options.Value.BucketName, key);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
