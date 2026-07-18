# Slice 3 — Chapters & Ingestion (Features 6, 7, 44, 54, 63)

Audited 2026-07-17 by the S3 slice agent. Read-only pass; no builds/tests run — all `verify: [pending]`.
Scope: Chapters (F6 Writing/Versioning, F7 Reading, F44 Reading-Progress), Export (F54), Import (F63).
Atoms `RichTextView`/`EditorView`/`ContentSurface`/`InlineAlert`/`ConfirmDialog`/`DraftAutosave` are
S0's (cited where consumed, not re-audited). `StoryPage`/`StoryDeck`/`StoryEditorPage` are S2's
(the import panels mount on `StoryEditorPage`; cited, not re-audited). EF `ChapterConfigurations.cs`
is S1's.

**Overall read:** quality is very high — the signal-buffer pipeline (ReadingProgress) is textbook, the
segmenter/splitter are pure and well-tested, import sanitization is defended twice and tested with
hostile input, the six export writers and five import readers are genuinely per-format (not
boilerplate). The findings are dominated by ONE real security gap — chapter write/edit paths that
never verify authorship while their now-live L5 endpoints are open to any authenticated user (MA-301)
— plus the recurring cross-slice patterns (silent catch, manual `/not-found`, token debris, aria-label
gap) already established in S0/S2.

## File inventory (path + LOC)

### Product — Core (Chapters / Import / Export)
| LOC | File |
|---|---|
| 37 | Chapters/Chapter.cs |
| 39 | Chapters/ChapterContent.cs |
| 18 | Chapters/UserChapterInteraction.cs |
| 42 | Chapters/ChapterReadingDto.cs |
| 17 | Chapters/ChapterTocEntryDto.cs |
| 20 | Chapters/ChapterVersionDto.cs |
| 31 | Chapters/ChapterListEntryDto.cs |
| 14 | Chapters/ChapterExportDto.cs |
| 65 | Chapters/ChapterListItem.cs |
| 192 | Chapters/ChapterListSegmenter.cs |
| 44 | Chapters/ChapterText.cs |
| 77 | Chapters/ChapterValidations.cs |
| 10 | Chapters/ChapterValidationException.cs |
| 33 | Chapters/CreateChapterDto.cs |
| 32 | Chapters/UpdateChapterContentDto.cs |
| 74 | Chapters/IChapterReadService.cs |
| 79 | Chapters/IChapterWriteService.cs |
| 33 | Chapters/IChapterReadMarkWriteService.cs |
| 21 | Chapters/IReadingProgressWriteService.cs |
| 43 | Import/ImportFormat.cs / 26 SplitStrategy / 24 ImportWarning / 15 ImportedChapterDraft / 25 ImportParseResult / 12 ResplitRequest / 21 ImportLimits / 10 ImportException / 39 IContentImportService |
| 18 | Export/ExportFormat.cs / 9 StoryExportResult / 28 IExportService |

### Product — Server (Chapters / Import / Export)
| LOC | File |
|---|---|
| 289 | Chapters/ServerChapterReadService.cs |
| 408 | Chapters/ServerChapterWriteService.cs |
| 123 | Chapters/ServerChapterReadMarkWriteService.cs |
| 109/103/59/22 | Chapters/{ReadingProgressBuffer,ReadingProgressFlusher,ReadingProgressFlushWorker,ServerReadingProgressWriteService}.cs |
| 139/40/39 | Chapters/{ChapterEndpoints,ChapterReadMarkEndpoints,ReadingProgressEndpoints}.cs |
| 235 | Import/ServerContentImportService.cs |
| 196 | Import/ImportHtmlNormalizer.cs |
| 111 | Import/ChapterSplitter.cs |
| 53/94/22/32/19 | Import/{DocxReader,EpubImportReader,HtmlFileReader,TxtReader,MarkdownReader}.cs |
| 94 | Import/ContentImportEndpoints.cs |
| 107 | Export/ServerExportService.cs |
| 51/29 | Export/{ExportDom,StoryExportModel}.cs |
| 175/234/66/131/179/328 | Export/{EpubWriter,PdfWriter,HtmlWriter,TxtWriter,MarkdownWriter,DocxWriter}.cs |
| 56 | Export/ExportEndpoints.cs |

