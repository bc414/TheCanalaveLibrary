namespace TheCanalaveLibrary.Core;

/// <summary>
/// Upload guardrails for chapter import (Feature 63, WU38d — security.md discipline: caps +
/// sniffing on every user-supplied file). Review state lives in circuit memory, so these caps are
/// also what protects the circuit.
/// </summary>
public static class ImportLimits
{
    /// <summary>Per-file upload cap (also passed to InputFile.OpenReadStream).</summary>
    public const long MaxFileBytes = 20 * 1024 * 1024;

    /// <summary>File-count cap for the one-file-per-chapter bulk mode.</summary>
    public const int MaxFilesPerBatch = 100;

    /// <summary>Spine-item cap when reading an EPUB (zip-bomb guard, with MaxFileBytes).</summary>
    public const int MaxEpubChapters = 500;

    /// <summary>Cap on chapter drafts a single split may produce (runaway-delimiter guard).</summary>
    public const int MaxSplitSegments = 500;
}
