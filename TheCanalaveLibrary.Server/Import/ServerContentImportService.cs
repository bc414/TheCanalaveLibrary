using Microsoft.Extensions.Logging;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// The one import backend behind all five UI modes (Feature 63, WU38d — audit/Import.md).
/// Pipeline per file: format reader → <see cref="ImportHtmlNormalizer"/> (fidelity: map toward the
/// allowlist, count losses) → <c>IHtmlSanitizationService</c> per draft (trust boundary) →
/// word count. Splitting exists ONLY in the document/EPUB paths; <see cref="ParseSingleAsync"/>
/// can never shred a one-chapter file. Buffers non-seekable uploads to memory under
/// <see cref="ImportLimits.MaxFileBytes"/> and sniffs ZIP magic for the container formats.
/// Stateless — registered as a singleton (the sanitizer it wraps is one too).
/// </summary>
public class ServerContentImportService(
    IHtmlSanitizationService sanitizer,
    ILogger<ServerContentImportService> logger) : IContentImportService
{
    public async Task<ImportedChapterDraft> ParseSingleAsync(Stream file, string fileName, ImportFormat format)
    {
        if (format == ImportFormat.Epub)
        {
            throw new ImportException(
                "An EPUB holds a whole book — import it with the \"chapters from an EPUB\" option instead.");
        }

        using MemoryStream buffered = await BufferAsync(file, fileName);
        (string rawHtml, List<ImportWarning> readerWarnings) = ReadSingleDocument(buffered, fileName, format);

        ImportHtmlNormalizer.Result normalized = ImportHtmlNormalizer.Normalize(rawHtml);
        ImportedChapterDraft draft = CreateDraft(
            TitleFromFirstHeading(normalized.Html),
            normalized.Html,
            readerWarnings.Concat(normalized.Warnings));

        logger.LogInformation(
            "Imported single {ImportFormat} file {FileName}: {WordCount} words, {WarningCount} warnings",
            format, fileName, draft.WordCount, draft.Warnings.Count);
        return draft;
    }

    public async Task<ImportParseResult> ParseDocumentAsync(Stream file, string fileName, ImportFormat format)
    {
        if (format == ImportFormat.Epub)
        {
            throw new ImportException("Use the EPUB import option for .epub files.");
        }

        using MemoryStream buffered = await BufferAsync(file, fileName);
        (string rawHtml, List<ImportWarning> readerWarnings) = ReadSingleDocument(buffered, fileName, format);

        ImportHtmlNormalizer.Result normalized = ImportHtmlNormalizer.Normalize(rawHtml);
        (SplitStrategy suggested, IReadOnlyList<SplitStrategy> available) =
            ChapterSplitter.Suggest(normalized.Html);

        List<ImportWarning> documentWarnings = readerWarnings.Concat(normalized.Warnings).ToList();
        IReadOnlyList<ImportedChapterDraft> drafts = SplitToDrafts(normalized.Html, suggested);

        logger.LogInformation(
            "Imported {ImportFormat} document {FileName}: suggested {SplitStrategy}, {DraftCount} drafts, {WarningCount} warnings",
            format, fileName, suggested, drafts.Count, documentWarnings.Count);

        return new ImportParseResult(normalized.Html, suggested, available, drafts, documentWarnings);
    }

    public async Task<ImportParseResult> ParseEpubAsync(Stream file)
    {
        using MemoryStream buffered = await BufferAsync(file, "upload.epub");
        SniffZipMagic(buffered, "This file isn't an EPUB (not a valid archive).");

        EpubImportReader.EpubContent content = await EpubImportReader.ReadAsync(buffered);

        var drafts = new List<ImportedChapterDraft>();
        foreach (EpubImportReader.EpubChapter chapter in content.Chapters)
        {
            ImportHtmlNormalizer.Result normalized = ImportHtmlNormalizer.Normalize(chapter.BodyHtml);
            drafts.Add(CreateDraft(chapter.Title, normalized.Html, normalized.Warnings));
        }

        if (drafts.Count == 0)
        {
            throw new ImportException("No readable chapters were found in this EPUB.");
        }

        logger.LogInformation("Imported EPUB \"{BookTitle}\": {DraftCount} chapter drafts",
            content.BookTitle ?? "(untitled)", drafts.Count);

        // Spine-defined chapters: no NormalizedHtml, no re-split, no delimiter picker.
        return new ImportParseResult(
            NormalizedHtml: null,
            SuggestedStrategy: SplitStrategy.None,
            AvailableStrategies: [],
            Drafts: drafts,
            Warnings: [],
            BookTitle: content.BookTitle,
            BookAuthor: content.BookAuthor);
    }

    public IReadOnlyList<ImportedChapterDraft> Resplit(ImportParseResult parsed, SplitStrategy strategy)
    {
        if (parsed.NormalizedHtml is null)
        {
            throw new InvalidOperationException(
                "Re-split requires a parsed document; EPUB chapters are spine-defined.");
        }
        return SplitToDrafts(parsed.NormalizedHtml, strategy);
    }

    // ── Pipeline pieces ──────────────────────────────────────────────────────────

    private (string RawHtml, List<ImportWarning> Warnings) ReadSingleDocument(
        MemoryStream buffered, string fileName, ImportFormat format)
    {
        try
        {
            switch (format)
            {
                case ImportFormat.Docx:
                    SniffZipMagic(buffered,
                        "This file isn't a Word document (.docx). If it came from Google Docs, use " +
                        "File → Download → Microsoft Word (.docx).");
                    return DocxReader.Read(buffered);
                case ImportFormat.Html:
                    return (HtmlFileReader.Read(buffered), []);
                case ImportFormat.Txt:
                    return (TxtReader.Read(buffered), []);
                case ImportFormat.Markdown:
                    return (MarkdownReader.Read(buffered), []);
                default:
                    throw new ImportException($"Unsupported import format: {format}.");
            }
        }
        catch (ImportException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Parse failures must never take down the circuit — degrade to a friendly, per-file error.
            logger.LogWarning(ex, "Import parse failed for {FileName} as {ImportFormat}", fileName, format);
            throw new ImportException($"\"{fileName}\" couldn't be read as {format} — is the file intact?", ex);
        }
    }

    private IReadOnlyList<ImportedChapterDraft> SplitToDrafts(string normalizedHtml, SplitStrategy strategy)
    {
        IReadOnlyList<ChapterSplitter.Segment> segments = ChapterSplitter.Split(normalizedHtml, strategy);
        var drafts = new List<ImportedChapterDraft>(segments.Count);
        foreach (ChapterSplitter.Segment segment in segments)
        {
            ImportedChapterDraft draft = CreateDraft(segment.Title, segment.Html, []);
            // Drop an empty untitled front-matter shell (e.g. only a page-break marker survived).
            if (draft.WordCount == 0 && segment.Title is null && segments.Count > 1)
            {
                continue;
            }
            drafts.Add(draft);
        }
        return drafts;
    }

    /// <summary>Sanitize (the trust boundary) + word count + per-draft warnings.</summary>
    private ImportedChapterDraft CreateDraft(
        string? title, string normalizedHtml, IEnumerable<ImportWarning> warnings)
    {
        string sanitized = sanitizer.Sanitize(normalizedHtml);
        int wordCount = ChapterText.CountWords(sanitized);

        var allWarnings = warnings.ToList();
        if (wordCount == 0)
        {
            allWarnings.Add(new ImportWarning(ImportWarningKind.EmptyContent,
                "No readable text was found."));
        }

        return new ImportedChapterDraft(title, sanitized, wordCount, allWarnings);
    }

    private static string? TitleFromFirstHeading(string normalizedHtml)
    {
        foreach (AngleSharp.Dom.INode node in ExportDom.ParseFragment(normalizedHtml))
        {
            if (node is AngleSharp.Dom.IElement { TagName: "H2" or "H3" } heading)
            {
                string text = heading.TextContent.Trim();
                return text.Length > 0 ? text : null;
            }
        }
        return null;
    }

    // ── Upload guards ────────────────────────────────────────────────────────────

    /// <summary>
    /// Buffers the upload to memory (readers need seekable streams; InputFile streams aren't),
    /// enforcing <see cref="ImportLimits.MaxFileBytes"/> while copying — defense in depth beyond
    /// the UI's OpenReadStream cap.
    /// </summary>
    private static async Task<MemoryStream> BufferAsync(Stream file, string fileName)
    {
        if (file.CanSeek && file.Length > ImportLimits.MaxFileBytes)
        {
            throw new ImportException(
                $"\"{fileName}\" is larger than {ImportLimits.MaxFileBytes / (1024 * 1024)} MB.");
        }

        var buffered = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await file.ReadAsync(chunk)) > 0)
        {
            if (buffered.Length + read > ImportLimits.MaxFileBytes)
            {
                buffered.Dispose();
                throw new ImportException(
                    $"\"{fileName}\" is larger than {ImportLimits.MaxFileBytes / (1024 * 1024)} MB.");
            }
            buffered.Write(chunk, 0, read);
        }
        buffered.Position = 0;
        return buffered;
    }

    /// <summary>DOCX and EPUB are ZIP containers — reject non-ZIP bytes before handing to parsers.</summary>
    private static void SniffZipMagic(MemoryStream buffered, string message)
    {
        Span<byte> magic = stackalloc byte[4];
        int read = buffered.Read(magic);
        buffered.Position = 0;
        if (read < 4 || magic[0] != 0x50 || magic[1] != 0x4B || magic[2] != 0x03 || magic[3] != 0x04)
        {
            throw new ImportException(message);
        }
    }
}
