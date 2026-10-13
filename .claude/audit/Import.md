# Audit — Import/

**Feature:** 63 (chapter import / file ingestion). Added 2026-07-11, split out of the Feature-53
reframe (see `audit/Moderation.md` Feature 53). Origin: the WU38c export planning session surfaced
that file-format content ingestion existed nowhere — spec, grid, or code. The Gemini archive
(`GeminiDiscussions/MyActivity September to November 2025_filtered.md` ~17334) holds a single
undeveloped bullet ("bring stories from FFN/AO3/Wattpad/Docs/Word via file upload, not scraping")
and zero parsing/UX deliberation. For a 20+-year-old community migrating from other sites, Word
docs, and Google Docs, import is essential authoring infrastructure; UX bar is top-tier.

## Shared Context
`Import/` cluster: Core contracts (`ImportFormat`, `ImportedChapterDraft`, `ImportWarning`,
`SplitStrategy`, `ImportParseResult`, `ResplitRequest`, `ImportLimits`, `IContentImportService`),
Server readers + splitter (`ServerContentImportService`, `DocxReader`, `EpubImportReader`,
`HtmlFileReader`, `TxtReader`, `MarkdownReader`, `ChapterSplitter`, `ImportHtmlNormalizer`,
`ArchiveGuard` and `ImportTextDecoder` (both WU-ParityAndRemaining, 2026-10-13), `ContentImportEndpoints`), SharedUI mode/review components (`StoryChapterImport` — entry + mode
selection, `ImportReviewPanel`, `ChapterFileImport`). **No entities** — imported chapters are
ordinary chapters created through `IChapterWriteService`; external story links belong to
Feature 53. *(As-built names corrected 2026-07-27; the build note below always had them right.)*

## Feature 63 — Chapter Import (file ingestion)
- **L1 — N/A** (no schema; commits go through existing Chapters tables).
- **L2 / L3-Logic / L3.5 / L4 — Stage 5 (archive decompression bound, legacy-encoding fallback with a
  warning, and the WASM re-split's camelCase read, WU-ParityAndRemaining 2026-10-13 — Unit-tested, stage
  unchanged, the Stage note below; WU38d, 2026-07-11; L2's readers re-verified on AngleSharp
  1.8.4 + HtmlSanitizer 9.2.1039 by WU-DependencyCVEs, 2026-10-12, and browser-driven on both render
  phases by its browser pass the same day — Stage note below; tracker H25 holds two pre-existing
  editor defects that pass found; the three parse routes' `ImportParse` limiter is pinned by an Integration metadata test since WU-ThrottleCoverage, 2026-10-13, which also filed tracker D16 — an import of more than five chapters commits five and then refuses; Stage note below).** **L4.5 —
  Stage 5** (see note).
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13 — reclassified from N/A; the flip makes every injected
  service need a client impl; the re-split's sync read binds camelCase since WU-ParityAndRemaining,
  2026-10-13 — Unit-tested, stage unchanged, the L2/L5 Stage note below).** Endpoints + client impl live (WU-L5Sweep), incl. the three
  multipart parse endpoints + `Resplit` — which stays a real network hop despite its
  synchronous/pure look (server-only `IHtmlSanitizationService` dependency; see WU-L5Sweep's Import
  notes). Not browser-driven in the flip's wave. Full wave narrative: `workplan.md` WU-GlobalFlip.
  **L6/L8 — N/A.**

