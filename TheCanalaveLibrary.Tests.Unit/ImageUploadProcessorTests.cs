using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// The upload-hardening pipeline (security.md §"Upload Content Pipeline") exercised directly —
/// no host, no DB (testing.md Unit tier). Fixtures are real encoded images generated in-test
/// (TestImages); the whole point of the pipeline is that fake bytes don't survive it.
/// </summary>
public sealed class ImageUploadProcessorTests
{
    private readonly RecordingWriteRateLimitService _rateLimit = new();
    private readonly StubActiveUserContext _activeUser = new();

    private ImageUploadProcessor BuildSut() =>
        new(_rateLimit, _activeUser, NullLogger<ImageUploadProcessor>.Instance);

    // ── Round trips ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("png", "image/png")]
    [InlineData("jpeg", "image/jpeg")]
    [InlineData("webp", "image/webp")]
    public async Task ProcessAsync_RoundTripsEachAllowedFormat(string fixture, string contentType)
    {
        byte[] bytes = fixture switch
        {
            "png" => TestImages.Png(4, 4),
            "jpeg" => TestImages.Jpeg(4, 4),
            _ => TestImages.Webp(4, 4),
        };
        using MemoryStream content = new(bytes);

        using ProcessedImage result = await BuildSut().ProcessAsync(content, contentType);

        result.ContentType.Should().Be(contentType);
        result.Extension.Should().Be(ImageUploadRules.ExtensionFor(contentType));
        ImageInfo output = Image.Identify(result.Content);
        output.Metadata.DecodedImageFormat!.DefaultMimeType.Should().Be(contentType);
        (output.Width, output.Height).Should().Be((4, 4));
    }

    [Fact]
    public async Task ProcessAsync_OnASpoofedClaimedType_ReturnsTheSniffedFormat()
    {
        // PNG bytes claiming to be JPEG — the rename trick. The sniffed format is authoritative.
        using MemoryStream content = new(TestImages.Png(4, 4));

        using ProcessedImage result = await BuildSut().ProcessAsync(content, "image/jpeg");

        result.ContentType.Should().Be("image/png");
        result.Extension.Should().Be("png");
    }

    // ── Rejections ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_RejectsAClaimedTypeOutsideTheAllowList_BeforeReadingBytes()
    {
        using MemoryStream content = new(TestImages.Png(4, 4));

        Func<Task> act = () => BuildSut().ProcessAsync(content, "image/svg+xml");

        await act.Should().ThrowAsync<ArgumentException>();
        content.Position.Should().Be(0, "the claimed-MIME fast-fail must fire before any byte is read");
    }

    [Fact]
    public async Task ProcessAsync_RejectsBytesThatAreNotAnImage()
    {
        using MemoryStream content = new("this is definitely not an image"u8.ToArray());

        Func<Task> act = () => BuildSut().ProcessAsync(content, "image/png");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ProcessAsync_RejectsARealImageInAFormatOffTheAllowList()
    {
        // A genuine GIF claiming to be PNG: only jpeg/png/webp decoders are configured, so the
        // sniff fails regardless of the claim — GIF stays off the allow-list (security.md).
        using Image<Rgba32> gif = new(4, 4, new Rgba32(90, 160, 120));
        using MemoryStream gifBytes = new();
        gif.SaveAsGif(gifBytes);
        gifBytes.Position = 0;

        Func<Task> act = () => BuildSut().ProcessAsync(gifBytes, "image/png");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ProcessAsync_RejectsAnOversizedNonSeekableStream()
    {
        // Non-seekable, like a real browser upload stream — proves the size cap no longer
        // depends on CanSeek (the pre-WU-Security Local-impl bypass).
        using NonSeekableStream content = new(new byte[ImageUploadRules.MaxBytes + 1]);

        Func<Task> act = () => BuildSut().ProcessAsync(content, "image/png");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ProcessAsync_RejectsADecompressionBombFromTheHeaderAlone()
    {
        // Hand-crafted, structurally valid PNG header claiming 50000x50000 (2.5 GP). The guard
        // must fire from Identify metadata — decoding it would be the attack succeeding.
        using MemoryStream content = new(PngWithClaimedDimensions(50_000, 50_000));

        Func<Task> act = () => BuildSut().ProcessAsync(content, "image/png");

        (await act.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*dimensions*");
    }

    // ── Transformations ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_StripsExifMetadata()
    {
        using MemoryStream content = new(TestImages.JpegWithExif(4, 4));

        using ProcessedImage result = await BuildSut().ProcessAsync(content, "image/jpeg");

        using Image output = Image.Load(result.Content);
        output.Metadata.ExifProfile.Should().BeNull("EXIF (incl. GPS) must not survive re-encode");
        output.Metadata.XmpProfile.Should().BeNull();
        output.Metadata.IptcProfile.Should().BeNull();
    }

    [Fact]
    public async Task ProcessAsync_DownscalesToTheStoredCeiling_PreservingAspect()
    {
        using MemoryStream content = new(TestImages.Png(3000, 100));

        using ProcessedImage result = await BuildSut().ProcessAsync(content, "image/png");

        ImageInfo output = Image.Identify(result.Content);
        output.Width.Should().Be(ImageUploadProcessor.MaxStoredDimension);
        output.Height.Should().BeGreaterThan(0)
            .And.BeLessThan(100, "aspect ratio must be preserved when the long side shrinks");
    }

    // ── Throttle wiring ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_ChecksTheUploadThrottle_ForAnAuthenticatedUser()
    {
        _activeUser.UserId = 42;
        _activeUser.IsAuthenticated = true;
        using MemoryStream content = new(TestImages.Png(4, 4));

        using ProcessedImage _ = await BuildSut().ProcessAsync(content, "image/png");

        _rateLimit.Calls.Should().ContainSingle().Which.Should().Be((WriteActionKind.ImageUpload, 42));
    }

    [Fact]
    public async Task ProcessAsync_SkipsTheThrottle_WhenAnonymous()
    {
        // Real upload UIs are auth-gated; anonymous only occurs on dev-diagnostics probes.
        using MemoryStream content = new(TestImages.Png(4, 4));

        using ProcessedImage _ = await BuildSut().ProcessAsync(content, "image/png");

        _rateLimit.Calls.Should().BeEmpty();
    }

    // ── Support ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// PNG signature + IHDR chunk (with valid CRC) claiming the given dimensions, then a
    /// truncated body — enough for Identify to read the header, never decodable.
    /// </summary>
    private static byte[] PngWithClaimedDimensions(int width, int height)
    {
        using MemoryStream ms = new();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        byte[] ihdr = new byte[17];
        "IHDR"u8.CopyTo(ihdr);
        WriteBigEndian(ihdr, 4, width);
        WriteBigEndian(ihdr, 8, height);
        ihdr[12] = 8;  // bit depth
        ihdr[13] = 6;  // color type RGBA
        ihdr[14] = 0;  // compression
        ihdr[15] = 0;  // filter
        ihdr[16] = 0;  // interlace

        byte[] length = new byte[4];
        WriteBigEndian(length, 0, 13);
        ms.Write(length);
        ms.Write(ihdr);
        byte[] crc = new byte[4];
        WriteBigEndian(crc, 0, unchecked((int)Crc32(ihdr)));
        ms.Write(crc);
        return ms.ToArray();
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    /// <summary>Standard PNG CRC-32 (ISO 3309), bitwise implementation — test-only, tiny inputs.</summary>
    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
        }
        return crc ^ 0xFFFFFFFF;
    }

    private sealed class NonSeekableStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