### Product — SharedUI (Chapters / Import)
| LOC | File |
|---|---|
| 386 | Chapters/ChapterReadingPage.razor |
| 561 | Chapters/ChapterEditorPage.razor |
| 152 | Chapters/ChapterPropertiesForm.razor |
| 33 | Chapters/ChapterEditorViewModel.cs |
| 160 | Chapters/ChapterNavigation.razor |
| 316 | Chapters/ChapterList.razor |
| 167 | Chapters/ChapterManagerPanel.razor |
| 78/346/148 | Import/{ChapterFileImport,StoryChapterImport,ImportReviewPanel}.razor |

### Product — Client (Chapters / Import)
| LOC | File |
|---|---|
| 45/93 | Chapters/{ClientChapterReadService,ClientChapterWriteService}.cs |
| 52 | Chapters/ClientChapterReadMarkWriteService.cs |
| 24 | Chapters/ClientReadingProgressWriteService.cs |
| 164 | Import/ClientContentImportService.cs |

**Product total ≈ 7,416 LOC** (excludes S0 atoms / S2 StoryPage / S1 configs).

### Tests owned by this slice (all read/sampled)
| Tier | Files (LOC) |
|---|---|
| Unit | ChapterTextTests (95), ReadingProgressBufferTests (121), ChapterListSegmenterTests (287), ContentImportTests (316), ExportWritersTests (199) |
| Integration | ChapterReadServiceTests (299), ChapterWriteServiceTests (362), ReadingProgressFlushTests (207), ExportServiceTests (202), ImportCommitTests (63), ChapterReadMarkServiceTests (281), ChapterReorderDeleteTests (374) |
| RazorComponents | ChapterNavigationTests (313), ImportReviewPanelTests (132), ChapterListTests (467) |

**Test total ≈ 3,718 LOC.** Scope note: no dedicated `ChapterReadingPageTests`/`ChapterEditorPageTests`/
`StoryChapterImportTests` (Quill/JS-backed pages are manual-boot-gated per `audit/Chapters.md` WU26 —
consistent with recorded precedent). `ExportWritersTests`/`ExportServiceTests` are Export's;
`ContentImportTests`/`ImportCommitTests`/`ImportReviewPanelTests` are Import's.

---

### MA-301 | Tier 1 | Bucket A | Slice 3
claim: Five of the seven `ServerChapterWriteService` methods — `CreateChapterAsync`, `AddAlternateVersionAsync`, `UpdateChapterContentAsync`, `SetPrimaryVersionAsync`, `SetPublishedAsync` — and the editor read `GetChapterForEditAsync` perform **no server-side authorship/ownership check**, while their now-live L5 endpoints are gated only by `RequireAuthorization()` (any authenticated user). Since the Global Flip made these HTTP endpoints reachable directly (F6/F7 L5 = Stage 5), any logged-in user can create, edit, re-rate, promote-to-primary, publish/unpublish, or read the draft source of chapters on **any** story they don't own — a broken-access-control / IDOR gap on the site's core authoring surface. Only `MoveChapterAsync`/`DeleteChapterAsync` verify `story.AuthorId == userId`.
evidence: `TheCanalaveLibrary.Server/Chapters/ServerChapterWriteService.cs:14-94` `CreateChapterAsync` stamps `AuthorId = ActiveUser.UserId` but never checks the *story's* owner; `:148-181` `UpdateChapterContentAsync` loads `ChapterContent`, validates rating, saves — no owner compare; `:183-208` `SetPrimaryVersionAsync` / `:210-220` `SetPublishedAsync` likewise; contrast `:222-231` `MoveChapterAsync` — `if (story.AuthorId != userId) throw new UnauthorizedAccessException(...)` and `:281-282` `DeleteChapterAsync` same. `ServerChapterReadService.cs:258-288` `GetChapterForEditAsync` returns any chapter's edit source (incl. unpublished drafts) with no author gate. Endpoints expose all of these to any authenticated user: `ChapterEndpoints.cs:70-125` (writes) `.RequireAuthorization()` only; `:63-66` `GetChapterForEditAsync` `.RequireAuthorization()` only — the class doc comment at `:20-31` itself flags "the other five write methods … carry no service-level ownership check today — same pre-existing gap." `ClientChapterWriteService.cs:83-85` even translates 403 → "This operation requires the story's author." — a 403 the server never actually produces. Convention: `identity-and-authorization.md` §"Security vs affordance" — "**Every write path must load the entity and verify `entity.OwnerId == IActiveUserContext.UserId`, throwing `UnauthorizedAccessException` on mismatch.** The UI `@if` … is convenience UX — it is not a control"; kind (d) Server enforcement "WU24+"; kind (f) "Editing is **author-only** (strict identity-equality)." The audit's own claim (`audit/Chapters.md` WU26 note: "`ServerChapterWriteService` is the real authority") and `ChapterManagerPanel.razor:6-7` ("author-gates every mutation") are contradicted by the code.
cells: F6 L2 + F6 L3-Logic (ServerChapterWriteService), F7 L2 (GetChapterForEditAsync) — all Stage 5, **proposes reopen**
effort: M | route: Stage-4 reconcile (load the parent story / chapter and compare `AuthorId` against `ActiveUser.UserId` in all five write methods + the edit read, throwing `UnauthorizedAccessException`; add integration tests seeding a second non-author user — none exists today: `ChapterWriteServiceTests.cs:28-29` seeds a single user and sets it active, so no test pins the gap)
verify: [pending]

