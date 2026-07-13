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
`SplitStrategy`, `ImportParseResult`, `IContentImportService`), Server readers + splitter
(`ServerContentImportService`, `DocxReader`, `EpubReader`, `HtmlReader`, `TxtReader`,
`MarkdownReader`, `ChapterSplitter`), SharedUI mode/review components (`ImportModePicker`,
`ImportReviewPanel`, `ChapterFileImport`). **No entities** — imported chapters are ordinary
chapters created through `IChapterWriteService`; external story links belong to Feature 53.

## Feature 63 — Chapter Import (file ingestion)
- **L1 — N/A** (no schema; commits go through existing Chapters tables).
- **L2 / L3-Logic / L3.5 / L4 — Stage 5 (WU38d, 2026-07-11).** **L4.5 — Stage 5** (see note).
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13 — reclassified from N/A; the flip makes every injected
  service need a client impl).** Endpoints + client impl live (WU-L5Sweep), incl. the three
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
  (explicit ref), Markdig (BSD-2).
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
  `ImageUploadProcessor` discipline, `security.md`); EPUB zip-bomb guards; parse failures are
  caught per-file — never a circuit crash. Review state lives in circuit memory (no temp storage).
- **Paste-from-Word is a verification item, not a build item** — Quill clipboard normalization +
  sanitize-on-save should already cover it; record fidelity during the browser pass; a Quill paste
  matcher becomes its own follow-up only if inadequate.
- **Rejected:** URL scraping from AO3/FFN (explicitly "file upload, not scraping" per the Gemini
  deliberation — respects other sites); auto-detecting author's notes into
  `TopAuthorsNote`/`BottomAuthorsNote` (too heuristic — authors cut/paste after import).

### Open
- None blocking. Future candidates (not scheduled): PDF text-only import; Quill paste matcher
  (pending browser-pass verdict); author's-note detection.
