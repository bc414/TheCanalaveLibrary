using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// The single shared upload-hardening step both <see cref="IImageStorageService"/> implementations
/// consume before any byte is written anywhere — security.md §"Upload Content Pipeline" is the
/// authoritative description; this class is its code mirror. In order: per-user throttle →
/// claimed-MIME fast-fail → buffer with hard size cap (works for non-seekable browser streams) →
/// magic-byte sniff (sniffed format authoritative over the browser's claim) → header-level
/// decompression-bomb guard → decode (first frame only) → EXIF-orientation bake + metadata strip
/// (EXIF/XMP/IPTC removed, ICC kept) → downscale to the stored ceiling → re-encode. Re-encoding is
/// the real security payload: it proves the bytes are a genuine image and discards polyglot
/// payloads and trailing data that survive a signature check alone.
/// </summary>
public class ImageUploadProcessor(
    IWriteRateLimitService writeRateLimit,
    IActiveUserContext activeUser,
    ILogger<ImageUploadProcessor> logger)
{
    /// <summary>Reject either source dimension above this from the header alone (pre-decode).</summary>
    public const int MaxSourceDimension = 16_384;

    /// <summary>Reject total source pixels above this (64 MP ≈ 256 MB decoded worst case).</summary>
    public const long MaxSourcePixels = 64_000_000;

    /// <summary>Stored images are downscaled so their longest side never exceeds this.</summary>
    public const int MaxStoredDimension = 2_048;

    // Only the three allow-listed formats get decoders — anything else (GIF, BMP, TIFF, SVG,
    // polyglots) fails Identify below regardless of what the claimed MIME said.
    private static readonly Configuration SniffConfiguration = new(
        new JpegConfigurationModule(),
        new PngConfigurationModule(),
        new WebpConfigurationModule());

    public async Task<ProcessedImage> ProcessAsync(Stream content, string claimedContentType)
    {
        // Throttle first — cheapest check, and uploads are auth-gated in every real UI path.
        // A null UserId only occurs on dev-diagnostics probes (Development-only endpoints).
        if (activeUser.UserId is int userId)
        {
            writeRateLimit.EnsureAllowed(WriteActionKind.ImageUpload, userId);
        }

        // Claimed-MIME fast-fail: reject types outside the allow-list before reading bytes.
        // Not a security boundary — the sniff below decides the real format.
        ImageUploadRules.ExtensionFor(claimedContentType);

        using MemoryStream buffer = new();
        await CopyWithLimitAsync(content, buffer);

        DecoderOptions decoderOptions = new()
        {
            Configuration = SniffConfiguration,
            // Animated WebP flattens to its first frame — deliberate; animated covers/avatars
            // are not a supported feature (security.md).
            MaxFrames = 1,
        };

        buffer.Position = 0;
        ImageInfo info = SniffOrThrow(decoderOptions, buffer);
        IImageFormat format = info.Metadata.DecodedImageFormat
            ?? throw new ArgumentException("Uploaded file is not a recognized image.", nameof(content));

        // Decompression-bomb guard from header metadata alone — no pixels decoded yet.
        if (info.Width > MaxSourceDimension || info.Height > MaxSourceDimension ||
            (long)info.Width * info.Height > MaxSourcePixels)
        {
            throw new ArgumentException(
                $"Image dimensions {info.Width}x{info.Height} exceed the supported maximum.", nameof(content));
        }

        buffer.Position = 0;
        using Image image = DecodeOrThrow(decoderOptions, buffer);

        // Bake EXIF orientation into the pixels BEFORE stripping metadata — otherwise phone
        // photos render sideways once the orientation tag is gone.
        image.Mutate(x => x.AutoOrient());
        image.Metadata.ExifProfile = null;
        image.Metadata.XmpProfile = null;
        image.Metadata.IptcProfile = null;
        // ICC color profile deliberately kept — visual fidelity, carries no location/identity data.

        if (image.Width > MaxStoredDimension || image.Height > MaxStoredDimension)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(MaxStoredDimension, MaxStoredDimension),
            }));
        }

        string contentType = format.DefaultMimeType;
        string extension = ImageUploadRules.ExtensionFor(contentType);

        MemoryStream output = new();
        try
        {
            await image.SaveAsync(output, EncoderFor(format));
        }
        catch
        {
            await output.DisposeAsync();
            throw;
        }
        output.Position = 0;

        if (!string.Equals(claimedContentType, contentType, StringComparison.OrdinalIgnoreCase))
        {
            // Not an error — the sniffed format is authoritative — but a claimed/actual mismatch
            // is exactly the signal a probing client produces, so leave a trace.
            logger.LogInformation(
                "Upload claimed {ClaimedContentType} but sniffed as {SniffedContentType}; storing the sniffed format",
                claimedContentType, contentType);
        }

        return new ProcessedImage(output, contentType, extension);
    }

    private static ImageInfo SniffOrThrow(DecoderOptions options, Stream buffer)
    {
        try
        {
            return Image.Identify(options, buffer);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            throw new ArgumentException("Uploaded file is not a recognized image.", nameof(buffer), ex);
        }
    }

    private static Image DecodeOrThrow(DecoderOptions options, Stream buffer)
    {
        try
        {
            return Image.Load(options, buffer);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            // Identify passed but the pixel data is corrupt/crafted — same rejection contract.
            throw new ArgumentException("Uploaded file is not a valid image.", nameof(buffer), ex);
        }
    }

    private static IImageEncoder EncoderFor(IImageFormat format) => format switch
    {
        JpegFormat => new JpegEncoder(),
        PngFormat => new PngEncoder(),
        WebpFormat => new WebpEncoder(),
        _ => throw new ArgumentException($"Unsupported image format: {format.Name}", nameof(format)),
    };

    /// <summary>
    /// Buffers the (typically non-seekable) browser stream, aborting past the byte cap — the
    /// single size check for both storage impls (subsumed S3's private copy and Local's
    /// CanSeek-gated check, which silently skipped non-seekable streams).
    /// </summary>
    private static async Task CopyWithLimitAsync(Stream source, MemoryStream destination)
    {
        byte[] chunk = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(chunk)) > 0)
        {
            destination.Write(chunk, 0, read);
            if (destination.Length > ImageUploadRules.MaxBytes)
            {
                throw new ArgumentException(
                    $"Image exceeds the {ImageUploadRules.MaxBytes / (1024 * 1024)} MB limit.", nameof(source));
            }
        }
    }
}