### MA-302 | Tier 2 | Bucket A | Slice 3
claim: An anonymous reader's scroll ping throws under the WASM runtime. `ReadingProgressEndpoints` carries `RequireAuthorization()` and `ClientReadingProgressWriteService` ends with `EnsureSuccessStatusCode()`, so an anonymous viewer on the (public) reading page gets a 401 → `HttpRequestException` on every scroll tick — directly contradicting the interface's documented "anonymous viewers no-op" contract, which the server body still honors. With global InteractiveAuto now live, the WASM path is reachable in production; the exception propagates out of the `[JSInvokable] OnScrollProgress` handler.
evidence: `TheCanalaveLibrary.Server/Chapters/ReadingProgressEndpoints.cs:29-35` — `group.MapPost("/", …).RequireAuthorization();`, doc `:16-21` admits "an anonymous caller now gets 401 instead of a silent no-op … Flagged for the eventual WASM flip / browser debug wave rather than resolved here"; `TheCanalaveLibrary.Client/Chapters/ClientReadingProgressWriteService.cs:20-22` — `await http.PostAsync(...); response.EnsureSuccessStatusCode();`; `TheCanalaveLibrary.SharedUI/Chapters/ChapterReadingPage.razor:350-353` `OnScrollProgress` `await ReadingProgress.RecordProgressAsync(...)` (page is public — no `[Authorize]`, `:1-3`). Contract: `Core/Chapters/ServerReadingProgressWriteService.cs:18` "Anonymous viewers no-op (interface contract)"; `IReadingProgressWriteService` doc. Self-flagged, not yet resolved.
cells: F44 L5 (Stage 5) — proposes reopen
effort: S | route: Stage-4 reconcile (drop `RequireAuthorization()` on the ping endpoint so anonymous stays a no-op, or make the client tolerate 401 for this fire-fast signal; browser-verify an anonymous WASM read)
verify: [pending]

### MA-303 | Tier 2 | Bucket A | Slice 3
claim: `ChapterReadingPage`'s scroll-tracker disposal swallows all exceptions in a bare `catch {}` with a prose-only comment — the unregistered-silent-catch class `logging.md` forbids, identical to S2's MA-206 and S0's MA-001. A non-disconnection failure of the dispose JS call is hidden with no log and no registry entry.
evidence: `TheCanalaveLibrary.SharedUI/Chapters/ChapterReadingPage.razor:375` — `try { await JS.InvokeVoidAsync("readingProgress.dispose"); } catch { /* page already gone or no JS */ }` (bare `catch`, no `// sanctioned-silent:` annotation, not in the registry). `logging.md` §"No silent catches": "A silent catch is legal only when annotated at the catch site: `// sanctioned-silent:` … Registry of sanctioned sites." (The sibling `catch (JSException)` at `:344` is typed + expected/commented — the sanctioned register path — and is not flagged.)
cells: F7 L3-Logic (Stage 5) — proposes reopen (same class already open as MA-206)
effort: S | route: mechanical sweep (narrow to `catch (JSDisconnectedException)`/`catch (JSException)`, or annotate + register)
verify: [pending]

