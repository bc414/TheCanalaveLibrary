using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Service audit §2.12's two import defects (WU-ParityAndRemaining, 2026-10-13):
/// <list type="bullet">
/// <item><b>Archive bound</b> (security.md §"Archive imports (EPUB / DOCX)"). <see cref="ArchiveGuard"/>
/// counts real decompressed bytes and entries, with small limits passed in so no 100 MB fixture is
/// needed; the service-level cases prove both container paths call it with the production limits. The
/// existing <c>ContentImportTests</c> round-trips prove legitimate EPUB/DOCX files still parse.</item>
/// <item><b>Legacy encoding</b> (layer2-services.md §"Export &amp; Import" → "Text decoding").
/// <see cref="ImportTextDecoder"/> honors a BOM, then strict UTF-8, then Windows-1252 with an
/// <see cref="ImportWarningKind.EncodingFallback"/> warning; the readers pass that warning through.
/// Before it, a Notepad file's curly quotes became U+FFFD with no warning.</item>
/// </list>
/// Tier: Unit (directly constructed, no host/DB).
/// </summary>
public class ImportArchiveAndEncodingTests
{
    private static ServerContentImportService Service() =>
        new(new ServerHtmlSanitizationService(), NullLogger<ServerContentImportService>.Instance);

    // ── ArchiveGuard ─────────────────────────────────────────────────────────────

    [Fact]
    public void AnEntryInflatingPastTheCap_IsRefused_ByItsRealBytes()
    {
        // 2 MB of zeros deflates to a few kilobytes: the compressed size says nothing.
        using MemoryStream zip = Zip(("chapter.xhtml", new byte[2 * 1024 * 1024]));
        zip.Length.Should().BeLessThan(64 * 1024, "the bomb shape: tiny archive, large payload");

        Action act = () => ArchiveGuard.EnsureWithinLimits(zip, "bomb.epub", maxBytes: 1024 * 1024);

        act.Should().Throw<ImportException>().WithMessage("*unpacks to more than 1 MB*");
    }

    [Fact]
    public void ManySmallEntriesInflatingPastTheCap_TogetherAreRefused()
    {
        using MemoryStream zip = Zip(
            ("a.xhtml", new byte[600 * 1024]), ("b.xhtml", new byte[600 * 1024]));

        Action act = () => ArchiveGuard.EnsureWithinLimits(zip, "two.epub", maxBytes: 1024 * 1024);

        act.Should().Throw<ImportException>("the cap is on the running total, not per entry");
    }

    [Fact]
    public void MoreEntriesThanTheCap_IsRefused()
    {
        using MemoryStream zip = Zip(Enumerable.Range(0, 4).Select(i => ($"e{i}.txt", new byte[1])).ToArray());

        Action act = () => ArchiveGuard.EnsureWithinLimits(zip, "many.docx", maxEntries: 3);

        act.Should().Throw<ImportException>().WithMessage("*more than 3 files*");
    }

    [Fact]
    public void AnArchiveWithinTheLimits_Passes_AndIsRewound()
    {
        using MemoryStream zip = Zip(("a.xhtml", new byte[1000]), ("b.xhtml", new byte[1000]));
        zip.Position = 17;

        ArchiveGuard.EnsureWithinLimits(zip, "fine.epub", maxBytes: 2000, maxEntries: 2);

        zip.Position.Should().Be(0, "the parser reads the archive next");
        zip.CanRead.Should().BeTrue("the guard leaves the caller's stream open");
    }

    [Fact]
    public void ACorruptArchive_IsAFriendlyImportException()
    {
        // ZIP magic, then garbage: passes the sniff, fails the read.
        using MemoryStream bogus = new([0x50, 0x4B, 0x03, 0x04, .. Enumerable.Repeat((byte)0xAB, 200)]);

        Action act = () => ArchiveGuard.EnsureWithinLimits(bogus, "broken.epub");

        act.Should().Throw<ImportException>().WithMessage("*isn't a readable archive*");
    }

