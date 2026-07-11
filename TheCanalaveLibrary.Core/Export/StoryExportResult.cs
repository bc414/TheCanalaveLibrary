namespace TheCanalaveLibrary.Core;

/// <summary>
/// A generated export file, ready to hand to <c>Results.File(...)</c> — bytes, MIME type, and the
/// suggested download filename (slugified title + extension). Small stories at MVP scale make a
/// materialized byte array fine here; a streaming seam can replace this post-MVP if exports ever
/// get large enough to matter.
/// </summary>
public record StoryExportResult(byte[] Content, string ContentType, string FileName);
