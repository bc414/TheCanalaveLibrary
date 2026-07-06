namespace TheCanalaveLibrary.Server;

/// <summary>
/// The output of <see cref="ImageUploadProcessor.ProcessAsync"/>: clean re-encoded bytes plus the
/// content type / extension of the **sniffed** format (never the browser's claim). Dispose after
/// the bytes are persisted — owns the backing stream.
/// </summary>
public sealed class ProcessedImage(MemoryStream content, string contentType, string extension) : IDisposable
{
    /// <summary>Re-encoded image bytes, positioned at 0, ready to write.</summary>
    public MemoryStream Content { get; } = content;

    public string ContentType { get; } = contentType;

    /// <summary>Extension without the dot (e.g. <c>"png"</c>), for <c>ImageUploadRules.BuildKey</c>.</summary>
    public string Extension { get; } = extension;

    public void Dispose() => Content.Dispose();
}
