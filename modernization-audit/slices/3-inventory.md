# Slice 3 — Patterns Inventory (Chapters / Import / Export)

15 fixed dimensions. `mechanism` / `exemplar` / `deviations` each.

1. **Pagination**
   - mechanism: none in slice — chapter lists return whole `IReadOnlyList<…>` (a story's chapters are a bounded set); no `PagedResult`/offset/keyset. Comment pagination on the reading page is `CommentSection`'s (S5).
   - exemplar: `ServerChapterReadService.GetChapterListAsync` (`:131`) — full list, no `Skip/Take`.
   - deviations: none observed.

2. **DTO mapping**
   - mechanism: two-step projection where correlated data is needed (list rows then in-memory grouping of alternates + read-state dict overlay); direct `.Select(new …Dto(...))` for flat reads. `record` DTOs throughout; server-internal `StoryExportModel`/`ChapterRow`/`AltVersionRow` records escape the DTO firewall deliberately (server-only).
   - exemplar: `ServerChapterReadService.GetChapterListAsync:139-210` (chapters → read-state dict → alt-version SelectMany → in-memory stitch).
   - deviations: `StoryExportModel` (`Export/StoryExportModel.cs`) is a public record crossing NO UI boundary — documented exception (`:6-11`).

3. **Error surfacing**
   - mechanism: typed `ChapterValidationException`/`ImportException` (presentation-safe message) → `ExecuteWriteAsync` maps to 400; client re-throws the typed exception from `ProblemDetails.Detail`. UI catches route through `ExceptionPresenter` + `InlineAlert`.
   - exemplar: `ChapterEditorPage.razor:391-400` (typed → forbidden flag; unexpected → LogError + generic).
   - deviations: `ImportException` deliberately carries UX copy as its message (`Core/Import/ImportException.cs`) — survives verbatim through ProblemDetails; a per-format friendly-message convention, not a leak.

4. **Form patterns**
   - mechanism: `EditForm`+`DataAnnotationsValidator` over a `ChapterEditorViewModel` (shields the DTO); enum/nullable `<select>` via `InputSelect` with an empty-value "inherit" option; validation errors accumulate in `ViewModel.ServerValidationErrors` → `InlineAlert`.
   - exemplar: `ChapterPropertiesForm.razor:16-105`.
   - deviations: none — the rating picker's floor/primary invariant UI is spec-settled (WU26 Phase 0.5).

5. **Flyout/overlay mechanics**
   - mechanism: HTML `<details data-dropdown>` CSS disclosures for chapter-jump / version picker / per-chapter download (all links always in DOM, bUnit-testable); `ConfirmDialog` (universal atom) for destructive delete; `z-(--z-dropdown)` token.
   - exemplar: `ChapterNavigation.razor:38-82`; `ChapterManagerPanel.razor:60-67` (ConfirmDialog).
   - deviations: none.

6. **Optimistic updates & debounce**
   - mechanism: reading-progress uses NO per-component debounce — the JS scroll tracker throttles (300ms in reading-progress.js), the C# path writes straight to the singleton buffer. Manual read-marks are write-then-overlay (`_readOverrides` dict), not pre-emptive optimism.
   - exemplar: `ChapterList.razor:156-169` (await write, then set override; error resets message).
   - deviations: none — the only "timer" is the server-side `PeriodicTimer` flush worker.

7. **Disposal & lifecycle**
   - mechanism: `IAsyncDisposable` on the reading page for JS interop (dispose the scroll registration + `DotNetObjectReference`); re-register on in-place chapter change; `_jsRegistered` gate (no firstRender check).
   - exemplar: `ChapterReadingPage.razor:334-385`.
   - deviations: the dispose JS call's `catch {}` is bare (MA-303); import readers are static (no lifecycle); `MemoryStream buffered` in `ServerContentImportService` is `using`-disposed per parse.

8. **Query shape**
   - mechanism: factory-per-method read contexts (H-04 clean); effective-rating ceiling `COALESCE(cc.Rating, story.Rating) <= ceiling` applied per-method (the global `ContentRating` filter covers `Story` only); `SelectMany` for translatable alternate-version ordering; correlated subqueries for prev/next.
   - exemplar: `ServerChapterReadService.cs:27-47` (unified reading query), `:172-185` (alt-version SelectMany).
   - deviations: none — no `Include`-then-map, no `AsSplitQuery` needed.

9. **Write-method skeleton**
   - mechanism: (conditional) rate-limit → load story rating → `dto.CanSave(storyRating,isPrimary)` → **sanitize all EditorView HTML** → word-count on sanitized text → build entity with `DateTime.UtcNow` → SaveChanges → refresh derived counters.
   - exemplar: `ServerChapterWriteService.CreateChapterAsync:14-94`.
   - deviations: **authorship guard is absent from 5 of 7 write methods (MA-301)** — the skeleton's ownership-check step (per `identity-and-authorization.md`) is missing; only `MoveChapterAsync`/`DeleteChapterAsync` have it. `ContentCreate` throttle is conditional on authenticated axis (nullable `AuthorId` contract).

10. **Endpoint & client shape**
    - mechanism: `Map{Feature}Endpoints` colocated per cluster; writes wrap `EndpointHelpers.ExecuteWriteAsync`; client impls inherit read→write (CQRS-lite), per-class `ThrowIf*Async` status→exception switch; nullable reads use `GetNullableFromJsonAsync`; downloads are plain `<a href>` GET endpoints (`Results.File`, no `[Authorize]` — "export = what you can read").
    - exemplar: `ExportEndpoints.cs:17-54`, `ClientChapterWriteService.cs:74-92`.
    - deviations: `ReadingProgressEndpoints` returns bare `Results.Accepted()` (fast-ping, no ExecuteWriteAsync) but `RequireAuthorization()` breaks the anonymous-no-op contract under WASM (MA-302). `ClientContentImportService.Resplit` is sync-over-async (`SendAsync().GetAwaiter().GetResult()`) — the one supported synchronous WASM HTTP path, extensively documented as a flagged UI-thread-block trade.

11. **Sanitization & derived fields**
    - mechanism: **every chapter-body / author-note / import-draft HTML passes `IHtmlSanitizationService.Sanitize` before persist**; word count computed on the *sanitized* text via `ChapterText.CountWords` (Core, dependency-free). Import is defended twice: `ImportHtmlNormalizer` maps toward the allowlist, then `CreateDraft` sanitizes (trust boundary), then the write service re-sanitizes on commit.
    - exemplar: `ServerChapterWriteService.cs:33-36`; `ServerContentImportService.CreateDraft:163-176`.
    - deviations: **none — the MA-201 (S2) stored-XSS class is CLEAN here on every EditorView-fed and file-fed write path** (unit-tested with hostile input, `ContentImportTests.cs:303-314`). NB export writers (`HtmlWriter:39`, `EpubWriter`) embed `LongDescriptionHtml` verbatim trusting sanitize-on-save — correct by contract, but downstream of S2's MA-201 (story long-description is NOT sanitized on save), so exported HTML/EPUB carries that residue until MA-201 is fixed (cross-ref, not re-filed).

12. **Notification triggering**
    - mechanism: n/a in slice — chapter writes trigger no notifications directly (new-chapter fan-out is deferred/owned elsewhere); `MarkStartedAsync` (USI, S4) is called post-mark but is not a notification.
    - exemplar: —
    - deviations: none.

13. **Counter updates**
    - mechanism: `Story.WordCount` recomputed from scratch (`SumAsync` of primary contents); author `WordsWritten` updated by atomic `ExecuteUpdateAsync(SetProperty(x + delta))`; the signal-buffer flush upsert uses idempotent `GREATEST`/`OR` merges (retry-safe).
    - exemplar: `ServerChapterWriteService.RefreshStoryWordCountAsync:385-407`; `ReadingProgressFlusher.cs:38-42`.
    - deviations: `chapter.VersionCount++` (`:142`) is a tracked increment — acceptable (author-serialized, no concurrency) but the one non-atomic counter in slice (H-13 note).

14. **Test idioms**
    - mechanism: pure Core (segmenter/splitter/CountWords/writers) → Unit (direct construction, custom collapse options); DB behavior → Integration with `SeedUserAsync`/`SeedStoryAsync` + `SetActiveUser(FakeActiveUserContext…)`; components → RazorComponents (bUnit), fakes for write services; import round-trips through the export writers as fidelity proof; sanitization pinned with hostile-input assertions.
    - exemplar: `ContentImportTests.cs:303-314` (hostile HTML sanitized), `ChapterListSegmenterTests` (17+ frontier/arc cases).
    - deviations: **no test seeds a second, non-author user** — so the MA-301 authorization gap has zero regression coverage (`ChapterWriteServiceTests.cs:28-29` single seeded user, set active). ReadingProgress flush tests correctly remove the timer worker and flush deterministically.

15. **Code economy** (FIXED feature set — not scope)
    - **Per-cluster LOC + pattern-tax:** Chapters ≈ 4,050 product (ServerChapterReadService 289 / WriteService 408 / ReadMark 123 / signal-buffer quartet 293 / 3 endpoints 218 / ChapterReadingPage 386 / ChapterEditorPage 561 / ChapterList 316 / segmenter+items 257 / rest); Import ≈ 1,150 (service 235 / normalizer 196 / splitter 111 / 5 readers 220 / endpoints 94 / 3 UI 572 / client 164); Export ≈ 1,300 (6 writers 1,113 / service 107 / dom+model 80 / endpoints 56). Pattern-tax (endpoints + client-impl + DTO boilerplate) ≈ 900 LOC — the sanctioned CQRS/L5 uniform tax (calibration #3), not compressible without breaking the body-swap axiom.
    - **Import readers — headline verdict: GENUINELY per-format, NOT boilerplate (false economy to merge).** Docx(53, Mammoth style-map)/Epub(94, VersOne spine+nav)/Html(22, AngleSharp body)/Txt(32, blank-line paras)/Markdown(19, Markdig) share *nothing* but the `(string,List<ImportWarning>)`/`string` return shape; each is irreducibly library-specific. They already converge at the one shared normalize→sanitize→count pipeline in `ServerContentImportService`. DRY-ing would fabricate a fake abstraction over five unrelated libraries. **False economy considered and rejected.**
    - **Export writers — GENUINELY per-format at the emit level; ONE modest shared-walk trade.** Html(66, string embed) and Epub(175, XHTML re-serialize) are structurally unique — not duplicative. Pdf(234)/Docx(328)/Txt(131)/Markdown(179) each re-implement the *same* block-walk dispatch (`switch` on P/H2/H3/BLOCKQUOTE/UL/OL/default) and inline-walk dispatch (`switch` on BR/STRONG/EM/U/S/A/text) over `ExportDom.ParseFragment`. Compression candidate: a shared `IHtmlBlockVisitor`/`IHtmlInlineVisitor` walk over the 13-tag DOM, with per-writer emit callbacks. LOC saved ≈ 80–120 (the 4 duplicated dispatch skeletons → 2 shared dispatchers); sites collapsed = 8 walk methods → 2; machinery cost = one visitor abstraction + 4 visitor impls (which carry most of the format-specific LOC anyway, so net saving is modest). **Classify: trade** — the dispatch structure is mechanical repetition, but the emit target genuinely differs per format; Brian decides whether the shared-walk indirection is worth ~100 LOC. Not a pure win, not a false economy.
    - **Mechanical repetition w/ fixable root cause:** `RequireAuthenticatedUser()` duplicated ×2 in slice, ×6 codebase-wide (MA-308 / MA-210) — an `IActiveUserContext.RequireUserId()` extension collapses all. `ImportReviewPanel` per-file/warning glue is genuinely distinct from `ChapterFileImport` (single vs bulk) — not duplicative.
    - **Near-identical pairs:** none — no Desktop/Mobile split in slice (ChapterReadingPage/ChapterList render one responsive tree). ClientChapterReadService/WriteService follow the mandated inherit-split.
    - **False economies considered + rejected:** (a) merging the 5 import readers behind one interface (above); (b) folding `ChapterFileImport` (modes 1/2, single) into `StoryChapterImport` (modes 3/4/5, bulk) — different commit targets (`SetHtmlAsync`/`AddAlternateVersionAsync` vs bulk `CreateChapterAsync` loop), disciplined separation; (c) unifying the reading-progress buffer/flusher/worker into one class — the singleton/scoped/BackgroundService lifetime split is load-bearing (fresh scope per flush).