### Stage-5 note (2026-07-11, WU38d)
Built: `Core/Import/` contracts; `Server/Import/` (`ServerContentImportService` singleton,
`ImportHtmlNormalizer`, `DocxReader` (Mammoth style map incl. `br[type='page'] => hr` — **the
PageBreak strategy verified working**, the plan's "drop if unsupported" caveat is closed),
`EpubImportReader` (VersOne), `HtmlFileReader`, `TxtReader`, `MarkdownReader`, pure
`ChapterSplitter`); `SharedUI/Import/` (`ChapterFileImport`, `StoryChapterImport`,
`ImportReviewPanel`); mode 1/2 wiring in `ChapterEditorPage`, bulk section in `StoryEditorPage`
(edit mode). **Key normalizer rationale recorded in code:** the sanitizer drops disallowed
elements *with their children*, so raw reader HTML must be normalized toward the allowlist first
(b→strong, h1→h2, h4+→h3, containers unwrapped, tables flattened with warnings, images counted +
dropped) — the sanitizer still runs on every draft as the trust boundary.
**Verified:**
- **Unit tier** (`ContentImportTests`, 18 tests): **round-trips through the WU38c export writers**
  (DOCX whole-story split at chapter headings with content preserved incl. `u => u`; PageBreak
  strategy availability; single-file never splits; HTML exact allowlist markup; Markdown rebuild;
  TXT paragraphs; EPUB spine+nav titles + book metadata); splitter (boundary consumption, front
  matter, "Chapter N" prose guard, suggestion ladder); normalizer (mappings, image/table
  warnings, script drop); guards (EPUB-in-single-mode rejection, non-ZIP docx with the
  Google-Docs-teaching message, oversize, hostile-HTML sanitization).
- **Integration tier** (`ImportCommitTests`): real EPUB → `ParseEpubAsync` → `CreateChapterAsync`
  loop → unpublished chapters, correct order, real word counts.
- **RazorComponents tier** (`ImportReviewPanelTests`, 8 tests): rows, warnings, remove, merge
  (content+word-count concat), reorder, commit order + edited titles, preview toggle, empty state.
- **Browser (2026-07-11, real circuit):** mode 4 driven end-to-end with a DOCX exported by WU38c —
  suggested TopHeading split, 6 drafts (droppable title page + 5 titled chapters), delimiter
  switch to PageBreak re-split live (6 segments) and back, commit created unpublished chapters
  with word counts (psql-confirmed; test drafts then removed from the workbench story). Mode 1
  driven with an HTML file — draft loaded into the chapter-text Quill with `<strong>`/headings
  intact, title auto-filled from the first heading. Modes 3/5 and mode 2 were not separately
  browser-driven — they differ from the driven paths only in InputFile wiring / one service call
  (`AddAlternateVersionAsync`, Integration-covered since WU17) and share the entire parse → review
  → commit pipeline exercised above.
- **Outstanding manual item (not a gate):** paste-from-Word fidelity into Quill — needs a real
  Word clipboard on a human machine; Quill normalization + sanitize-on-save is expected adequate
  (audit/Import.md "Settled"). Record findings here when Brian tries it; a Quill paste matcher
  becomes a follow-up only if inadequate.
- **Token fix (WU-TokenGreen, 2026-07-26):** `ImportReviewPanel`'s expanded draft preview rendered
  `RichTextView` in a bare bordered div — the role system's UGC-on-ContentSurface rule
  (`layer4-style.md` §"Element Roles") applies to previews too; drafts are user prose. Now wrapped
  in `<ContentSurface Variant="Inline">`, with the outer div reduced to the scroll constraint only
  (`max-h-64 overflow-y-auto`) — ground/frame/padding belong to the surface. This was one of the
  two findings that had kept `scripts/check-design-tokens.ps1` red repo-wide since WU38d shipped;
  green again as of this fix. `ImportReviewPanelTests` (incl. preview toggle) still pass unchanged.

### L2 Stage note (WU-DependencyCVEs, 2026-10-12) — no cell flips; F63 L2 stays 5
The import path's two AngleSharp consumers moved together (tracker E2 closed; pin rule in
`security.md` §"Dependency Vulnerability Scan Cadence"):
- the readers' own DOM work (`ImportHtmlNormalizer`, `ChapterSplitter`, `HtmlFileReader`,
  `EpubImportReader`) went from AngleSharp 0.17.1 to **1.8.4**;
- the trust boundary every reader converges on (`IHtmlSanitizationService`) went from HtmlSanitizer
  9.0.892 to 9.2.1039.

This matters most here: import is where *wild* HTML (AO3/Calibre EPUBs, arbitrary HTML files) meets
the parser, and both cleared advisories are parse/serialize mXSS (CVE-2026-54570; GHSA-cgp3-27rh-pcp2,
which 1.8.4 clears and 1.7.2 would not). No code change was needed.
**Verified:** Unit `ContentImportTests` and Integration `ImportCommitTests` pass unchanged on the
new parser, including the export → import round trips. The sanitizer's new mXSS payload tests are
in `audit/Chapters.md` F6 L2's Stage note. No UI changed, so no browser pass is owed; L4.5 stays 5.

