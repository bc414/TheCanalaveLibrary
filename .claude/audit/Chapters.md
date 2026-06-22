# Audit — Chapters/

**Features:** 6 (writing & versioning), 7 (reading), 44 (reading-progress tracking).

## Shared Context

**Entities (Core/Models/):** `Chapter` (`StoryId`, `ChapterNumber`, `Title`, `PrimaryContentId` FK to the
active version), `ChapterContent` (versioned body — "live alternates" not revision history, `Rating`,
`SortOrder`), `UserChapterInteraction` (`ReadProgress` 0.0–1.0, `IsRead`, PK `(UserId,ChapterId)`).
**Fluent config:** unique `(StoryId,ChapterNumber)`, unique `(ChapterId,SortOrder)`; the two-relationship
setup separating "all versions" (Cascade) from "primary version" (`PrimaryContentId`, Restrict — can't
delete the primary). Reader settings live in `User.ReaderSettings` (JSON, see Identity).

**As of WU6 (2026-06-21), both universal rich-text atoms — `RichTextView` (WU5) and `EditorView`
(WU6) — are built. Neither lives under Chapters**; both sit in the cross-cutting `SharedUI/RichText/`
cluster (this folder doesn't own them, it's one of their consumers), alongside the server-side
`IHtmlSanitizationService` allow-list (`Core/RichText/` + `Server/RichText/`, minted in WU6). No
chapter write/read service exists yet — that's still unbuilt here, and is the first intended caller
of both `EditorView`'s output and the sanitizer.

---

## Feature 6 — Chapter Writing & Versioning
- **L1 — Stage 5.** `Chapter`/`ChapterContent` with the live-alternate versioning model and
  `PrimaryContentId` primary-version link. Sound; matches §6.9. Awaiting migration.
- **L2 — Stage 2.** No chapter write service. The `IHtmlSanitizationService` allow-list (§3.21) is
  minted in WU6 (DI-registered, `Server/RichText/`) but has no call site yet — this chapter write
  service is its first intended caller. Word-count-on-stripped-text also unimplemented.
- **L3-Logic — Stage 5 (WU6, see Stage note below).** `EditorView`'s coordination logic (preview
  capture/popup state, `GetHtmlAsync()` pull-on-submit contract) — Phase-1 atom shared with
  BlogPosts/Messaging/Recommendations/Comments/Profiles. Version indicators (live-alternate switcher
  UI) remain unbuilt — that's `ChapterPage`/`ChapterNavigation` (WU18/WU26), not this atom.
- **L3.5-Structure — Stage 5 (WU6, see Stage note below).** `EditorView` (third-party wrapper
  composite) built. `RichTextView` leaf was WU5.
