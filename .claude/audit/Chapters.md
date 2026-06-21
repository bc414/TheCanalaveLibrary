# Audit — Chapters/

**Features:** 6 (writing & versioning), 7 (reading), 44 (reading-progress tracking).

## Shared Context

**Entities (Core/Models/):** `Chapter` (`StoryId`, `ChapterNumber`, `Title`, `PrimaryContentId` FK to the
active version), `ChapterContent` (versioned body — "live alternates" not revision history, `Rating`,
`SortOrder`), `UserChapterInteraction` (`ReadProgress` 0.0–1.0, `IsRead`, PK `(UserId,ChapterId)`).
**Fluent config:** unique `(StoryId,ChapterNumber)`, unique `(ChapterId,SortOrder)`; the two-relationship
setup separating "all versions" (Cascade) from "primary version" (`PrimaryContentId`, Restrict — can't
delete the primary). Reader settings live in `User.ReaderSettings` (JSON, see Identity).

**As of WU5 (2026-06-21), `RichTextView` (the universal display leaf, see Feature 7 Stage note) is
built — it lives in the new cross-cutting `SharedUI/RichText/` cluster, not under Chapters; this
folder doesn't own it, it's one of its consumers.** Everything else in Layers 2–8 remains unbuilt
here: no `EditorView`/Quill (no Blazored TextEditor or Quill package), no `HtmlSanitizer` package, no
chapter service.

---

## Feature 6 — Chapter Writing & Versioning
- **L1 — Stage 5.** `Chapter`/`ChapterContent` with the live-alternate versioning model and
  `PrimaryContentId` primary-version link. Sound; matches §6.9. Awaiting migration.
- **L2 — Stage 2.** No chapter write service. Server-side HTML sanitization (`HtmlSanitizer` allow-list,
  §3.21) and word-count-on-stripped-text are unimplemented and the package isn't referenced.
- **L3-Logic — Stage 2.** Universal `EditorView` (Quill wrapper) logic unbuilt — and is a Phase-1 atom
  shared with BlogPosts/Messaging/Recommendations.
- **L3.5-Structure — Stage 2.** `EditorView` (third-party wrapper composite), `RichTextView` leaf,
  version indicators — unbuilt.
- **L4-Style — Stage 1** (editor toolbar, Quill stylesheet interaction; blocked on tokens).
- **L5 — Stage 2. L6 — Stage 2.**

## Feature 7 — Chapter Reading
- **L1 — Stage 5.** `UserChapterInteraction` supports progress + read state.
- **L2 — Stage 2.** No reading read-service.
- **L3-Logic — Stage 2.** Reader settings application (font/size/line-height/width/justify, auto-load
  next, §7) and `AutoLoadNextChapter` unbuilt. Content-rating warning ("Skip to next chapter" when chapter
  rating exceeds story rating) unbuilt.
- **L3.5-Structure — Stage 2** (slice complete, see WU5 note below). `ChapterPage` dispatcher (two
  `@page` directives — primary + versioned URL, §5.30.3) and `ChapterNavigation` coordination composite
  (prev/next, dropdown, version switcher; top + bottom) remain unbuilt (WU18/WU26).
- **L4-Style — Stage 2** (slice complete, see WU5 note below; reader settings as CSS unblocked — tokens
  locked Phase C. The earlier "Stage 1, blocked on tokens" note was stale).
- **L5 — Stage 2. L6 — Stage 2.**

**WU5 Stage note (2026-06-21) — `RichTextView` leaf slice, DONE ✓.** Built the universal read-only
rich-text renderer (`SharedUI/RichText/RichTextView.razor`) — pure leaf, no service injection, no
sanitization (it trusts stored HTML; sanitization is the write path's job, §3.21 — see
`layer2-services.md` "User HTML Is Sanitized Once, On Save — Never On Display"). Reader display
settings (`FontName`/`FontSize`/`LineHeight`/`TextWidth`/`JustifyText`) arrive via a cascaded slim
property bag, `ReaderDisplaySettings` (`SharedUI/RichText/`, deliberately not a `*Dto` — see
`layer3.5-structure.md` "Ambient Viewer Settings via Cascading Slim Bags"), with built-in defaults
when no provider is present. `ReaderSettings` (Core) is unchanged — no L1/schema split. No border/
background on the leaf — that's a Container Composite (`Card`) concern owned by the composing context
(`layer4-style.md` Pattern Accumulation). New cross-cutting `RichText/` cluster recorded in `SKILL.md`
Code Organization (parallel to `Lookups/`).
- **This is only the leaf** — does not flip Feature 7's L3.5/L4 cells to Stage 5; `ChapterPage`/
  `ChapterNavigation` (WU18/WU26) still owe the rest of the structure.
- **Deferred, open dependency:** the layout-level `CascadingValue` *provider* (reading the
  authenticated viewer's `User.ReaderSettings` and converting it via `ToReaderDisplaySettings()`) is
  not wired yet. First real consumer needing it is WU26 (chapter reading) / WU30 (profile settings
  edit) — wire the provider there, not as a WU5 afterthought.
- **Verified:** `dotnet build` green (4 projects); live server run, homepage `200`; throwaway harness
  on `HomeDesktop.razor` confirmed both a non-default cascaded `ReaderDisplaySettings` and the
  no-cascade default path render correct inline styles and unescaped HTML (raw DOM inspected via
  `curl`); harness removed after confirmation.
- **Feeds WU6 (`EditorView`):** EditorView's preview mode renders `RichTextView` directly (spec
  §5.30.2) — what the author sees in preview is what readers see. WU6 owns sanitizing on save to
  match what this leaf trusts.

## Feature 44 — Reading Progress Tracking
- **L1 — Stage 5.** `UserChapterInteraction.ReadProgress` / `IsRead`.
- **L2 — Stage 2.** **L3-Logic — Stage 2.** JS scroll-percentage tracking; auto-set `IsRead` at >90%;
  **set `HasStarted` on `UserStoryInteraction` at 90% of Ch.1** — *blocked on the UserStoryInteractions
  reading-status re-model, since `HasStarted` does not currently exist* (see UserStoryInteractions audit).
- **L3.5-Structure — Stage 2.** **L4 — N/A** (no dedicated visual surface). **L5 — N/A** (write path).
- **L7 — Stage 2.** Redis batching of progress writes (write-behind pattern 1; MVP direct DB).

---

### Dependency callout
Feature 44's `HasStarted` write target does not exist yet — surface the UserStoryInteractions L1 re-model
dependency before implementing reading-progress writes.