**Browser verification (2026-10-12, WU-DependencyCVEs' pass; server-only path).** Every reader the
WU moved was driven through the real import UIs, with `psql` after each commit:
- **One document, HTML (circuit, AuthorAlpha, story 2).** The advisory-payload file split at its
  two `<h1>`s. Switching the split to "Don't split" and back re-split in memory (1 ↔ 2 drafts), and
  the panel warned "2 images dropped". The commit created chapters 5 and 6, sanitized; the payload
  detail is in `audit/Chapters.md` F6 L2's browser note.
- **EPUB round trip (WASM).** Story 2's own EPUB export, fed back into the EPUB mode of story 10:
  `POST /api/content-import/epub` answered 200 and showed four drafts (the title page and three
  chapters). With three removed, the commit (`POST /api/chapters`) created story 10's chapter 1
  (id 21). Its body equals the source chapter's apart from the EPUB's own chapter heading, which
  import keeps as an `<h2>`.
- **Into editor, Markdown (circuit).** `ChapterFileImport` parsed a Markdown file holding both
  payloads ("1 image dropped"), "Replace editor content" loaded it, and the save stored the payloads
  as escaped text.
- **Refusals.** A non-ZIP `.epub` read "This file isn't an EPUB (not a valid archive)." (400 on
  WASM). Another author (AuthorBeta) on `/story/2/edit` got "You don't have permission to edit this
  story." Over HTTP, AuthorBeta's `POST /api/chapters` into story 2 answered 403 and wrote nothing;
  an anonymous parse answered 401.
- **Two pre-existing defects found, both in the editor rather than the readers:** tracker **H25**.
  The Markdown import's blockquote lost its quote and gained empty paragraphs once Quill loaded it.
  Saving the draft chapter then landed the author on "Page not found".

### L2/L5 Stage note (WU-ParityAndRemaining, 2026-10-13) — no cell flips; F63 stays 5

Service audit §2.12 (two import items) and §2.7.4.
- **Archive bound (EPUB and DOCX).** New `Server/Import/ArchiveGuard.EnsureWithinLimits`, called after
  the ZIP magic sniff on both container paths: more than `ImportLimits.MaxArchiveEntries` (10 000)
  entries, or more than `ImportLimits.MaxDecompressedBytes` (100 MB) of **real** decompressed bytes
  (stream-counted; declared sizes are never trusted), is an `ImportException`, and so is a corrupt
  archive. `MaxEpubChapters`' doc no longer calls itself a zip-bomb guard (the Settled bullet above is
  corrected too). Rule: `security.md` §"Archive imports (EPUB / DOCX)".
- **Legacy encodings.** New `Server/Import/ImportTextDecoder`: BOM, then strict UTF-8, then Windows-1252
  with the new `ImportWarningKind.EncodingFallback` (appended last; the enum travels by number). The TXT,
  HTML and Markdown readers now return `(Html, Warnings)` like `DocxReader`, and `ReadSingleDocument`
  passes the warnings through; `ImportReviewPanel` renders `Message`, so no UI change. Before, a
  Notepad file's curly quotes became U+FFFD with no warning. Rule: `layer2-services.md` §"Export &
  Import" → "Text decoding".
- **L5 — the WASM re-split.** `ClientContentImportService.Resplit` read its JSON with the case-sensitive
  default options, so the server's camelCase `title`/`html` never bound and every re-split draft came
  back blank on WASM; its 400/401 detail never bound either. It now reads through
  `ClientHttpHelpers.ReadNullableFromJson`/`ReadProblemDetail` (the sync twins of the async helpers,
  same web options and payload); the private `ProblemPayload` copy is gone. Rule: `layer5-wasm.md`
  §"Client Service Implementations" ("Every JSON read uses the web defaults").
- Tier: Unit — `ImportArchiveAndEncodingTests` (14: the byte cap by real bytes and across entries, the
  entry cap, rewind, a corrupt archive, both service paths with the production entry cap, four decoder
  cases, and all three text readers passing the warning to the draft) and `WasmParityClientTests` (the
  re-split binds camelCase; 400 and 401 details). The existing EPUB/DOCX round-trips in
  `ContentImportTests` prove legitimate archives still parse. Mutation-checked: dropping the EPUB
  guard call, dropping a reader's warning, and the default-options re-split each fail a test. No
  browser was available: a WASM re-split is a step of tracker **H37**; no markup changed, so L4.5
  stays 5.

### L2 Stage note (WU-ThrottleCoverage, 2026-10-13) — no cell flips; F63 stays 5
- **Pinned, not changed:** `ExportServiceTests.ImportParseRoutes_CarryTheImportParseLimiter` (Integration)
  asserts the three parse routes carry the `"ImportParse"` concurrency limiter and `/resplit` none — the
  precedent owner ruling D20's `ExportGenerate` mirrors, untested until now. `ContentImportEndpoints`'
  comment now cites `security.md` §"Write Throttling" (the section it named never existed).
- **Found, not fixed (tracker D16, a design choice):** the commit loops `CreateChapterAsync`, and each
  call spends a `ContentCreate` token (5 burst, then 1 per 2 min). An import of more than five
  chapters commits five, then shows "wait N seconds" with the drafts still listed — so committing
  again duplicates the first five. `ImportCommitTests` (2 chapters) runs on the pass-through limiter
  and cannot see it.

