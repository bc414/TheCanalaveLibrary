# Audit — Export/

**Feature:** 54 (content download/export). Six-format download, server-side generation. **No schema
impact** (§5.24) — `L1 = N/A`.

## Shared Context
No entities. No components in this folder — the download *trigger* lives in the consuming feature
(Stories: per-format anchor links on the story page + StoryCard caret). Pure application-layer
generation: `IExportService` (Core) + `ServerExportService` + per-format writers + `ExportEndpoints`
(Server), all in the `Export/` cluster.

## Feature 54 — Content Download/Export
- **L1 — N/A** (no schema). **L2 — Stage 2 → building (WU38c, plan approved 2026-07-11).**
- **L3-Logic — N/A** (trigger lives in Stories components). **L3.5 — N/A** (no components).
- **L4 — N/A. L5 — N/A. L6 — N/A. L8 — N/A.**

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
