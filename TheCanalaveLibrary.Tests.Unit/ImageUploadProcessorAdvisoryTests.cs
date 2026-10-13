using System.Buffers.Binary;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Regression guards for the accepted-risk posture on SixLabors.ImageSharp 3.1.12 (tracker E9;
/// security.md §"Dependency Vulnerability Scan Cadence"). The 2026-10 advisories are fixed only in
/// license-key-gated releases, so the project stays on 3.1.12 because every vector is unreachable
/// through <see cref="ImageUploadProcessor"/>. These tests feed the advisories' own payloads through
/// the pipeline and fail if a later change makes a vector reachable: a TIFF decoder added to the
/// sniff, or the ICC tag table parsed. Unit tier (testing.md): the processor is constructed directly.
/// The class runs in a non-parallel collection because the ICC guard measures process-wide
/// allocation.
/// </summary>
[Collection(AllocationSensitiveCollection.Name)]
public sealed class ImageUploadProcessorAdvisoryTests
{
    private static ImageUploadProcessor BuildSut() =>
        new(new RecordingWriteRateLimitService(), new StubActiveUserContext(),
            NullLogger<ImageUploadProcessor>.Instance);

    [Fact]
    public async Task ProcessAsync_RejectsTheBigTiffIfdLoopPayload_AtTheSniff()
    {
        // GHSA-wmxv-xphr-5c9g's 24-byte PoC: a little-endian BigTIFF whose first IFD declares
        // 5,000,000,000 entries and supplies none. A configured TIFF decoder loops once per declared
        // entry without consuming input. SniffConfiguration (JPEG/PNG/WebP only) must reject it
        // before any TIFF code runs. The deadline turns a regression (the hang) into a failure.
        byte[] bigTiff = new byte[24];
        bigTiff[0] = 0x49; bigTiff[1] = 0x49;   // "II"
        bigTiff[2] = 0x2B;                      // BigTIFF magic
        bigTiff[4] = 0x08;                      // 8-byte offsets
        BinaryPrimitives.WriteUInt64LittleEndian(bigTiff.AsSpan(8), 16);
        BinaryPrimitives.WriteUInt64LittleEndian(bigTiff.AsSpan(16), 5_000_000_000);

        Task<ProcessedImage> processing = Task.Run(() =>
            BuildSut().ProcessAsync(new MemoryStream(bigTiff), "image/png"));
        Func<Task> act = () => processing.WaitAsync(TimeSpan.FromSeconds(10));

        (await act.Should().ThrowAsync<ImageUploadValidationException>(
                "the BigTIFF payload must fail the JPEG/PNG/WebP-only sniff, not reach a TIFF decoder"))
            .WithMessage(ImageUploadProcessor.UnrecognizedMessage);
    }

    [Fact]
    public async Task ProcessAsync_CarriesAMalformedIccProfileAsRawBytes_WithoutParsingIt()
    {
        // GHSA-gwg2-r3hj-4w44's PoC profile: an A2B0 'mpet' tag whose CLUT declares 15 input and
        // 15 output channels at three grid points, then ends. On 3.x, parsing the tag table
        // (IccProfile.Entries) allocates ~115 MB before it notices the truncation. The pipeline
        // keeps ICC for visual fidelity, but only as the original bytes; it never parses them.
        byte[] profile = TruncatedClutIccProfile();
        byte[] png = PngCarrying(profile);

        long before = GC.GetTotalAllocatedBytes(precise: true);
        using ProcessedImage result = await BuildSut().ProcessAsync(new MemoryStream(png), "image/png");
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - before;

        allocated.Should().BeLessThan(48L * 1024 * 1024,
            "a 1x1 upload costs a few MB; parsing the hostile CLUT would cost ~115 MB");
        using Image output = Image.Load(result.Content);
        output.Metadata.IccProfile.Should().NotBeNull("the ICC profile is deliberately kept");
        output.Metadata.IccProfile!.ToByteArray().Should().Equal(profile,
            "the profile passes through as the uploaded bytes, never re-serialized from a parse");
    }

    /// <summary>The advisory's 200-byte ICC v4 RGB profile, verbatim from its PoC.</summary>
    private static byte[] TruncatedClutIccProfile()
    {
        const int tagOffset = 144;
        const int elementOffset = 168;
        byte[] p = new byte[200];
        U32(p, 0, (uint)p.Length);
        Ascii(p, 4, "test");
        U32(p, 8, 0x04000000);
        U32(p, 12, 0x6D6E7472);  // display device
        U32(p, 16, 0x52474220);  // RGB
        U32(p, 20, 0x58595A20);  // XYZ
        Ascii(p, 36, "acsp");
        U32(p, 128, 1);           // one tag
        U32(p, 132, 0x41324230);  // A2B0
        U32(p, 136, tagOffset);
        U32(p, 140, 56);
        U32(p, tagOffset, 0x6D706574);       // mpet
        U32(p, tagOffset + 12, 1);
        U32(p, tagOffset + 16, 24);          // element offset, relative to the tag
        U32(p, tagOffset + 20, 32);
        U32(p, elementOffset, 0x636C7574);   // clut
        U16(p, elementOffset + 4, 15);       // input channels
        U16(p, elementOffset + 6, 15);       // output channels
        for (int i = 0; i < 15; i++)
        {
            p[elementOffset + 8 + i] = 3;    // grid points per input channel; values never follow
        }
        return p;
    }

    private static byte[] PngCarrying(byte[] iccProfile)
    {
        using Image<Rgba32> image = new(1, 1, new Rgba32(90, 160, 120));
        image.Metadata.IccProfile = new IccProfile(iccProfile);
        using MemoryStream ms = new();
        image.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    private static void U32(byte[] buffer, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset, 4), value);

    private static void U16(byte[] buffer, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset, 2), value);

    private static void Ascii(byte[] buffer, int offset, string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            buffer[offset + i] = (byte)value[i];
        }
    }
}

/// <summary>
/// Runs after every parallel test class has finished, alone, so a test can measure process-wide
/// allocation without other classes adding to the count.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AllocationSensitiveCollection
{
    public const string Name = "Allocation-sensitive";
}