### Settled (WU38d, 2026-07-11 — do not revisit without Stage-4 diagnosis)
- **Reframe:** fundamentally *chapter* import / bulk chapter import, NOT "story import." Story
  shell is created via the normal `StoryEditorPage` flow first; import attaches chapters to a
  story (bulk modes) or content to an editor (single modes). Imported chapters are **unpublished
  drafts** via `CreateChapterAsync` — publish → approval flow untouched.
- **Formats:** DOCX (Word; a downloaded Google Doc IS a .docx — same parser), EPUB (AO3 direct
  export / FFN via FicHub), HTML (AO3 exports it; nearly free), TXT, Markdown. **PDF deferred** —
  positioned glyphs, not structure; cannot meet the formatting-preservation requirement; EPUB/HTML
  cover the AO3/FFN exit paths. Drop-in later behind `ImportFormat`.
- **Formatting preservation contract:** every reader converges on HTML and passes through
  `IHtmlSanitizationService` — the 13-tag allowlist is the single trust boundary AND the fidelity
  target. Unrepresentable formatting is stripped **with a warning** surfaced to the author
  (e.g. "3 images dropped" — the allowlist has no `img`).
- **Libraries:** Mammoth (BSD-2; semantic docx→HTML, style-mapped to the allowlist), VersOne.Epub
  (wild-file robustness — we generate clean EPUBs, AO3/Calibre files are messier), AngleSharp
  (explicit ref; 1.8.4 since 2026-10-12, version rule in `security.md` pin rules), Markdig (BSD-2).
- **Explicit modes over one common backend** — the user declares intent; auto-detect splitting runs
  ONLY in mode 4, so a single-chapter doc can never be accidentally shredded:
  1. **Into editor** — single file → `EditorView.SetHtmlAsync`; nothing committed.
  2. **As new version** — single file → `AddAlternateVersionAsync` on an existing chapter.
  3. **One file per chapter** — multi-select upload, natural-sort order (numeric-aware), title
     from filename unless the file's first heading provides one.
  4. **One file, many chapters** — suggest-then-refine: parse once, show suggested split,
     delimiter picker re-splits live in memory (no re-upload), then rename/merge/drop/reorder.
  5. **EPUB** — spine items = chapter drafts; no delimiter UI; front matter dropped in review.
- **Split suggestion logic:** headings present → lowest heading level yielding >1 segment; else
  "Chapter N"-like standalone paragraphs; else no split. Front matter before the first delimiter
  becomes segment 0 (droppable). `PageBreak` strategy only if Mammoth exposes docx page breaks
  (verify at build; drop the option if not).
- **Security:** per-file cap + multi-file count cap; extension AND magic-byte sniff (mirrors
  `ImageUploadProcessor` discipline, `security.md`); parse failures are caught per-file — never a
  circuit crash. Review state lives in circuit memory (no temp storage). *Correction
  (WU-ParityAndRemaining, 2026-10-13):* this bullet used to list "EPUB zip-bomb guards". There were
  none: `MaxEpubChapters` caps the spine and `MaxFileBytes` the upload, and neither bounds the
  decompressed size. The archive guard (entry cap + real decompressed-byte cap, EPUB **and** DOCX) is
  now `ArchiveGuard`, rule in `security.md` §"Archive imports (EPUB / DOCX)".
- **Text encoding (WU-ParityAndRemaining, 2026-10-13):** TXT/HTML/Markdown decode BOM → strict UTF-8
  → Windows-1252 with an `EncodingFallback` warning; never silently garbled
  (`layer2-services.md` §"Export & Import").
- **Paste-from-Word is a verification item, not a build item** — Quill clipboard normalization +
  sanitize-on-save should already cover it; record fidelity during the browser pass; a Quill paste
  matcher becomes its own follow-up only if inadequate.
- **Rejected:** URL scraping from AO3/FFN (explicitly "file upload, not scraping" per the Gemini
  deliberation — respects other sites); auto-detecting author's notes into
  `TopAuthorsNote`/`BottomAuthorsNote` (too heuristic — authors cut/paste after import).

### Open
- **Tracker D16** (WU-ThrottleCoverage, 2026-10-13): an import of more than five chapters partially
  commits under the `ContentCreate` bucket — the fix needs a design choice (one charge per import, or
  resumable progress).
- Otherwise none blocking. Future candidates (not scheduled): PDF text-only import; Quill paste matcher
  (pending browser-pass verdict); author's-note detection.
