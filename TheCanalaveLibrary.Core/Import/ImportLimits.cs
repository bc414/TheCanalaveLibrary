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

    /// <summary>Spine-item cap when reading an EPUB. A spine-length cap, not a bomb guard: neither it
    /// nor <see cref="MaxFileBytes"/> bounds the decompressed size; <see cref="MaxDecompressedBytes"/>
    /// and <see cref="MaxArchiveEntries"/> do (corrected WU-ParityAndRemaining, 2026-10-13).</summary>
    public const int MaxEpubChapters = 500;

    /// <summary>
    /// Cap on the total decompressed bytes of an EPUB or DOCX (<c>ArchiveGuard</c>; security.md
    /// §"Archive imports (EPUB / DOCX)"). 5× <see cref="MaxFileBytes"/>: a million-word story is
    /// about 6 MB of text, so anything near this is not a story.
    /// </summary>
    public const long MaxDecompressedBytes = 100L * 1024 * 1024;

    /// <summary>Cap on the number of entries in an EPUB or DOCX archive (<c>ArchiveGuard</c>).</summary>
    public const int MaxArchiveEntries = 10_000;

    /// <summary>Cap on chapter drafts a single split may produce (runaway-delimiter guard).</summary>
    public const int MaxSplitSegments = 500;
}
