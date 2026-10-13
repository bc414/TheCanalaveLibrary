using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// A <see cref="LocalImageStorageService"/> over its own temp web root, for tests whose result depends
/// on which files exist (the orphan sweep, a delete cap, "no file was written"). The shared host's
/// <c>WebRootPath</c> is not reset between tests — Respawn resets rows only — so such tests must not
/// use it (testing.md §"Integration test host is shared collection-wide"; WU-BlobCleanup).
/// Construct services over it with <c>ActivatorUtilities.CreateInstance&lt;T&gt;(scope.ServiceProvider,
/// storage, …)</c>. Dispose deletes the folder.
/// </summary>
public sealed class TempBlobStore : IDisposable
{
    public string WebRoot { get; } =
        Path.Combine(Path.GetTempPath(), "canalave-blob-tests", Guid.NewGuid().ToString("N"));

    public LocalImageStorageService Create(IServiceProvider services) =>
        ActivatorUtilities.CreateInstance<LocalImageStorageService>(services, new TempWebHostEnvironment(WebRoot));

    /// <summary>Full file-system path of a stored <c>/uploads/…</c> path.</summary>
    public string FullPath(string relativePath) =>
        Path.Combine(WebRoot, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

    public bool Exists(string relativePath) => File.Exists(FullPath(relativePath));

    /// <summary>Writes a file directly (no upload pipeline) and sets its age; returns the stored path.</summary>
    public string WriteBlob(string key, TimeSpan age)
    {
        string relativePath = ImageUploadRules.UploadsPrefix + key;
        string fullPath = FullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, [1, 2, 3]);
        File.SetLastWriteTimeUtc(fullPath, DateTime.UtcNow - age);
        return relativePath;
    }

    /// <summary>Ages an existing stored file past (or within) the sweep's grace window.</summary>
    public void Age(string relativePath, TimeSpan age) =>
        File.SetLastWriteTimeUtc(FullPath(relativePath), DateTime.UtcNow - age);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(WebRoot)) Directory.Delete(WebRoot, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort temp cleanup — never fail a test over it.
        }
    }

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

/// <summary>
/// Wraps a real store but throws on <see cref="DeleteAsync"/> — the "storage is down" fault the inline
/// delete sites must survive (owner ruling D14: best-effort, Warning, the sweep collects the orphan).
/// Saves and enumeration go through to the inner store.
/// </summary>
public sealed class ThrowingDeleteImageStorageService(IImageStorageService inner) : IImageStorageService
{
    public List<string> DeleteAttempts { get; } = [];

    public Task<string> SaveAsync(Stream content, string contentType, ImageKind kind, int ownerId) =>
        inner.SaveAsync(content, contentType, kind, ownerId);

    public Task DeleteAsync(string relativePath)
    {
        DeleteAttempts.Add(relativePath);
        throw new IOException("Simulated storage outage.");
    }

    public IAsyncEnumerable<StoredImage> EnumerateAsync(CancellationToken cancellationToken = default) =>
        inner.EnumerateAsync(cancellationToken);
}