    [Fact]
    public async Task TheEpubPath_RunsTheGuard_WithTheProductionEntryCap()
    {
        using MemoryStream zip = Zip(Enumerable.Range(0, ImportLimits.MaxArchiveEntries + 1)
            .Select(i => ($"e{i}", Array.Empty<byte>())).ToArray());

        Func<Task> act = () => Service().ParseEpubAsync(zip);

        (await act.Should().ThrowAsync<ImportException>()).Which.Message.Should().Contain("more than 10,000 files");
    }

    [Fact]
    public async Task TheDocxPath_RunsTheGuard_WithTheProductionEntryCap()
    {
        using MemoryStream zip = Zip(Enumerable.Range(0, ImportLimits.MaxArchiveEntries + 1)
            .Select(i => ($"e{i}", Array.Empty<byte>())).ToArray());

        Func<Task> act = () => Service().ParseSingleAsync(zip, "huge.docx", ImportFormat.Docx);

        (await act.Should().ThrowAsync<ImportException>()).Which.Message.Should().Contain("more than 10,000 files");
    }

    // ── ImportTextDecoder ────────────────────────────────────────────────────────

    [Fact]
    public void Windows1252Bytes_DecodeAsWindows1252_WithTheFallbackWarning()
    {
        // 0x93/0x94 are Windows-1252 curly quotes and invalid as UTF-8.
        byte[] bytes = [0x93, (byte)'h', (byte)'i', 0x94, (byte)' ', 0x96, (byte)' ', 0xE9];

        (string text, ImportWarning? warning) = ImportTextDecoder.Decode(new MemoryStream(bytes));

        text.Should().Be("“hi” – é");
        warning.Should().Be(new ImportWarning(ImportWarningKind.EncodingFallback, ImportTextDecoder.EncodingFallbackMessage));
    }

    [Fact]
    public void ValidUtf8_DecodesWithNoWarning()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("“hi” é");

        (string text, ImportWarning? warning) = ImportTextDecoder.Decode(new MemoryStream(bytes));

        text.Should().Be("“hi” é");
        warning.Should().BeNull();
    }

    [Fact]
    public void Utf8WithABom_DecodesWithNoWarning_AndNoBomCharacter()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("café")];

        (string text, ImportWarning? warning) = ImportTextDecoder.Decode(new MemoryStream(bytes));

        text.Should().Be("café");
        warning.Should().BeNull();
    }

    [Fact]
    public void Utf16WithABom_IsHonored()
    {
        byte[] bytes = [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes("“quoted”")];

        (string text, ImportWarning? warning) = ImportTextDecoder.Decode(new MemoryStream(bytes));

        text.Should().Be("“quoted”");
        warning.Should().BeNull("a byte-order mark is an explicit declaration");
    }

    [Theory]
    [InlineData(ImportFormat.Txt)]
    [InlineData(ImportFormat.Markdown)]
    [InlineData(ImportFormat.Html)]
    public async Task EveryTextReader_PassesTheFallbackWarningToTheDraft(ImportFormat format)
    {
        byte[] bytes = [.. Encoding.ASCII.GetBytes("<p>She said "), 0x93, .. Encoding.ASCII.GetBytes("hello"), 0x94, .. Encoding.ASCII.GetBytes(".</p>")];

        ImportedChapterDraft draft = await Service().ParseSingleAsync(new MemoryStream(bytes), "legacy", format);

        draft.Html.Should().Contain("“hello”");
        draft.Warnings.Should().ContainSingle(w => w.Kind == ImportWarningKind.EncodingFallback);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static MemoryStream Zip(params (string Name, byte[] Content)[] entries)
    {
        MemoryStream ms = new();
        using (ZipArchive archive = new(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, byte[] content) in entries)
            {
                using Stream entry = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
                entry.Write(content);
            }
        }
        ms.Position = 0;
        return ms;
    }
}
