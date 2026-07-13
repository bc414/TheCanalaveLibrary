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

**WU-ErrorHandling note (2026-07-06) — editor draft safety live.** `ChapterEditorPage` embeds
`DraftAutosave` (`draft:chapter:{contentId}` / `draft:chapter:new:{storyId}`): 10s change-only
localStorage capture of Title/ChapterText/notes/VersionName, restore banner on return, backup
cleared on successful submit. `ChapterPropertiesForm` gained `Set*Async` push methods (Quill
ignores later `Html` parameter changes) and renders errors via `InlineAlert`; all the page's
catches route through `ExceptionPresenter` (unexpected → `LogError` with IDs). Browser-verified
end-to-end on the seeded chapter (type → autosave → reload → restore → submit clears; `psql`
ground truth both ways). Strategy: `error-handling.md` §"Error Handling Strategy"; tests:
`DraftAutosaveTests` (RazorComponents), `ExceptionPresenterTests` (Unit).

**WU26 Phase 0.5 Stage note (2026-06-24) — Rating model reconciliation, DONE ✓.**
`ChapterContent.Rating` was non-nullable (`Rating`, default E), diverging from spec §5.2 which
specifies nullable (NULL = inherit story rating). Reconciled as part of WU26 Phase 0.5, before any
editor UI was built:

- **L1:** `ChapterContent.Rating` changed to `Rating?` (nullable). EF config `HasConversion<short?>()`.
  Migration `20260624123232_MakeChapterContentRatingNullable` (alters `chapter_contents.rating` from
  `smallint NOT NULL` to `smallint NULL`; existing rows keep their explicit values).
- **L2 (write):** `CreateChapterDto.Rating` and `UpdateChapterContentDto.Rating` changed to `Rating?`
  (null = inherit). `ChapterValidations.CanSave` extended with `storyRating`/`isPrimary` parameters:
  floor invariant (explicit override must be ≥ story rating) and primary invariant (effective rating
  of the primary version must equal story rating, naturally satisfied by null/inherit). Both invariants
  enforced by `ServerChapterWriteService`: `CreateChapterAsync` and `AddAlternateVersionAsync` load
  story rating before calling `CanSave`; `SetPrimaryVersionAsync` enforces primary invariant inline.
  `UpdateChapterContentAsync` loads story rating and `IsPrimary` flag before calling `CanSave`.
- **L2 (read):** `ServerChapterReadService` rating ceiling changed from `cc.Rating <= ceiling` to
  `(cc.Rating ?? cc.Chapter.Story.Rating) <= ceiling` (EF COALESCE) in all three query methods.
  Projection: `ChapterReadingDto.Rating` = effective (`cc.Rating ?? storyRating`). `ChapterVersionDto.Rating`
  = raw nullable (null = inherit; consumers compute effective as `rating ?? storyRating`).
  `ChapterReadingDto.RawRating` added (populated only by `GetChapterForEditAsync`, for the edit form's
  "Same as story (inherit)" vs explicit-override display; null on reading-page loads).

Verified: `dotnet build` green (8 projects). `dotnet test` green — 195 Unit + 261 RazorComponents +
195 Integration = 651 total. Test tier: **Integration** (5 new tests in `ChapterWriteServiceTests` —
null rating as primary, floor rejection, primary invariant rejection on create + promote; 1 new test in
`ChapterReadServiceTests` — null-rated version reads as effective story rating). `SeedStoryAsync` in
`IntegrationTestBase` extended with optional `rating` parameter.

- **L1 — Stage 5.** `Chapter`/`ChapterContent` with the live-alternate versioning model. Note:
  `Chapter.PrimaryContentId` was changed from `long` to `long?` (nullable) during WU17 to break the
  circular FK insert dependency (`Chapter.PrimaryContentId → ChapterContent.ChapterId → Chapter`);
  migration `20260623005108_MakeChapterPrimaryContentIdNullable` applied. `PrimaryContent` nav is now
  `ChapterContent?`. Also: `Story.ChapterCount` does not exist in the current C# model (the field was
  assumed during WU17 planning but is absent from the L1 entity — future work-unit adds it when needed).
