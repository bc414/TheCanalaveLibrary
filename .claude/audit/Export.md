# Audit — Export/

**Feature:** 54 (content download/export). Six-format download, server-side generation. **No schema
impact** (§5.24) — `L1 = N/A`.

## Shared Context
No entities. No components in this folder — the download *trigger* lives in the consuming feature
(Stories: per-format anchor links on the story page + StoryCard caret). Pure application-layer
generation: `IExportService` (Core) + `ServerExportService` + per-format writers + `ExportEndpoints`
(Server), all in the `Export/` cluster.

## Feature 54 — Content Download/Export
- **L1 — N/A** (no schema). **L2 — Stage 5 (WU38c, 2026-07-11; re-verified WU-StoryLifecycle,
  2026-09-30 — nullable publish date, "Not yet published" header; see its Stage note below).**
- **L3-Logic — N/A** (trigger lives in Stories components). **L3.5 — N/A** (no components).
- **L4 — N/A. L4.5 — Stage 5. L5 — N/A. L6 — N/A. L8 — N/A.**

### L2 Stage-5 note (2026-07-11, WU38c)
Built: `Core/Export/` (`ExportFormat`, `StoryExportResult`, `IExportService`) +
`Server/Export/` (`ServerExportService`, six writers, `ExportEndpoints`, shared `ExportDom`
AngleSharp helpers) + additive `IChapterReadService.GetChaptersForExportAsync`. Trigger surfaces:
`StoryDownloadLinks` leaf (SharedUI/Stories — anchor links with the `download` attribute so
Blazor's router doesn't intercept) on the story page (as-built 2026-07-11: `StoryDesktop`/
`StoryMobile`, since merged into `StoryPage` by WU-ResponsiveMerge 2026-07-18) + a StoryCard
caret Download submenu (the dead `OnDownload` EventCallback parameter was removed).
**Verified:**
- **Unit tier** (`ExportWritersTests`, 9 tests): EPUB OCF invariants (mimetype first + stored,
  well-formed XHTML per chapter — `XDocument.Parse`), PDF magic, DOCX opens via
  `WordprocessingDocument` with real heading styles + hyperlink relationships, HTML metadata
  escaping, TXT shape preservation, full MD tag mapping incl. `br` hard breaks.
- **Integration tier** (`ExportServiceTests`, 16 tests): per-format bytes/content-type/slug
  filename against real seeded chapters; **"export = what you can read" both directions** (Mature
  story → null for non-mature viewer, bytes for mature); unpublished chapters excluded; chapter
  order; endpoint 200 + `Content-Disposition: attachment`, unknown format/missing story → 404.
- **Live (curl, 2026-07-11):** all six formats downloaded from the running server for seed story 1
  (5 chapters): correct types, slugified attachment filename
  (`seed-story-five-chapters-alt-version-t.epub`), valid signatures (PK zip / %PDF / mimetype at
  byte 30). RazorComponents tier covers the trigger surfaces (`StoryCardTests` download submenu:
  six anchors, `download` attribute, collapsed-by-default).
- Import round-trips (ContentImportTests) double as writer-fidelity proof: export → import → the
  allowlist content survives byte-for-byte in every text format.

### L2 Stage note (WU-StoryLifecycle, 2026-09-30) — no cell flips; F54 L2 stays 5
Owner ruling D2 made `stories.published_date` nullable (NULL = never published on this site), and
only a story's author can read — so export — a never-published story. `StoryExportModel.PublishDate`
became `DateTime?` (`ServerExportService` passes the value through unchanged) and gained a computed
`PublishedLabel` ("Published MMM d, yyyy" / "Not yet published") plus `FormatPublished(format)` for
Markdown's ISO date. The header line of five writers uses it — DOCX, HTML, Markdown, PDF, TXT; EPUB
carries no publish date and is untouched. HTML needed the change to compile at all (`.ToString(fmt)`
doesn't exist on a nullable). This entry was missing from the WU-StoryLifecycle commit (the change
was recorded only under `audit/Stories.md` F5) and was added by its review fixes the same day.
**Verified:** Unit `ExportWritersTests` — `PublishedLabel_FormatsTheDate_OrSaysNotYetPublished`,
`NullPublishDate_RendersNotYetPublished_InEveryTextWriter` (HTML/TXT/Markdown/DOCX text; PDF proves
only that the writer accepts the null, since its text is compressed),
`Markdown_KeepsItsIsoDateFormat_ForAPublishedStory`. No UI changed, so no browser pass is owed;
L4.5 stays 5.

### Settled (WU38c, 2026-07-11 — do not revisit without Stage-4 diagnosis)
- **Six formats:** EPUB (zero-dep `ZipArchive`), PDF (**QuestPDF**, Community license — free under
  $1M revenue, license set at startup), HTML (string assembly), TXT + Markdown (string transforms
  over one shared AngleSharp DOM walk), DOCX (**Open XML SDK**). **MOBI rejected** — obsolete;
  Kindle ingests EPUB directly. Format set extends behind the `ExportFormat` enum + one writer per
  format.
- **"Export = what you can read":** anyone may export any story they can read — the read services'
  content-rating ceiling (via `IActiveUserContext`) is the only gate; no `[Authorize]`, no
  author-only restriction. Resolves spec §5.24's "download *their* stories" wording against the
  Download caret already present on every StoryCard: the UI intent wins.
- **Download mechanism is a plain `<a href>` to a minimal-API endpoint** (`GET
  /api/stories/{id}/export/{format}`, `Results.File` → `Content-Disposition: attachment`). A Blazor
  Server EventCallback cannot produce a file response (SignalR circuit has no HTTP response);
  the anchor is a real browser GET that carries the auth cookie, so `IActiveUserContext` resolves
  normally. `StoryCard.OnDownload` (dead EventCallback parameter, never wired) is removed in favor
  of anchor links. Convention recorded in `layer2-services.md` §"File Downloads Bypass the Circuit".
- **The sanitizer allowlist is the export fidelity contract:** writers map exactly the 13 allowed
  tags (`p, br, strong, em, u, s, h2, h3, blockquote, ul, ol, li, a`) — what the editor can produce
  is what exports render. Extending the toolbar/allowlist means extending the writers.
- **Chapters read extension is additive:** `GetChaptersForExportAsync(storyId)` on
  `IChapterReadService` (published primary versions, ordered, rating ceiling applied) — recorded in
  `audit/Chapters.md`.

### Open
- None for this WU. PDF *import* is a different feature (see `audit/Import.md`).
