using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Real encoded image fixtures, generated with ImageSharp at test time. The upload pipeline
/// (ImageUploadProcessor: sniff + decode + re-encode) rightly rejects hand-typed fake byte
/// blobs, so any test that feeds <c>SaveAsync</c> must use bytes that genuinely decode.
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