- **L2 — Stage 5 (WU17, DONE ✓ 2026-06-22).** Built `IChapterWriteService : IChapterReadService` and
  `ServerChapterWriteService : ServerChapterReadService` in `Core/Chapters/`/`Server/Chapters/`. Write
  service is the **first production caller** of `IHtmlSanitizationService`. `ChapterText.CountWords()`
  helper in `Core/Chapters/ChapterText.cs` (strips HTML tags + decodes entities before splitting on
  whitespace — dependency-free, parallels `StorySlug.Slugify`). Write surface: `CreateChapterAsync`
  (two-SaveChanges circular-FK break: insert Chapter+ChapterContent graph with `PrimaryContentId=null`,
  then fix-up), `AddAlternateVersionAsync`, `UpdateChapterContentAsync`, `SetPrimaryVersionAsync`,
  `SetPublishedAsync`. `Story.WordCount` and `Chapter.VersionCount` maintained; `Story.ChapterCount` not
  maintained (field absent from model — see L1 note). No Client/WASM impl or `ChapterEndpoints` (MVP
  InteractiveServer-only). Verified: `dotnet build` green; `dotnet test` 50/50 green (Unit tier:
  `ChapterTextTests` — 14 tests covering CountWords over plain text, HTML tags, entities, null/whitespace;
  Integration tier: `ChapterWriteServiceTests` — 8 tests covering circular-FK insert, sanitization, word
  count, versioning, promotion, update, Story.WordCount roll-up, validation). Mutation-sanity confirmed:
  adding `"script"` to the sanitizer allow-list → `SanitizesScriptTag` fails; reverted. Server boot:
  homepage/login `200`, DI resolves both services.
- **L3-Logic — Stage 5 (WU26 editor slice, DONE ✓ 2026-06-24; WU6 atom already Stage 5).** `ChapterEditorPage`
  dispatcher with progressive versioning disclosure, publish toggle, author gate, "Set as default" promote,
  "Add alternate version" action. `ChapterEditorViewModel` shields form from DTO fields. See WU26 Phase 1–3 Stage note.
- **L3.5-Structure — Stage 5 (WU26 editor slice, DONE ✓ 2026-06-24; WU6 atom also Stage 5).** `ChapterPropertiesForm`
  (three `EditorView` instances — top note, chapter text, bottom note), version switcher composite (progressive),
  per-version controls. `ChapterEditorPage` orchestrates all.
- **L4-Style — Stage 5 (WU26 editor slice, DONE ✓ 2026-06-24; WU6 atom already Stage 5).**
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto; chapter CREATE verified in a real WASM runtime during the
  flip's browser wave (incl. the create→edit `forceLoad` redirect fix for Quill-hosting pages).
  Full wave narrative + the 7 bugs found/fixed: `workplan.md` WU-GlobalFlip.
- **L6 — Stage 2.**

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
- **WU38c additive extension (2026-07-11):** `GetChaptersForExportAsync(storyId)` added to
  `IChapterReadService`/`ServerChapterReadService` — every published chapter's primary-version
  content in one query, ordered, viewer's rating ceiling applied ("export = what you can read";
  alternates deliberately excluded — exports carry the canonical text). Integration-covered via
  `ExportServiceTests` (order, unpublished exclusion, rating gate). Cells stay Stage 5 (additive).
- **WU38d additive touch (2026-07-11):** `ChapterEditorPage` gained file import (Feature 63 modes
  1/2): `ChapterFileImport` above the form — new chapter → parsed draft straight into the editor
  via `SetChapterTextAsync` (+ title from first heading); existing chapter → author chooses
  replace-editor vs `AddAlternateVersionAsync` (VersionName "Imported"). Browser-verified
  (mode 1); detail in `audit/Import.md`.
- **L2 — Stage 5 (WU17, DONE ✓ 2026-06-22; extended WU25, 2026-06-24).** `IChapterReadService` with
  `GetChapterForReadingAsync`, `GetChapterTocAsync`, `GetChapterVersionsAsync`, `GetChapterForEditAsync`
  in `ServerChapterReadService` (primary-constructor DI on `ReadOnlyApplicationDbContext`). Per-version
  `ChapterContent.Rating` filter applied explicitly (the global `"ContentRating"` query filter covers
  `Story` only — `ChapterContent.Rating` vs `ShowMatureContent` ceiling is a manual `.Where()` in every
  method). `ChapterReadingDto` includes prev/next chapter numbers (EF correlated subqueries) and
  `StoryRating` so L3 can render the "chapter rating exceeds story rating → skip to next" warning without
  a second fetch. Note on `GetChapterVersionsAsync`: `.OrderBy()` must be inside `SelectMany`'s inner
  query (on the entity field), not after — EF Core cannot translate `.OrderBy()` on a DTO property
  projected from a `SelectMany` transparent identifier.
  **WU25 additive extension:** `GetChapterListAsync(int storyId)` → `IReadOnlyList<ChapterListEntryDto>`.
  Story-landing-page TOC-with-versions (distinct from `GetChapterTocAsync` which stays the reading-page
  lean TOC). New DTO: `ChapterListEntryDto(int ChapterNumber, string Title, int WordCount, bool IsPublished,
  IReadOnlyList<ChapterVersionDto> AlternateVersions)` — `AlternateVersions` holds non-primary accessible
  versions only (empty for the common single-version case; reuses `ChapterVersionDto`).
  Implementation: two-step — (1) chapter rows via `readDb.Chapters` `OrderBy(ChapterNumber)`, (2)
  non-primary alternates via `SelectMany` (mirrors `GetChapterVersionsAsync` translatable pattern,
  references `c.PrimaryContentId` from outer scope), grouped in memory. `ChapterNavigation` (WU18)
  is **not** used on the story landing page — it is reading-context-only; the story page uses
  `ChapterList` (WU25 new leaf in `SharedUI/Chapters/`).
  Verified: see WU25 stage note in `audit/Stories.md` Feature 5 L3-Logic.
