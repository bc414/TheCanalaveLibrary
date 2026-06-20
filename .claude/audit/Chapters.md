# Audit — Chapters/

**Features:** 6 (writing & versioning), 7 (reading), 44 (reading-progress tracking).

## Shared Context

**Entities (Core/Models/):** `Chapter` (`StoryId`, `ChapterNumber`, `Title`, `PrimaryContentId` FK to the
active version), `ChapterContent` (versioned body — "live alternates" not revision history, `Rating`,
`SortOrder`), `UserChapterInteraction` (`ReadProgress` 0.0–1.0, `IsRead`, PK `(UserId,ChapterId)`).
**Fluent config:** unique `(StoryId,ChapterNumber)`, unique `(ChapterId,SortOrder)`; the two-relationship
setup separating "all versions" (Cascade) from "primary version" (`PrimaryContentId`, Restrict — can't
delete the primary). Reader settings live in `User.ReaderSettings` (JSON, see Identity).

**Nothing in Layers 2–8 is built here.** No `EditorView`/Quill (no Blazored TextEditor or Quill package),
no `HtmlSanitizer` package, no chapter service, no reader components.

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
- **L3.5-Structure — Stage 2.** `ChapterPage` dispatcher (two `@page` directives — primary + versioned
  URL, §5.30.3), `ChapterNavigation` coordination composite (prev/next, dropdown, version switcher; top +
  bottom), `RichTextView` leaf — unbuilt.
- **L4-Style — Stage 1** (reader settings as CSS on the reading container; blocked).
- **L5 — Stage 2. L6 — Stage 2.**

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
