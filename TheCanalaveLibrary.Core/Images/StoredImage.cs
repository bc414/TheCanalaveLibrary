namespace TheCanalaveLibrary.Core;

/// <summary>
/// One stored upload, as <see cref="IImageStorageService.EnumerateAsync"/> reports it: the stored
/// relative path (the same <c>/uploads/…</c> shape an entity column holds) and the store-reported
/// last-modified time, in UTC. The orphan sweep's grace window reads that time, which is why the
/// enumeration yields this record rather than a bare path (owner ruling D14, WU-BlobCleanup
/// 2026-10-13 — audit/ImageStorage.md "Contract").
/// </summary>
public sealed record StoredImage(string RelativePath, DateTime LastModifiedUtc);