- **L3-Logic — Stage 5 (WU26, DONE ✓ 2026-06-24).** `ChapterReadingPage` dispatcher built with content-rating
  handling, scroll-progress JS interop, attribution capture, helpful-prompt gate. Reader settings cascade
  provider deferred to WU30 (`RichTextView` falls back to defaults). `AutoLoadNextChapter` is post-MVP.
- **L3.5-Structure — Stage 5 (WU26 page slice, DONE ✓ 2026-06-24; WU18 nav slice and WU5 leaf also Stage 5).**
  `ChapterReadingPage` + `ChapterNavigation` top+bottom + `CommentSection` wired. See WU26 Phase 1–3 Stage note.
- **L4-Style — Stage 5 (WU26/WU18/WU5, DONE ✓ 2026-06-24; see Stage notes).**
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto; chapter reading page verified in a real WASM runtime during
  the flip's browser wave (content, TOC, and versions loaded via API). Full wave narrative + the
  7 bugs found/fixed: `workplan.md` WU-GlobalFlip.
- **L6 — Stage 2.**

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

**WU18 Stage note (2026-06-23) — `ChapterNavigation` composite, DONE ✓.** Built
`ChapterNavigation` (`SharedUI/Chapters/`, first file in the SharedUI `Chapters/` cluster — folder
created just-in-time). Mints the parameter contract the WU26 reading-page dispatcher will pass to
both the top and bottom instances.

Settled (both confirmed with user 2026-06-23, Doc-Touch moment 1/pre-plan):

1. **Navigation = anchor `<a href>` links** to the spec URLs (spec §5.30.3). Composite is injection-
   free — no `NavigationManager`, no service injection. Blazor's `Router` intercepts internal links in
   both InteractiveServer and InteractiveWasm modes (no full page reload). Disabled endpoints (first/
   last chapter boundary) render as `<span aria-disabled="true">`, not `<button disabled>` — these
   are navigation, not actions.

2. **Compact chapter dropdown + alt-version indicator** — HTML `<details>`/`<summary>` disclosure
   (no JS, no Blazor state; all links always in the DOM for bUnit regardless of open state). Each TOC
   entry is a link to its chapter's primary URL; entries with `HasAlternateVersions=true` carry a small
   `<span title="Has alternate versions">` glyph. A **separate version picker** `<details>` is rendered
   only when `Versions.Count > 1`; primary version → clean `/story/{id}/{ch}` URL; alternate →
   `/story/{id}/{ch}/{versionOrder}`. The rich full table of contents (one row per chapter, split
   horizontally per version) lives on the story detail page — WU25/WU26, not here.

Parameter contract minted (WU26 dispatcher must supply these):
```csharp
[Parameter, EditorRequired] public int StoryId { get; set; }
[Parameter, EditorRequired] public int CurrentChapterNumber { get; set; }
[Parameter] public int CurrentVersionOrder { get; set; }        // 0 = primary (matches ChapterReadingDto.VersionOrder)
[Parameter] public int? PreviousChapterNumber { get; set; }
[Parameter] public int? NextChapterNumber { get; set; }
[Parameter, EditorRequired] public IReadOnlyList<ChapterTocEntryDto> Toc { get; set; } = [];
[Parameter] public IReadOnlyList<ChapterVersionDto> Versions { get; set; } = [];
```

**Verified:** `dotnet build` green (8 projects, 0 errors, 2 pre-existing warnings unrelated to this
WU). `dotnet test` green — 105 Unit / 122 RazorComponents / 109 Integration = **336 total**. Test
tier: **RazorComponents** (`ChapterNavigationTests.cs` — 13 tests covering prev/next hrefs,
first/last-chapter disabled spans, TOC link count + hrefs, current-chapter `aria-current`, alt-version
indicator on HasAlternateVersions-only entries, version picker visibility gate, per-version hrefs +
primary clean URL, current-version `aria-current`). No Unit/Integration tests apply — no service, no
DB. Visual/L4 human sign-off pending (Stage 6) via throwaway harness on live server before WU26 lands.
Mutation-sanity confirmed manually: inverting `aria-current` condition → tests fail; inverting
`HasAlternateVersions` guard → tests fail; showing version picker for single-version chapter → test
fails. Doc-Touch moment 2 (mid-build): `layer3.5-structure.md` "Pass-Through Layout Composite" snippet
updated from placeholder sub-components to the real `<details>`/`<summary>` anchor shape; new rules
recorded for CSS disclosures, `<a>`-vs-`<span>` navigation pattern, `aria-current="page"`, and
version URL contract. `layer4-style.md` Pattern Accumulation entry added for the nav-bar disclosure
shape and dropdown row classes.

