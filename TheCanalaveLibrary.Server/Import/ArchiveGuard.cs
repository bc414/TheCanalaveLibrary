using System.Globalization;
using System.IO.Compression;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// The decompression bound for the two ZIP-container import formats, EPUB and DOCX (security.md
/// §"Archive imports (EPUB / DOCX)"; WU-ParityAndRemaining, 2026-10-13 — service audit §2.12).
/// <see cref="ImportLimits.MaxFileBytes"/> caps the upload, but a compressed size bounds nothing about
/// the decompressed one: a kilobyte-scale entry can inflate to gigabytes, and the parser that reads it
/// holds all of it. Called after the ZIP magic sniff and before any parser.
/// <para>It counts <b>real</b> bytes: every entry is stream-decompressed into a counting sink, and the
/// read stops the moment the running total passes the cap. The central directory's declared sizes are
/// never trusted — the uploader writes them.</para>
/// <para>Public and static with the limits as parameters, so the Unit tier can prove the abort with
/// small numbers instead of a 100 MB fixture.</para>
/// </summary>
public static class ArchiveGuard
{
    /// <summary>
    /// Throws <see cref="ImportException"/> when the archive has more than <paramref name="maxEntries"/>
    /// entries, decompresses to more than <paramref name="maxBytes"/> bytes in total, or cannot be read
    /// as a ZIP at all. Leaves <paramref name="buffered"/> open and rewound to the start.
    /// </summary>
    public static void EnsureWithinLimits(
        MemoryStream buffered,
        string fileName,
        long maxBytes = ImportLimits.MaxDecompressedBytes,
        int maxEntries = ImportLimits.MaxArchiveEntries)
    {
        try
        {
            using ZipArchive archive = new(buffered, ZipArchiveMode.Read, leaveOpen: true);

            if (archive.Entries.Count > maxEntries)
            {
                throw new ImportException(
                    $"\"{fileName}\" holds more than {maxEntries.ToString("N0", CultureInfo.InvariantCulture)} files, " +
                    "which is more than a story needs.");
            }

            byte[] chunk = new byte[81920];
            long total = 0;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                using Stream entryStream = entry.Open();
                int read;
                while ((read = entryStream.Read(chunk, 0, chunk.Length)) > 0)
                {
                    total += read;
                    if (total > maxBytes)
                    {
                        throw new ImportException(
                            $"\"{fileName}\" unpacks to more than " +
                            $"{(maxBytes / (1024 * 1024)).ToString("N0", CultureInfo.InvariantCulture)} MB, which is " +
                            "more than a story needs.");
                    }
                }
            }
        }
        catch (InvalidDataException ex)
        {
            // A corrupt or crafted archive (bad headers, bad deflate data). Same per-file friendly
            // refusal as every other unreadable import; the cause rides along for the log.
            throw new ImportException($"\"{fileName}\" isn't a readable archive — is the file intact?", ex);
        }
        finally
        {
            buffered.Position = 0;
        }
    }
}