- **L4-Style — Stage 5 (WU6, see Stage note below).** Toolbar + preview-popup visual treatment built
  and visually confirmed (tokens locked Phase C; the global blocker is cleared — the earlier "Stage 1,
  blocked on tokens" note was stale, same correction as Feature 7's L4).
- **L5 — Stage 2. L6 — Stage 2.**

**WU6 Stage note (2026-06-21) — `EditorView` composite + sanitizer allow-list, DONE ✓.** Built
`EditorView` (`SharedUI/RichText/EditorView.razor`) — a third-party wrapper composite around
Blazored TextEditor (Quill.js, v1.1.3, netstandard2.0 — consumes cleanly on net10.0). Settled during
the build, superseding the original spec/skill sketch:
- **No `Compact` runtime toggle.** Tried first, then discarded: Quill binds toolbar-button listeners
  once at construction, so changing the `ToolbarContent` RenderFragment later doesn't rewire them —
  forcing a real rebuild needs `@key`-driven destroy/recreate, which duplicates the device-axis
  problem `layer4-style.md` Responsive Breakpoints already settles ("structurally different → separate
  components"). **MVP ships the desktop toolbar only; the mobile-compact variant is deferred**
  (not MVP-blocking — build it as a separate composition when a mobile EditorView consumer is needed,
  mirroring `HomeDesktop`/`HomeMobile`).
- **Preview is an overlay popup, not an in-place swap.** Quill stays mounted continuously (cursor/
  scroll position preserved); preview renders `RichTextView` in a dimmed-backdrop popup instead of
  replacing the editor in the tree — an in-place swap reflowed the surrounding page on every toggle.
- Sanitizer allow-list minted alongside: `IHtmlSanitizationService` (`Core/RichText/`) /
  `ServerHtmlSanitizationService` (`Server/RichText/`, wraps a configured `Ganss.Xss.HtmlSanitizer`
  9.0.892, `AddSingleton` in `Server/Program.cs`) — permits exactly the toolbar's output set (`p, br,
  strong, em, u, s, h2, h3, blockquote, ul, ol, li, a`), normalizes link `rel`/`target` itself rather
  than trusting client-supplied values. **No call site wired** — first intended caller is this
  feature's own chapter write service (WU17), so L2 stays Stage 2.
- Doc-Touch (moment 1, before the build): `layer2-services.md` "The allow-list is the inverse of the
  toolbar"; `layer3.5-structure.md` "Third-Party Wrapper Composite" (corrected the snippet's
  `@bind-Value` — doesn't exist on Blazored TextEditor — to the real `ToolbarContent`/`EditorContent`/
  `GetHTML()`/`LoadHTMLContent()` API, and replaced the in-place-swap sketch with the popup pattern);
  `layer4-style.md` "Reader Settings as CSS" (fixed a stale `RichTextEditor` → `EditorView` naming
  mismatch); `cross-cutting.md` "Rich Text & Sanitization" (flagged mobile toolbar as deferred).
- Inline Pokémon-sprite Quill blot (spec §5.30.2) remains explicitly out of scope — its own future
  work-unit.
- **Verified:** `dotnet build` green (4 projects, 0 warnings/errors); live server run, homepage `200`;
  `Blazored.TextEditor`/`HtmlSanitizer` NuGet restore + build confirmed on net10.0; throwaway harness
  on `HomeDesktop.razor` plus a throwaway `/dev/test-html-sanitizer` diagnostic endpoint confirmed the
  sanitizer strips `<script>`/event-handler attributes/`javascript:` hrefs while preserving allowed
  formatting; user-confirmed visual check against the live server (toolbar renders and is functional,
  preview popup opens/closes without page reflow, `GetHtmlAsync()` captures edited content). Harness
  and diagnostic endpoint removed after confirmation.
- **Feeds WU17 (chapter write service):** inject `IHtmlSanitizationService`, call `Sanitize()` on
  `EditorView.GetHtmlAsync()`'s output before persisting `ChapterContent`.
- **2026-06-22 (WU12.5 backfill):** WU6's throwaway diagnostic endpoint was the only automated check;
  verification migrated into asserted tests — `HtmlSanitizationServiceTests` in
  `TheCanalaveLibrary.Tests.Unit` (tier: **Unit**). `ServerHtmlSanitizationService` is constructed
  directly (no host, no DB). Covers: `<script>` stripping (the WU6 regression); all 11 allowed tags
  survive (`p/strong/em/u/s/h2/h3/blockquote/ul/ol/li/a`); disallowed tags stripped; anchor
  href-only survival + `target=_blank`/`rel="noopener noreferrer"` injection; scheme filtering
  (`javascript:` dropped); CSS/class attribute stripping; plain text preserved; null/empty/whitespace
  → `string.Empty`. One production fix discovered: the guard was `IsNullOrEmpty` — whitespace-only
  input bypassed it and the sanitizer returned it unchanged; corrected to `IsNullOrWhiteSpace`.
  Mutation-sanity confirmed: adding `"script"` to the allow-list → `Sanitize_ScriptTag_IsStrippedCompletely`
  fails. `dotnet test` green.

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