## Feature 44 — Reading Progress Tracking
- **L1 — Stage 5.** `UserChapterInteraction.ReadProgress` / `IsRead`. `UserChapterInteraction.cs` moved
  from deprecated `Core/Models/` → `Core/Chapters/` (vertical org rule, WU26 2026-06-24).
- **L2 — Stage 5 (WU26, DONE ✓ 2026-06-24).** `IReadingProgressWriteService` + `ServerReadingProgressWriteService`
  in `Core/Chapters/`/`Server/Chapters/`. `MarkStartedAsync(int storyId)` added to `IUserStoryInteractionWriteService`.
  `IRecommendationReadService.GetHelpfulPromptRecommendationIdAsync(int storyId)` added. See WU26 Phase 1–3 Stage note.
- **L3-Logic — Stage 5 (WU26, DONE ✓ 2026-06-24).** JS scroll interop (reading-progress.js), `[JSInvokable]
  OnScrollProgress` callback, Ch.1 ≥90% `MarkStartedAsync` trigger, helpful-prompt gate.
- **L3.5-Structure — Stage 5 (WU26, DONE ✓ 2026-06-24).** Progress tracking wired into `ChapterReadingPage`.
- **L4 — N/A** (no dedicated visual surface). **L5 — N/A** (write path, server-only).
- ~~**L7 — Stage 2.** Redis batching of progress writes (write-behind pattern 1; MVP direct DB; L7 swaps body).~~
  Superseded — see "Feature 44 L2 body swap — signal buffer" below (Layer 7 dissolved 2026-07-06).

---

### WU26 Phase 1–3 Stage note (2026-06-24) — Reading/writing pages + F44 L2/L3/L3.5, DONE ✓

**Feature 6 (Writing & Versioning) L3/L3.5/L4 — Stage 5:**
Built `ChapterEditorPage.razor` (`SharedUI/Chapters/`), `ChapterPropertiesForm.razor`, and
`ChapterEditorViewModel.cs` (Pattern 1 edit side). Three `@page` directives: new chapter, edit primary,
edit versioned. Key design decisions:
- Form fields: title (optional → defaults to "Chapter N"), top/bottom author's notes (two
  `EditorView` instances with `Compact=true` variant), chapter text (`EditorView`), rating (nullable —
  null = "Same as story (inherit)"), optional `VersionName`.
- **Progressive disclosure:** `VersionCount > 1` gate on version name field and on the full version
  switcher + per-version controls (set-as-default, "Add alternate version" link at the bottom).
  `IsPrimary` drives "Primary" badge, never `SortOrder`. Primary version's rating field is locked to
  a read-only description (primary invariant — raising the story rating is the unlock path).
- **Rating picker:** For non-primary alternates, the select only shows ratings ≥ story rating
  (floor invariant UI). `null` option = "Same as story (inherit)".
- **Publish toggle** inline with form status (Draft / Published) with Publish/Unpublish buttons.
- **Author gate:** UX pre-check from `chapterDto.AuthorId` vs `AuthStateTask` claims — purely for
  early UI feedback; `ServerChapterWriteService` is the real authority.
- **Story rating** loaded via `GetStoryForEditAsync(StoryId).Rating` (also returns the story's
  edit data the page needs for the floor-aware rating picker).

The `GetChapterForEditAsync` route resolves `(ChapterNumber, VersionOrder?) → ChapterContentId` by
loading the version list first (`GetChapterVersionsAsync`), then selecting the primary version's
`ChapterContentId` or the matching `SortOrder` entry.

**Feature 7 (Reading) L3/L3.5/L4 — Stage 5:**
Built `ChapterReadingPage.razor` (`SharedUI/Chapters/`), implementing Pattern 1 read side. Two `@page`
directives (primary + versioned URL). Key design:
- Content-rating handling: null DTO from versioned URL → "requires mature content" notice (not
  redirect); null DTO from primary URL → `/not-found`. Exceeds-story-rating heads-up banner
  (`dto.Rating > dto.StoryRating`) for mature readers on exceeding alternates (primary invariant
  guarantees primary never triggers this).
- Top and bottom `ChapterNavigation` instances (TOC + version list loaded in parallel via
  `Task.WhenAll`).
- Top/bottom author's notes in `<aside>` blocks (only if non-null).
- `<article id="chapter-body">` — anchor for the reading-progress scroll tracker.
- `CommentSection` wired with `ChapterId`, `CurrentUserId`, `UserHasCompletedStory=false` (full
  completion tracking is post-MVP).