### MA-304 | Tier 2 | Bucket A | Slice 3
claim: Four chapter-dispatcher missing-entity branches navigate to a literal `/not-found` route (200 + client redirect, not an HTTP 404) instead of the sanctioned `NavigationManager.NotFound()` — the same MA-202 (S2) class, extending it into Chapters. Deleted-chapter / deleted-story URLs therefore return 200, the SEO/crawler concern (F64) `render-and-layout.md` calls out.
evidence: `TheCanalaveLibrary.SharedUI/Chapters/ChapterReadingPage.razor:292` — `NavigationManager.NavigateTo("/not-found");`; `TheCanalaveLibrary.SharedUI/Chapters/ChapterEditorPage.razor:248` (missing story), `:273` (unresolvable content id), `:286` (missing edit source) — all `NavigationManager.NavigateTo("/not-found");`. Zero uses of `Nav.NotFound()` in slice. Convention: `render-and-layout.md` §"NavigationManager.NotFound()" — "Use in page dispatchers when the requested entity doesn't exist … Replaces manual navigation to error pages." (`ChapterReadingPage.razor:44-47` also renders an inline "Chapter not found." `<p>` for the null-primary path — a third, distinct not-found mechanism.)
cells: F7 L3-Logic (ChapterReadingPage), F6 L3-Logic (ChapterEditorPage) — Stage 5, proposes reopen (bundle with MA-202's sweep)
effort: S | route: mechanical sweep (swap the four to `Nav.NotFound()`; browser-verify a deleted-chapter URL returns 404)
verify: [pending]

### MA-305 | Tier 3 | Bucket A | Slice 3
claim: `ImportReviewPanel`'s remove button uses the bare-name `bg-danger` utility and the raw palette color `text-white`, which `layer4-style.md`'s token rule forbids ("raw palette/hex colors" fail the build via `check-design-tokens.ps1`). This is the exact pre-existing violation `audit/Chapters.md`'s WU45 build note references ("the only finding is pre-existing (`ImportReviewPanel.razor`, untouched by WU45)") — still unfixed.
evidence: `TheCanalaveLibrary.SharedUI/Import/ImportReviewPanel.razor:61` — `class="rounded-md bg-danger px-2 py-1 text-white transition-colors hover:bg-(--color-danger-strong)"` — `bg-danger` (bare, not the paren-form `bg-(--color-danger)` used everywhere else) and `text-white` (raw Tailwind palette). Contrast the sibling commit button `:78` `bg-(--color-action) … text-(--color-text)` (correct). Convention: `layer4-style.md` §"Element Roles"/token rule; the CI script `scripts/check-design-tokens.ps1` (S1 evidence) fails on raw palette outside the sanctioned exemption list (which does not include `ImportReviewPanel`).
cells: F63 L4 (Stage 5) — proposes reopen (cosmetic; CI already flags it)
effort: S | route: mechanical sweep (`bg-(--color-danger)` / `text-(--color-danger-ink)` per the danger Control recipe)
verify: [pending]

### MA-306 | Tier 3 | Bucket C | Slice 3
claim: `ChapterReadingPage` launches the attribution-source write as fire-and-forget with no exception observation — a `_ = …Async()` whose failure (most plausibly an HTTP error under WASM) becomes an unobserved `TaskScheduler` exception rather than a logged/tolerated loss. Same class as S0's MA-002/MA-011; documented as "fire-and-forget" in the WU26 note but not as loss-tolerant-*with-observation*.
evidence: `TheCanalaveLibrary.SharedUI/Chapters/ChapterReadingPage.razor:327` — `_ = RecommendationWriteService.RecordAttributionSourceAsync(StoryId, queryRecId);` (no `await`, no continuation, no try/catch). `calibration.md` unwritten-pattern #? / H-12: "every `_ = SomeAsync(...)` … is failure observed (log/restore) or can it die silently?" (`RecordAttributionSourceAsync` is Recommendations/S5's write service — the call *site* is this slice's.)
cells: F44/F7 L3-Logic (attribution capture; no cell change proposed — cosmetic)
effort: S | route: mechanical sweep (wrap in a logged fire-and-forget helper, or `await` inside a try/catch that logs Warning)
verify: [pending]

### MA-307 | Tier 3 | Bucket A | Slice 3
claim: `ChapterPropertiesForm` wraps three `EditorView` instances but its submit button carries no `aria-label`, against `testing.md`'s rule that every button in an `EditorView`-wrapping component must have a unique aria-label to stay selectable past Quill's toolbar buttons — identical to S2's MA-212 on `StoryPropertiesForm`. Separately, `ChapterList`'s icon-only read-toggle button carries `title` + `aria-pressed` but no `aria-label`.
evidence: `TheCanalaveLibrary.SharedUI/Chapters/ChapterPropertiesForm.razor:37,45,55` embed three `<EditorView>`; `:98-102` submit `<button type="submit" …>@SubmitLabel</button>` (no aria-label). `TheCanalaveLibrary.SharedUI/Chapters/ChapterList.razor:218-222` — icon-only read-toggle `<button … title="Mark read / unread" aria-pressed=…>` with an SVG child only, no `aria-label`. Convention: `testing.md` §"BlazoredTextEditor button collision" — "any button on a component that wraps `EditorView` MUST carry a unique `aria-label`"; a11y Stage-5 criterion (icon-only controls carry aria-label).
cells: F6 L4.5-area / F7 L4.5-area (test-friction; no cell change)
effort: S | route: mechanical sweep (add aria-labels)
verify: [pending]

### MA-308 | Tier 3 | Bucket C | Slice 3
claim: The `RequireAuthenticatedUser()` guard (identical `UserId is not int → throw InvalidOperationException`) is re-implemented in each write service of the cluster — the same MA-210 (S2) mechanical repetition with a fixable root cause, now two more copies.
evidence: `TheCanalaveLibrary.Server/Chapters/ServerChapterWriteService.cs:375-380` and `TheCanalaveLibrary.Server/Chapters/ServerChapterReadMarkWriteService.cs:117-122` — byte-identical private helpers; `ServerReadingProgressWriteService.cs:18` inlines the same `is int userId` gate. Cross-refs MA-210's four copies in Stories/Series.
cells: F6/F44 L2 (no cell change)
effort: S | route: seam — direction undetermined (an `IActiveUserContext.RequireUserId()` extension in Core would collapse every copy codebase-wide; Brian decides)
verify: [pending]

### MA-309 | B-flag | Bucket B | Slice 3
claim: The three import *parse* endpoints (`/single`, `/document`, `/epub`) run expensive, attacker-influenced work — Mammoth DOCX conversion, VersOne EPUB decompression + AngleSharp DOM walks, up to 20 MB/file and up to 500 segments/chapters — behind `RequireAuthorization()` only, with **no `IWriteRateLimitService` throttle**. `security.md` throttles the *commit* (`ContentCreate` on `CreateChapterAsync`) but nothing bounds repeated parse calls, an authenticated CPU/memory-amplification surface. Whether `security.md`'s "unbounded … write method" clause is meant to cover expensive authenticated *reads/parses* is a scope question, not a settled rule — flagging, not ruling.
evidence: `TheCanalaveLibrary.Server/Import/ContentImportEndpoints.cs:62-90` — four `MapPost` handlers, each `.RequireAuthorization()`, none calling `rateLimit.EnsureAllowed(...)`; `ServerContentImportService.cs` injects only `IHtmlSanitizationService` + `ILogger` — no `IWriteRateLimitService`. Per-file/-segment caps exist (`ImportLimits.cs`: 20 MB, 100 files, 500 EPUB chapters, 500 split segments) but no per-user rate cap on parse frequency. Convention: `security.md` §"Write Throttling" — "Every *new* abuse-prone write method (creates content another user sees, or is **unbounded**) adds a call under an existing kind."
cells: F63 L2/L5 (security posture; no cell change)
effort: S | route: doc-touch decision (scope the throttle rule to cover expensive parse endpoints, or record parse as deliberately unthrottled because commit is the real cost gate)
verify: [pending]

---

## Hypothesis results (slice 3)

- **H-01** (`@key` on stateful list children): **clean** — `ChapterManagerPanel.razor:35` keys its stateful draggable rows `@key="chapter.ChapterId"`. `ChapterList` rows hold no per-row *component* state (read-toggle state lives in the parent's `_readOverrides` dict keyed by ChapterId, not a child field) → no `@key` needed. `ImportReviewPanel`'s `@for` renders plain `<li>`/`<input>`/`RichTextView` (no stateful child component caching a param/CTS) — outside the H-01 target class; noted-not-filed that its reorder-able uncontrolled `<input>`s lack `@key` (cosmetic bleed risk only, no state corruption).
- **H-02** (route-param reload discipline): **clean** — `ChapterReadingPage` (`:243-254,261-284`) and `ChapterEditorPage` (`:206-238`) both implement the guarded sentinel + `_initialized` reload with **plain-assign on non-initial** for `[PersistentState]` fields (`Chapter`/`Toc`/`Versions`/`EditSource` set to null then re-fetched), `??=` restore-or-fetch only on `initialLoad`. `ChapterManagerPanel` (`:99`) `??=` restore, mutations plain-assign (`:108`).
- **H-03** (unnamed `HasIndex` overwrite): **n/a** — EF configs are S1's (`ChapterConfigurations.cs`); no `HasIndex` in slice product code.
- **H-04** (read-context factory-per-method): **clean** — `ServerChapterReadService` opens `await using … readDbFactory.CreateDbContextAsync()` per method (7 methods); base exposes `protected IActiveUserContext ActiveUser`; write services hold only `writeDb` (+ buffer). No shared read-context field. The flusher opens a fresh scope per flush (`ReadingProgressFlusher.cs:73`), never capturing a scoped context in the singleton.
- **H-05** (dead Tailwind classes): **MA-305** — `ImportReviewPanel.razor:61` `bg-danger` (bare) + `text-white` (raw palette), the pre-existing CI-flagged violation. Paren-form throughout the rest of the slice; bare-name semantic tokens (`bg-surface` etc.) are the sanctioned dual style.
- **H-06** (unregistered silent catches): **MA-303** — `ChapterReadingPage.razor:375` bare `catch {}`. Every other catch logs or translates: `ServerContentImportService.cs:137-142` (`catch (Exception)` → `LogWarning` + rethrow as `ImportException`), `DocxReader`/`EpubImportReader` catch→throw `ImportException`, the write-service/editor catches route through `ExceptionPresenter`/`LogError`.
- **H-07** (stale/untracked TODO comments): **clean** — no `TODO`/`HACK`/`FIXME` in slice. (The doc-vs-code staleness lives in H-11, not a TODO.)
- **H-08** (`Nav.NotFound()` vs manual `/not-found`): **MA-304** — `ChapterReadingPage` (×1) + `ChapterEditorPage` (×3) `NavigateTo("/not-found")`; zero `Nav.NotFound()`. Extends S2's MA-202 into Chapters.
- **H-09** (dispatcher load parallelism): **clean** — `ChapterReadingPage.LoadChapterAsync` (`:300-312`) `Task.WhenAll`s its four independent loads (toc/versions/storyListing/arcs) — it IS in the layer2-sanctioned parallelizing list. `ChapterEditorPage.LoadAsync`'s awaits are a genuine dependency chain (story rating → versions → content id → edit source → toc), not independent sequential awaits.
- **H-10** (debounced/pending writes lost on dispose): **clean/n-a-honored** — the reading-progress signal is loss-tolerant *by contract* (`ReadingProgressBuffer` doc; `layer2` §"Signal Buffering"), honored + documented: `OnScrollProgress` writes to the singleton buffer synchronously (no per-component debounce/timer), `ChapterReadingPage.DisposeAsync` only tears down the JS ref — no pending write to lose. The manual read-mark path is durable-direct (immediate `SaveChangesAsync`) and even `Discard`s in-flight buffered pings before saving (`ServerChapterReadMarkWriteService.cs:57`) so a racing flush can't resurrect an overridden state.
- **H-11** (doc-vs-code staleness): **feeds MA-301** — `audit/Chapters.md` WU26 note ("`ServerChapterWriteService` is the real authority") and `ChapterManagerPanel.razor:6-7` ("author-gates every mutation") both assert an authorship enforcement the code performs for only 2 of 7 write methods; `identity-and-authorization.md` kind (d)/(f) say chapter editing must be author-gated server-side. No *new* standalone doc-staleness instance surfaced.
- **H-12** (fire-and-forget without observation): **MA-306** — `ChapterReadingPage.razor:327` `_ = RecordAttributionSourceAsync(...)`. `ClientReadingProgressWriteService`/`ClientChapterReadMarkWriteService` calls are all awaited; `StoryChapterImport`/`ChapterEditorPage` import commits are awaited in try/catch.
- **H-13** (denormalized counter discipline): **clean (one noted borderline)** — `ServerChapterWriteService.RefreshStoryWordCountAsync:403-405` updates `WordsWritten` via atomic `ExecuteUpdateAsync(SetProperty(us => us.WordsWritten + wordDelta))`; `Story.WordCount` is recomputed from scratch (`:389-391 SumAsync`), not `++`. Noted-not-filed: `AddAlternateVersionAsync:142` `chapter.VersionCount++` is a *tracked* increment, but on a single-author-serialized aggregate (only the owning author adds versions to their own chapter) — no concurrency window, so the atomic-ExecuteUpdate rule's rationale doesn't bite.
- **H-14** (elevated reads annotated + named): **n/a** — zero `IgnoreQueryFilters` calls in slice. `GetChapterForEditAsync` deliberately leaves the Story-level filter active (comment `ServerChapterReadService.cs:260-264`), no bypass.
- **H-15** (write-path by-id lookups bypass ContentRating): **clean by construction** — every write-service existence check reads the unfiltered `writeDb` (`ServerChapterWriteService.cs:22,103,116,150,185,212,227,277`; `ServerChapterReadMarkWriteService.cs:22,70,74`), which carries no named filters — the phantom-`KeyNotFound` class can't occur; no readDb PK fetch in a write path.
- **H-16** (`[FromQuery]` on non-GET arrays): **clean** — no array/collection params on slice POST/PUT handlers; `ContentImportEndpoints` binds `IFormFile` (multipart) + the scalar `ImportFormat` enum via query string (documented, `:28-31`); reading-progress/read-mark writes use scalar query params.
- **H-17** (nullable-return client reads use tolerant helpers): **clean** — `ClientChapterReadService` uses `GetNullableFromJsonAsync` for the three `Task<T?>` reads (`GetChapterForReadingAsync:22`, `GetViewerLastInteractionUtcAsync:38`, `GetChapterForEditAsync:41`); non-nullable lists use `GetFromJsonAsync<List<T>> ?? []`. Server returns `Results.Json(nullable)` (`ChapterEndpoints.cs:43,57,65`), which the tolerant helpers cover.
- **H-18** (aria-labels on icon-only + EditorView-adjacent buttons): **MA-307** — `ChapterPropertiesForm` submit (wraps 3 EditorViews) and `ChapterList` icon-only read-toggle lack aria-labels. Others are labeled: `ChapterManagerPanel` drag handle (`aria-label="Drag to reorder"`), `ChapterList` download summary (`aria-label="Download chapter"`), `ImportReviewPanel` move up/down (`aria-label="Move up/down"`), `ChapterNavigation` prev/next.
- **H-19** (AuthorizeView-gated DI wrapper/inner split): **clean** — `ChapterEditorPage`/`ChapterManagerPanel` are reachable only under page-level `[Authorize]` (`StoryEditorPage` edit mode); `ChapterList` injects `IChapterReadMarkWriteService` and renders for anonymous viewers on the public `StoryPage`, but only *calls* it under the parent-computed `CanMarkRead` gate — injection resolves for anyone, no anonymous-construction crash (the NotificationBell class needs a gated *service that 401s on construction*, which this isn't).
- **H-20** (feedback-channel discipline): **clean** — `ChapterManagerPanel:18`, `ChapterList:31`, `ChapterFileImport:22`, `StoryChapterImport:62`, `ChapterPropertiesForm:14` all use `InlineAlert` for validation/operation errors; editor catches route through `ExceptionPresenter` (`ChapterEditorPage.razor:395-400`, `StoryChapterImport.razor:335-339`); no hand-rolled `<div role="alert">` validation blocks, no raw `ex.Message` in UI, no toast carrying validation.
