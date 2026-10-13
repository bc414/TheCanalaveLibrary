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
  2026-09-30 — nullable publish date, "Not yet published" header; re-verified WU-DependencyCVEs,
  2026-10-12 — the `ExportDom` walks on AngleSharp 1.8.4, all six formats downloaded by its browser
  pass the same day; both download routes carry the `ExportGenerate` concurrency limiter since
  WU-ThrottleCoverage, 2026-10-13, owner ruling D20 — Integration metadata test; see their Stage notes
  below).**
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

### L2 Stage note (WU-DependencyCVEs, 2026-10-12) — no cell flips; F54 L2 stays 5
The Server's explicit AngleSharp reference moved 0.17.1 → **1.8.4**, together with HtmlSanitizer
9.0.892 → 9.2.1039 (tracker E2 closed; the pin rule is in `security.md` §"Dependency Vulnerability
Scan Cadence"). It serves the writers' DOM walks: `ExportDom`, plus the EPUB, Markdown and TXT
writers directly. AngleSharp 1.x needed no code change: there was no compile break, and the
0.17 → 1.x namespace/API-shape fixes the earlier scoping anticipated were never needed. The walks
only parse already-sanitized stored HTML.
**Verified:** Unit `ExportWritersTests` and Integration `ExportServiceTests` pass unchanged on the
new parser, with no serializer diff in any writer's expected output. `ContentImportTests`' export →
import round trips still survive byte-for-byte. No UI changed, so no browser pass is owed; L4.5
stays 5.
**Browser verification (2026-10-12, WU-DependencyCVEs' pass; server-only path).** Story 2, which
then held the sanitized probe chapter 5 (`audit/Chapters.md` F6 L2's browser note), was downloaded
anonymously in all six formats: each answered 200 with its content type. The HTML, EPUB
(`chapter-3.xhtml`), Markdown and TXT bodies keep the bold, italic, underline, strike, link and
quote and hold no `img`/`svg`/`math`/`script`; the DOCX keeps the bold run; the PDF starts
`%PDF-1.4`. Anonymous downloads of a Draft, a pending, an M-rated and an unpublished chapter
answered 404, as did a numeric format segment. Feeding the EPUB back in is `audit/Import.md` F63's
round trip.

### L2 Stage note (WU-ThrottleCoverage, 2026-10-13; owner ruling D20) — no cell flips; F54 L2 stays 5
D20: "exports are the concurrency-limiter class, not the token bucket". Both `MapGet`s in
`ExportEndpoints` now carry `.RequireRateLimiting("ExportGenerate")`, a server-wide concurrency limiter
registered in `Program.cs` beside `"ImportParse"` and mirroring it (4 permits, queue 8, oldest first —
the owner's "mirroring"). Generating a document is expensive (QuestPDF runs synchronously on the
request) and never commits, so no token bucket sees it. Unpartitioned, so anonymous exporters are
bounded too; export stays unauthenticated ("export = what you can read", settled below). A rejection
is the edge limiters' plain-text 429, which an anchor download shows as text — accepted.
**Verified:** Integration `ExportServiceTests` — `ExportRoutes_CarryTheExportGenerateLimiter` (both
routes' `EnableRateLimitingAttribute` names the policy, read off `EndpointDataSource`), plus the
same assertion for the three `ImportParse` routes and `/resplit`'s absence (an untested precedent
until now). A behavioural concurrency test is timing-dependent and deliberately not attempted. The
existing endpoint tests (200 with attachment, 404s) pass through the limiter unchanged. No UI changed;
L4.5 stays 5. Service audit §2.5's export row is closed.
**Browser smoke (2026-10-13, tracker H33 step 5; L4.5 stays 5):** behind the limiter, all six formats
(EPUB, PDF, HTML, TXT, Markdown, DOCX), whole-story and chapter 1, answered 200 with their content types:
anonymously over `curl` (story 1) and signed in as TestUser through the page's own `fetch` (stories 1,
4 (M, mature on) and 11). Forty concurrent anonymous PDF requests all answered 200: on localhost a
seed story renders faster than the queue fills, so the limiter's refusal is not reachable by hand.

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
- None. The throttling gap (service audit §2.5's export row) closed with WU-ThrottleCoverage
  (2026-10-13). PDF *import* is a different feature (see `audit/Import.md`).