- `RecommendationHelpfulPrompt` gated on `_recId.HasValue && _progressReached90` — prompt appears
  only after scroll reaches 90% of chapter body AND a source recommendation exists with no prior
  success. `OnRespond(true)` → `RecordSuccessAsync(recId)`.
- Author-only edit link (UX visibility; server is authority).
- `CascadingParameter Task<AuthenticationState>` resolves `_currentUserId` from `NameIdentifier` claim.
- Attribution capture: `?rec={id}` query param → `RecordAttributionSourceAsync` on first render (fire-
  and-forget with `_ =`).

**Reading-progress JS scroll tracker:**
Added `SharedUI/wwwroot/js/reading-progress.js`: IIFE module exporting `register(dotnetRef, elementId)`
and `dispose()`. Throttled scroll/resize listener (300ms debounce) computes fraction of the chapter body
element scrolled past the viewport (bottom edge) and invokes `dotnetRef.invokeMethodAsync('OnScrollProgress', fraction)`.
`App.razor` loads the script via `_content/TheCanalaveLibrary.SharedUI/js/reading-progress.js`.
Page registers in `OnAfterRenderAsync(firstRender)`, disposes in `DisposeAsync`. `[JSInvokable]
OnScrollProgress(float progress)` calls `RecordProgressAsync` and (Ch.1 ≥ 0.9) `MarkStartedAsync` +
flips `_progressReached90` to reveal the helpful prompt. `JSException` caught silently (SSR/test host).
`DotNetObjectReference` disposed in `DisposeAsync` (IAsyncDisposable).

**Reader-settings cascade:** The provider (`CascadingValue<ReaderDisplaySettings>`) is not wired in
WU26 — `RichTextView` falls back to its built-in defaults (Georgia, 16px, 1.5 line-height, 800px,
left-aligned). Wiring the provider is deferred to WU30 (profile settings edit, the first feature
that actually manages reader settings). See WU5 Stage note "Deferred dependency" entry.

**Feature 44 (Reading Progress Tracking) L2/L3/L3.5 — Stage 5:**
- `IReadingProgressWriteService` minted in `Core/Chapters/`; `ServerReadingProgressWriteService`
  in `Server/Chapters/` (direct DB upsert at WU26; superseded by the signal buffer below).
  `IReadingProgressWriteService` DI-registered in `Program.cs`.

**Feature 44 L2 body swap — signal buffer (WU-SignalBuffering, 2026-07-06). Supersedes the
"L7 Redis write-behind" plan** (divergence from spec §3.18/§7 "Redis" sections: the spec's Redis
hash + write-behind design was a SQL-Server-era plan; under Postgres MVCC its locking rationale is
void, and the in-process buffer achieves the batching with no external store — see
`middle_plan_v2.md` Resolved "Layer 7 dissolved"):
- `ServerReadingProgressWriteService.RecordProgressAsync` body = `ReadingProgressBuffer.Record`
  (O(1) coalescing merge — max progress + latest timestamp per (user, chapter); no DB hit).
  Interface unchanged; its doc now states the honest contract (eventually-durable, loss window =
  one flush interval, not read-your-own-write). HasStarted still takes the durable direct path.
- `ReadingProgressFlusher` (singleton): one batched `unnest … ON CONFLICT (user_id, chapter_id)
  DO UPDATE` upsert per cycle — `GREATEST` progress/timestamp merge + sticky `is_read` makes
  retry-strategy replays idempotent; `WHERE EXISTS` guards drop pings whose chapter/user was
  deleted mid-window (one stale ping can't fail the batch); failed batches restore to the buffer.
  `is_read` computed in C# with the same `>= 0.9f` float comparison as the old direct write (SQL
  `real`→`double` promotion would shift the edge).
