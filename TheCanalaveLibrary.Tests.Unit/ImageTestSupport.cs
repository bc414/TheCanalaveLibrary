using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Real encoded image fixtures generated with ImageSharp at test time — the upload pipeline
/// (ImageUploadProcessor: sniff + decode + re-encode) rightly rejects hand-typed fake byte blobs.
/// Unit-tier twin of Tests.Integration's TestImages.
/// </summary>
public static class TestImages
{
    public static byte[] Png(int width, int height)
    {
        using Image<Rgba32> image = new(width, height, new Rgba32(90, 160, 120));
        using MemoryStream ms = new();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    public static byte[] Jpeg(int width, int height)
    {
        using Image<Rgba32> image = new(width, height, new Rgba32(90, 160, 120));
        using MemoryStream ms = new();
        image.SaveAsJpeg(ms);
        return ms.ToArray();
    }

    public static byte[] Webp(int width, int height)
    {
        using Image<Rgba32> image = new(width, height, new Rgba32(90, 160, 120));
        using MemoryStream ms = new();
        image.SaveAsWebp(ms, new WebpEncoder());
        return ms.ToArray();
    }

    /// <summary>A JPEG carrying EXIF metadata (incl. a GPS-adjacent tag) — for strip assertions.</summary>
    public static byte[] JpegWithExif(int width, int height)
    {
        using Image<Rgba32> image = new(width, height, new Rgba32(90, 160, 120));
        ExifProfile exif = new();
        exif.SetValue(ExifTag.Software, "canalave-test");
        exif.SetValue(ExifTag.GPSAltitude, new Rational(123, 1));
        image.Metadata.ExifProfile = exif;
        using MemoryStream ms = new();
        image.SaveAsJpeg(ms);
        return ms.ToArray();
    }
}

/// <summary>Pass-through limiter for directly-constructed SUTs; records calls for assertions.</summary>
public sealed class RecordingWriteRateLimitService : IWriteRateLimitService
{
    public List<(WriteActionKind Kind, int UserId)> Calls { get; } = [];

    public void EnsureAllowed(WriteActionKind kind, int userId) => Calls.Add((kind, userId));
}

/// <summary>Settable <see cref="IActiveUserContext"/> stand-in for unit-tier construction.</summary>
public sealed class StubActiveUserContext : IActiveUserContext
{
    public int? UserId { get; set; }
    public bool IsAuthenticated { get; set; }
    public bool ShowMatureContent { get; set; }
    public string Theme { get; set; } = "pokemon";
    public bool PrefersAnimatedSprites { get; set; }
    public bool IsModerator { get; set; }
    public bool IsAdmin { get; set; }
}
