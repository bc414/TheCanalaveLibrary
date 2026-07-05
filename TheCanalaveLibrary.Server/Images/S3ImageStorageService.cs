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
public class S3ImageStorageService(IAmazonS3 s3, IOptions<S3ImageStorageOptions> options) : IImageStorageService
{
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
        string extension = ImageUploadRules.ExtensionFor(contentType);

        // Buffer before PutObject: browser upload streams are non-seekable, and an unchunked
        // signed payload needs a known length up front. Buffering also enforces MaxBytes on
        // non-seekable streams (which the Local impl's CanSeek check cannot); the 10 MB cap
        // makes the memory cost fine.
        using MemoryStream buffer = new();
        await CopyWithLimitAsync(content, buffer);
        buffer.Position = 0;

        string key = ImageUploadRules.BuildKey(kind, ownerId, extension);

        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = options.Value.BucketName,
            Key = key,
            InputStream = buffer,
            ContentType = contentType,
            AutoCloseStream = false,       // the using block owns the buffer
            UseChunkEncoding = false,      // R2: no SigV4 streaming — see class doc
        });

        return ImageUploadRules.UploadsPrefix + key;
    }

    public async Task DeleteAsync(string relativePath)
    {
        string? key = ImageUploadRules.TryExtractKey(relativePath);
        if (key is null)
        {
            return; // not a value this service family produced — no-op, per the interface contract
        }

        // S3 DeleteObject is idempotent: deleting a nonexistent key succeeds (204), which
        // matches the interface's "no-ops if not found" contract without a HEAD round-trip.
        await s3.DeleteObjectAsync(options.Value.BucketName, key);
    }

    private static async Task CopyWithLimitAsync(Stream source, MemoryStream destination)
    {
        byte[] rented = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(rented)) > 0)
        {
            destination.Write(rented, 0, read);
            if (destination.Length > ImageUploadRules.MaxBytes)
            {
                throw new ArgumentException(
                    $"Image exceeds the {ImageUploadRules.MaxBytes / (1024 * 1024)} MB limit.", nameof(source));
            }
        }
    }
}
