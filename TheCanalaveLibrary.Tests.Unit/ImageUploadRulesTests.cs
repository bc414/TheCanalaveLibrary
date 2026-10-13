using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// <see cref="ImageUploadRules.IsGeneratedKey"/> and <see cref="ImageUploadRules.KeyPrefixes"/> — the
/// orphan sweep's scope guard (owner ruling D14, WU-BlobCleanup). The sweep deletes only a key that
/// passes, so the predicate must accept exactly what <see cref="ImageUploadRules.BuildKey"/> emits and
/// nothing else: a too-loose predicate deletes files that are not ours (a sprite in a shared bucket, a
/// hand-placed file), a too-strict one only leaves orphans. Tier: Unit.
/// </summary>
public class ImageUploadRulesTests
{
    [Theory]
    [InlineData(ImageKind.Cover, 1, "jpg")]
    [InlineData(ImageKind.Cover, 999, "png")]
    [InlineData(ImageKind.Cover, int.MaxValue, "webp")]
    [InlineData(ImageKind.ProfilePicture, 7, "jpg")]
    [InlineData(ImageKind.ProfilePicture, 12345, "png")]
    [InlineData(ImageKind.ProfilePicture, 2, "webp")]
    public void IsGeneratedKey_AcceptsWhatBuildKeyEmits(ImageKind kind, int ownerId, string extension)
    {
        string key = ImageUploadRules.BuildKey(kind, ownerId, extension);

        ImageUploadRules.IsGeneratedKey(key).Should().BeTrue(key);
        ImageUploadRules.IsGeneratedKey(ImageUploadRules.TryExtractKey(ImageUploadRules.UploadsPrefix + key))
            .Should().BeTrue("the sweep tests the key extracted from the stored path");
    }

    [Theory]
    // Outside the upload key space.
    [InlineData("sprites/themes/pokemon/static/1.png")]
    [InlineData("themes/stories/1/cover-0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    // Not a guid, or not the lowercase "D" format BuildKey writes.
    [InlineData("stories/1/cover-abc.png")]
    [InlineData("stories/1/cover-0F8FAD5B-D9CB-469F-A165-70867728950E.png")]
    [InlineData("stories/1/cover-0f8fad5bd9cb469fa16570867728950e.png")]
    // The owner is not an int.
    [InlineData("stories/abc/cover-0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    [InlineData("stories/-1/cover-0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    [InlineData("stories/99999999999/cover-0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    [InlineData("stories/9999999999/cover-0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    // The kind does not match the folder.
    [InlineData("users/1/cover-0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    [InlineData("stories/1/profile-0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    // An extension this family never writes, or a doubled one.
    [InlineData("stories/1/cover-0f8fad5b-d9cb-469f-a165-70867728950e.gif")]
    [InlineData("stories/1/cover-0f8fad5b-d9cb-469f-a165-70867728950e.jpeg")]
    [InlineData("stories/1/cover-0f8fad5b-d9cb-469f-a165-70867728950e.png.txt")]
    // Nested deeper, traversal-shaped, or padded.
    [InlineData("stories/1/x/cover-0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    [InlineData("stories/1/../cover-0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    [InlineData(" stories/1/cover-0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    [InlineData("stories/1/cover-0f8fad5b-d9cb-469f-a165-70867728950e.png\n")]
    [InlineData("/uploads/stories/1/cover-0f8fad5b-d9cb-469f-a165-70867728950e.png")]
    [InlineData("")]
    public void IsGeneratedKey_RejectsAnythingElse(string key)
    {
        ImageUploadRules.IsGeneratedKey(key).Should().BeFalse();
    }

    [Fact]
    public void IsGeneratedKey_RejectsNull() =>
        ImageUploadRules.IsGeneratedKey(null).Should().BeFalse();

    [Fact]
    public void KeyPrefixes_CoverEveryKindBuildKeyWrites()
    {
        // A new ImageKind whose folder is not listed would never be enumerated, so its orphans would
        // never be swept; this fails first.
        foreach (ImageKind kind in Enum.GetValues<ImageKind>())
        {
            string key = ImageUploadRules.BuildKey(kind, 1, "png");
            ImageUploadRules.KeyPrefixes.Should().Contain(prefix => key.StartsWith(prefix), key);
        }

        ImageUploadRules.KeyPrefixes.Should().BeEquivalentTo(["stories/", "users/"],
            "the sweep must never list outside the upload key space (sprites may share the bucket)");
    }
}
