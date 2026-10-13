using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// <see cref="LocalImageStorageService.EnumerateAsync"/> (owner ruling D14, WU-BlobCleanup) over a temp
/// web root: it yields stored <c>/uploads/…</c> paths with <c>/</c> separators and the file's
/// last-write time, lists only the upload prefixes (<c>stories/</c>, <c>users/</c>), and yields nothing
/// when nothing has been uploaded. The S3 half is Integration-tested against Garage
/// (<c>S3ImageStorageServiceTests</c>). Tier: Unit.
/// </summary>
public sealed class LocalImageStorageEnumerationTests : IDisposable
{
    private readonly string _webRoot =
        Path.Combine(Path.GetTempPath(), "canalave-enumeration-tests", Guid.NewGuid().ToString("N"));

    // Never call SaveAsync on this SUT: see the note above TempWebHostEnvironment.
    private LocalImageStorageService BuildSut() => new(
        new TempWebHostEnvironment(_webRoot),
        new ImageUploadProcessor(new RecordingWriteRateLimitService(), new StubActiveUserContext(),
            NullLogger<ImageUploadProcessor>.Instance),
        NullLogger<LocalImageStorageService>.Instance);

    public void Dispose()
    {
        if (Directory.Exists(_webRoot)) Directory.Delete(_webRoot, recursive: true);
    }

    private string WriteFile(string relativeUnderWebRoot, DateTime lastWriteUtc)
    {
        string fullPath = Path.Combine(_webRoot, relativeUnderWebRoot.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, [1, 2, 3]);
        File.SetLastWriteTimeUtc(fullPath, lastWriteUtc);
        return fullPath;
    }

    private async Task<List<StoredImage>> EnumerateAllAsync()
    {
        List<StoredImage> items = [];
        await foreach (StoredImage item in BuildSut().EnumerateAsync()) items.Add(item);
        return items;
    }

    [Fact]
    public async Task Enumerate_YieldsStoredPathsWithSlashes_AndTheLastWriteTime()
    {
        DateTime coverTime = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        DateTime avatarTime = new(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        WriteFile("uploads/stories/5/cover-0f8fad5b-d9cb-469f-a165-70867728950e.png", coverTime);
        WriteFile("uploads/users/9/profile-7c9e6679-7425-40de-944b-e07fc1f90ae7.webp", avatarTime);

        List<StoredImage> items = await EnumerateAllAsync();

        items.Should().BeEquivalentTo([
            new StoredImage("/uploads/stories/5/cover-0f8fad5b-d9cb-469f-a165-70867728950e.png", coverTime),
            new StoredImage("/uploads/users/9/profile-7c9e6679-7425-40de-944b-e07fc1f90ae7.webp", avatarTime),
        ]);
        items.Should().OnlyContain(i => i.LastModifiedUtc.Kind == DateTimeKind.Utc);
    }

    [Fact]
    public async Task Enumerate_ListsOnlyTheUploadPrefixes()
    {
        DateTime old = DateTime.UtcNow.AddDays(-3);
        WriteFile("uploads/stories/5/cover-0f8fad5b-d9cb-469f-a165-70867728950e.png", old);
        WriteFile("uploads/stories/5/notes.txt", old); // inside a prefix: listed (the sweep calls it foreign)
        WriteFile("uploads/sprites/pokemon/1.png", old);
        WriteFile("uploads/readme.txt", old);
        WriteFile("sprites/themes/pokemon/static/1.png", old);
        WriteFile("img/default-cover.svg", old);

        List<StoredImage> items = await EnumerateAllAsync();

        items.Select(i => i.RelativePath).Should().BeEquivalentTo([
            "/uploads/stories/5/cover-0f8fad5b-d9cb-469f-a165-70867728950e.png",
            "/uploads/stories/5/notes.txt",
        ]);
    }

    [Fact]
    public async Task Enumerate_WithNoUploadsFolder_YieldsNothing()
    {
        Directory.CreateDirectory(_webRoot);

        (await EnumerateAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Enumerate_WithOnlyOnePrefixFolder_YieldsThatFolder()
    {
        WriteFile("uploads/users/3/profile-7c9e6679-7425-40de-944b-e07fc1f90ae7.jpg", DateTime.UtcNow);

        (await EnumerateAllAsync()).Should().ContainSingle()
            .Which.RelativePath.Should().Be("/uploads/users/3/profile-7c9e6679-7425-40de-944b-e07fc1f90ae7.jpg");
    }

    [Fact]
    public async Task Enumerate_HonorsCancellation()
    {
        WriteFile("uploads/stories/5/cover-0f8fad5b-d9cb-469f-a165-70867728950e.png", DateTime.UtcNow);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        Func<Task> act = async () =>
        {
            await foreach (StoredImage _ in BuildSut().EnumerateAsync(cts.Token)) { }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // No SaveAsync round trip here: SaveAsync emits an ImageStorage.Save span, and
    // ImageStorageTelemetryTests' process-wide ActivityListener (run in parallel) would catch it. The
    // save → enumerate round trip is Integration-covered: BlobCleanupTests saves covers through SaveAsync
    // and the sweep finds every one of them referenced.

    private sealed class TempWebHostEnvironment(string webRoot) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = webRoot;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = webRoot;
        public string EnvironmentName { get; set; } = "Development";
    }
}