- `ReadingProgressFlushWorker` (`BackgroundService`, 5 s `PeriodicTimer`): drains once more after
  cancellation (graceful shutdown doesn't eat the loss window). Removed from the test host by
  `TestAppFactory` — integration tests flush deterministically via the flusher.
- **No stored LastReadDate anywhere** (settled 2026-07-06): the spec §3.18 Redis hash is dead;
  Bookshelves "Actively Reading" recency is *derived* — `DefaultSortOrder.RecentlyRead` sorts by
  `MAX(uci.last_interaction_date)` per story (explicit `Any()` first key = NULLS LAST for
  never-pinged stories), offered/defaulted only on that tab (`AvailableSorts` is per-surface;
  never on `/discover`). `UserStoryInteractionDate` keeps its once-per-deliberate-action contract.
- Telemetry: `CanalaveTelemetry.ReadingProgress` — buffer-depth gauge + flush batch-size/duration
  histograms + `ReadingProgress.Flush` spans.
- **Verified:** Unit tier (`ReadingProgressBufferTests` — coalescing/drain/restore semantics),
  Integration tier (`ReadingProgressFlushTests` — nothing persists pre-flush, high-water +
  sticky-is_read across flushes, multi-reader batch, anonymous no-op, deleted-chapter guard,
  RecentlyRead ordering incl. never-pinged-last). Browser-verified 2026-07-06: chapter scroll →
  flush landed the row (psql), Actively Reading tab ordered most-recently-read-first with
  "Recently read" default sort.
- `MarkStartedAsync(int storyId)` added to `IUserStoryInteractionWriteService` +
  `ServerUserStoryInteractionWriteService` (idempotent upsert — flips `HasStarted = true`, never
  clears other flags, no-ops for anonymous).
- `IRecommendationReadService.GetHelpfulPromptRecommendationIdAsync(int storyId)` added —
  returns the `SourceRecommendationId` from `UserStoryRecommendationSource` iff no
  `RecommendationSuccess` exists for (viewer, that rec); null otherwise.
- `UserChapterInteraction.cs` moved from deprecated `Core/Models/` → `Core/Chapters/` (vertical
  org rule; same namespace `TheCanalaveLibrary.Core`).
- Fakes updated: `FakeUserStoryInteractionWriteService.MarkStartedAsync` records calls;
  `FakeRecommendationWriteService.GetHelpfulPromptRecommendationIdAsync` returns a configurable id.

**Verified:** `dotnet build` green (8 projects, 0 errors, 1 pre-existing CS9107 warning unrelated to
WU26). `dotnet test` green — 195 Unit + 261 RazorComponents + 195 Integration = **651 total** (no
new tests added in Phases 1–3; existing suite includes the WU26-relevant integration tests from
Phase 0.5 for rating invariants). Test tier: Phases 1–3 are predominantly L3/UI surface — bUnit
RazorComponents tests for `ChapterReadingPage` and `ChapterEditorPage` are the pending gap (author
gate, prompt gating, progressive versioning UI assertions); server-side behavior is covered by existing
Integration tests (Phase 0.5 rating invariants) and existing Integration tests on the USI write service.

### WU26 settled constraints (pre-implementation, 2026-06-24)

**Routes (settled):** See `identity-and-authorization.md` "Two content-editing patterns" → Chapter route table.
Reading routes have no `/chapter/` literal; edit/new use `/chapter/` + chapter number + optional
`versionOrder`. URL version token = `ChapterContent.SortOrder` (`VersionOrder`), not raw ContentId.

**Versioning UX (settled):** Progressive disclosure on the edit page — see `layer3.5-structure.md`
"Chapter Versioning — Progressive Disclosure". `IsPrimary` drives the "Primary" badge, never
`SortOrder == 0`.

**`HasStarted` blocker resolved:** `has_started` column exists in `InitialSchema`; `UserStoryInteraction.HasStarted`
property exists (WU15). WU26 Phase 1 adds `MarkStartedAsync(int storyId)` (idempotent upsert) to
the USI write service. No migration needed.

**Chapter rating model (to be reconciled in WU26 Phase 0.5):**
- `ChapterContent.Rating` is currently `Rating` (non-nullable, E default). Spec §5.2 requires nullable
  (NULL = inherit story rating). WU26 Phase 0.5 reconciles this before any UI is built.
- Floor invariant: version effective rating ≥ story rating. Primary invariant: primary's effective
  rating = story rating (naturally via NULL/inherit). Details in `layer3.5-structure.md` "Chapter Versioning."
- Integration test coverage: floor rejection, primary rejection on create + promote, NULL→story
  inheritance, effective ceiling in reads.

**Browser-pass fixes (2026-07-01) — L3/L3.5 corrections, found via browser debugging (stages unchanged, fixed same-session):**
- `ChapterPropertiesForm` passed `InitialHtml=`/`Compact=` to `EditorView` — parameters the component
  never declared (unmatched component parameters fail at *runtime*, not compile time; every other
  caller uses `Html=`; `Compact` was the variant EditorView's header documents as tried-and-discarded).
  `/story/{id}/chapter/new` 500'd on render. Fixed to `Html=`, `Compact` removed.
- `ChapterEditorPage` post-create navigation interpolated the returned **ChapterId (PK)** into the
  `{ChapterNumber:int}` route slot (`/story/1/chapter/3429/edit` for chapter *1*). Now resolves the
  assigned number from `GetChapterTocAsync` (service assigns next-sequential) and lands on the new
  draft's edit page (which owns the Publish toggle).
- `ChapterEditorPage` loaded only in `OnInitializedAsync` — same-component route changes
  (`/chapter/new` → `/chapter/{n}/edit`, version switches) rendered stale state. Refactored to the
  WU-ComponentSoundness dispatcher pattern (`LoadAsync` + guarded `OnParametersSetAsync`), which that
  wave applied to ChapterReadingPage but not this page.
- **Verified:** browser — create chapter → land on `/story/1/chapter/1/edit` with populated form →
  Publish → `/story/1/1` renders the chapter (ChapterReadingPage's parallel toc+versions load also
  exercised live). No automated tier covers Quill/JS-backed editor rendering (bUnit fakes it); the
  param-name class of bug is caught only by rendering the real component tree — browser band per
  `debugging.md`.

**L4.5-Browser verification (2026-07-01) — F6 + F7 + F44 → Stage 5, no new bugs:**
As AuthorAlpha: "+ Add alternate version" on the flagship's chapter 1 → editor at
`/story/1/chapter/1/2/edit` with Versions panel → typed body → Save landed on the new version's
reading page → "Set as default" promoted it (bare `/story/1/1` served it) → demote/re-promote
restored the original. Author gate negative-tested (TestUser gets "You don't have permission to
edit this chapter" on someone else's chapter). Publish toggle + create-chapter verified in the
prior browser wave (workplan WU-BrowserPass). F7: chapter nav dropdowns, prev/next links, and the
per-chapter version switcher (appears only when alternates exist; `/story/1/3/2` renders the
alternate with switcher state) all behave per the WU26 notes. F44: as TestUser on a never-touched
story (`/story/6/1`), the `readingProgress` JS threshold fired and
`user_story_interactions.has_started` flipped to true (psql-verified) — the JS-interop band no
automated tier covers.

---

## WU45 settled design (2026-07-12) — chapter presentation, manual read-marks, reorder/delete

Chat deliberation with Brian (Fimfiction reference pages inspected directly: DOM + CSS + JS of two
real story pages — behavioral reference only, deliberately NOT a mechanism port; Fimfiction is
progressive-enhancement JS, TCL re-derives from Blazor first principles). Full requirements record:
WU45 in `workplan.md`. **Settled — do not revisit without flagging:**

**Feature 7 surface — `ChapterList` upgrade (L3/L3.5/L4 reopen to Stage 2):**
- Per-row: title link, publish date, word count, read/in-progress/unread state, "New" badge,
  per-chapter download menu (chapter-granular reuse of WU38c export writers), progress fill-bar
  (`ReadProgress` rendered as an absolutely-positioned background wash behind row content —
  Indicator role composed onto the row Control; design tokens, no ad-hoc color).
- **Segmentation is one pure shared function** (no EF/JS deps): `(chapters, arc ranges, per-viewer
  read state, watermark, constants) → flat render-item list` (chapter rows + arc-header +
  expander markers). Server runs it for initial paint (via the existing `[PersistentState]`
  StoryPage pattern); the interactive component re-runs the SAME function locally after a
  read-state mutation. Collapse/expand + arc toggles are ephemeral local view-state, always
  re-derivable from read state (SSR output correct before circuit/WASM attaches).
- **Collapse rule (one model, no special cases):** every chapter is governed either by its arc
  (whole-arc granularity, sticky always-visible header, toggleable; frontier arc expanded by
  default, others collapsed; NO windowing inside an expanded arc) or by frontier-windowing (gap
  segments and the no-arc case): read runs collapse behind counted expanders; `HeadWindow` (3)
  chapters stay visible from the frontier (first not-fully-read chapter; ch.1 for
  zero-read/anonymous); `TailWindow` (3) last-of-story chapters stay visible ONLY when the story
  tail is a gap segment (arcs fully govern their regions); nothing collapses under
  `CollapseMinimum` (~10). All three are named tunable constants.
- **"New" badge — strict chain rule:** `PublishDate > MAX(user_chapter_interactions.
  last_interaction_date)` AND every earlier chapter is read or itself New (the contiguous
  fresh run starting at the frontier). No interaction rows → no watermark → no badges. Cosmetic
  only — never pierces collapse.

**Feature 44 — manual read-marks (new durable-direct seam in `Chapters/`):**
- Per-row mark read/unread + mark-all. Durable user intent → direct EF write service, NEVER the
  buffered `RecordProgressAsync` pipeline (durability rule in this file's signal-buffer note).
- Manual marks set BOTH fields (read → `IsRead=true, ReadProgress=1`; unread → `false, 0`) —
  required because the flusher recomputes `is_read = progress ≥ 0.9` from high-water progress and
  would silently resurrect an overridden state. The write also discards pending buffered pings for
  that (user, chapter).
- Manual mark-read calls the existing idempotent `MarkStartedAsync(storyId)` ("read elsewhere"
  case); mark-unread never un-sets `HasStarted`.

**Feature 6 — chapter reorder + delete (new capability; L2 reopens to Stage 2):**
- Creation stays append-only (`MAX+1`). Reorder = drag-to-reorder of existing chapters only
  (published AND drafts), on the author edit surface. Move P→Q shifts the intervening range ±1;
  only `ChapterNumber` changes (all children key on `ChapterId`). Unique `(StoryId,ChapterNumber)`
  requires a transient-collision-safe update order. **Silent — no link-breakage or arc-crossing
  warning (explicitly waived 2026-07-12).**
- `DeleteChapterAsync`: author-gated, `ConfirmDialog` in UI; later chapters shift −1;
  `PrimaryContentId` (Restrict FK) nulled before row delete (mirror of two-step create);
  `Story.WordCount` refreshed. Arc bounds shift per the rule in `audit/Stories.md` F8; empty arcs
  auto-delete.

### WU45 build Stage note (2026-07-12) — DONE except L4.5 (deferred)

**Cells: F6 L2 additive (reorder/delete), F7 L3/L3.5 (ChapterList rebuild), F44 L2 additive
(manual marks). All automated tiers green: Unit 685 / Integration 650 / RazorComponents 619.
`check-design-tokens.ps1`: the only finding is pre-existing (`ImportReviewPanel.razor`, untouched
by WU45). L4.5-Browser verification explicitly deferred at WU45 close (Brian's direction) — F6/F7
L4.5 flipped 5→2 in `status.md` until the new surfaces get a real-circuit pass.**

- **Segmenter (the WU45 heart):** `ChapterListSegmenter` + `ChapterListItem` union +
  `ChapterListCollapseOptions` (`CollapseMinimum=10`/`HeadWindow=3`/`TailWindow=3`, named
  constants) in `Core/Chapters/` — pure, dependency-free, same function runs server-side (SSR
  paint) and client-side (post-mark re-segment). Covered: Unit `ChapterListSegmenterTests` (17 —
  frontier windowing incl. zero-read/partial/fully-read/in-progress-frontier, read-run labeling,
  stable reveal keys, strict-chain New incl. broken-chain + no-watermark suppression, arc
  segments incl. sticky fully-read headers / frontier-arc default expansion / no-windowing-inside
  / zero-visible-chapter arcs skipped / gap rows / arcs-govern-the-tail).
- **F7 read path:** `ChapterListEntryDto` enriched (`ChapterId`, `PublishDate`, `IsRead`,
  `ReadProgress`); `GetChapterListAsync` viewer-aware (one extra query, empty dict for
  anonymous); new `GetViewerLastInteractionUtcAsync` (New-badge watermark). Covered: Integration
  `ChapterReadMarkServiceTests` round-trip tests; existing `StoryDetailTests` unchanged and green.
- **F44 manual marks:** `IChapterReadMarkWriteService` / `ServerChapterReadMarkWriteService`
  (`Chapters/`) — durable-direct, both-fields writes, `ReadingProgressBuffer.Discard` (new seam)
  before save, `MarkStartedAsync` on read, mark-all published-only + sparse-unread. Covered:
  Integration `ChapterReadMarkServiceTests` (13, incl. both buffer-resurrection guards).
- **F6 reorder/delete:** `MoveChapterAsync` (negative-pass renumbering against the unique
  `(story_id, chapter_number)` index; arc remove-at-P + insert-at-Q composition; silent per the
  waived-warnings decision) and `DeleteChapterAsync` (TPT-safe comment removal — EF RemoveRange so
  base_comments rows go too; Restrict-FK two-step; −1 shift; arc shrink + empty-arc auto-delete;
  WordCount refresh), both in execution-strategy transactions (EnableRetryOnFailure precedent).
  Covered: Integration `ChapterReorderDeleteTests` (17).
- **UI:** `ChapterList` rebuilt (coordination composite — injects only the read-mark write
  service, UserStoryInteractionPanel precedent; fill-bar Indicator wash `--color-progress`/20;
  action-family read toggle; success-tint New badge; per-chapter download `<details>` menu of
  plain anchors). Per-chapter export: `ExportChapterAsync` + endpoint
  `GET /api/stories/{id}/chapters/{n}/export/{format}` (writers reused via the extracted
  `WriteAs`). `ChapterManagerPanel` (HTML5 drag reorder + ConfirmDialog delete) and
  `StoryArcManagerPanel` mounted on `StoryEditorPage` edit mode. `StoryPage` loads arcs +
  watermark (ephemeral, deterministic — no PersistentState needed) and passes `CanMarkRead`.
  Covered: RazorComponents `ChapterListTests` (25 = 14 preserved WU25 + 11 WU45: collapse/expander
  reveal/read-run label/fill width/toggle-service-call + aria-pressed/mark-all/arc header
  toggle/sticky collapsed arc/New badge/download links published-only).
- **Not covered by automated tiers (the deferred L4.5 band):** real-circuit drag-and-drop
  reorder, live mark→fill-bar repaint, arc-manager live preview interactivity, download
  Content-Disposition; plus `DataSeeder` has no arced story yet — manual verification needs an
  arc created via the panel first (or a seeder addition later).
