# Audit — Stories/

**Features:** 4, 5, 8, 9, 10, 45 (Story creation/editing, browsing/display, arcs, series, lineage,
view-count). Largest cluster.

## Shared Context

> **2026-07-18 — Desktop/Mobile fork removed (WU-ResponsiveMerge).** `StoryDesktop`/`StoryMobile`
> merged into `StoryPage` (page renders its own markup; mobile variant deleted as an unvalidated
> placeholder); MA-209 display statics centralized in `StoryDisplayFormat`. Narrow rendering is
> provisional (graceful degradation) pending the future mobile phase. Desktop/mobile structure
> assertions elsewhere in this file are historical. Rules: `canalave-conventions/render-and-layout.md`
> §"Responsive Layout Architecture"; spec §3.9/§3.10 superseded on this axis.
> Verified 2026-07-18: full suite green post-merge (Unit 702 / Integration 727 / RazorComponents
> 510); browser smoke at desktop width clean (loads, no error banner, zero console errors);
> narrow rendering deliberately unpolished, no visual pass yet.

**Entities (Core/Stories/):** `Story` (hot partition — status, counts, dates, FKs), `StoryListing`
(warm — title, short desc, cover art URL; carries the FTS `SearchVector` generated column), `StoryDetail`
(cold — long desc, slug, `PostApprovalStatus`). Vertical-partition trio with 1-to-1 cascade. `Story`
implements `IEditableStoryProperties` via explicit interface implementation (`[NotMapped]` projections
across the three partitions) — a deliberate, somewhat advanced pattern that lets one object satisfy the
edit contract. Also here: `StoryArc`, `StoryLineage` (+type lookup, renamed from `StoryRelationship`/
`StoryRelationshipType` in WU42, 2026-07-12 — see Feature 10 below), `StoryTag`/`StoryCharacter`
live in the model set but belong to Tags. **`Series`/`SeriesEntry` moved to their own `Core/Series/`
vertical cluster (WU41, 2026-07-11)** — same namespace (`TheCanalaveLibrary.Core`), separate folder;
see Feature 9 below.

**DTOs/contracts (Core/Stories/):** `CreateStoryDTO`, `StoryUpdateDTO`, `StoryDetailsDTO`, `StoryTagDTO`,
`IEditableStoryProperties`, `IStoryTag`, `StoryMappers`, `StoryValidations`, `StoryValidationException`.
**As of 2026-06-27 (sprite redesign):** `TagChipDto.SpriteIdentifier` replaces `TagChipDto.SpriteUrl`
in the character-chip projections in `ServerStoryReadService`. The read service drops its
`ISpriteReadService` dependency; sprite resolution is now in the `CharacterEntry.razor` component.

**Services:** `IStoryReadService` / `IStoryWriteService` (Core) + `ServerStoryReadService` /
`ServerStoryWriteService` (Server, direct-injection pattern per spec §6.6 — see RESOLVED note below) +
`HttpStoryReadService` / `HttpStoryWriteService` / `HttpStoryOverviewService` (Client). `StoryEndpoints`
(Server) maps the API.

**Components (SharedUI):** `StoryPage` (dispatcher, `/story/{StoryId:int}/{Slug?}`),
`StoryDesktop`/`StoryMobile` (stubs), `StoryPropertiesForm` + `StoryPropertiesViewModel` (now
`SharedUI/Stories/` — moved out of the legacy `Components/StoryProperties/` folder ahead of the
cluster's actual build; see `canalave-conventions/SKILL.md` "Code Organization"). **The relocation is
folder-only — content is unchanged and remains the Stage-4 build-to-spec scaffolding described in
Feature 4 below; do not treat the new location as endorsement of the existing content.**

**Fluent config:** inline in `ApplicationDbContext.OnModelCreating` — Story FK fan-out (Cascade to owned
collections, Restrict to `StoryStatus`, `SetNull` author anonymization), partition 1-to-1s, FTS computed
column + GIN index `ix_story_listing_search_vector`, slug unique-filtered index.

---

## Feature 4 — Story Creation & Editing

**WU-ErrorHandling note (2026-07-06).** `StoryEditorPage` embeds `DraftAutosave`
(`draft:story:{id|new}`; prose fields only — Title/ShortDescription/LongDescription; structured
tag/character picker state deliberately excluded, re-selecting is cheap, re-typing is not);
backup cleared on successful submit. `StoryPropertiesForm` renders errors via `InlineAlert` and
gained `SetLongDescriptionAsync` (Quill push for restore). The page's generic catch routes
through `ExceptionPresenter` + `LogError`. `StoryDeck` wraps each `StoryCard` in a compact
`story-card` boundary island — one broken card degrades to a tile, the deck survives. Strategy:
`error-handling.md` §"Error Handling Strategy"; detail: `workplan.md` WU-ErrorHandling.

**Settled — cover upload persists its own reference (owner ruling D14's sub-ruling, answered
2026-08-07; settled for WU-BlobCleanup 2026-10-13). Do not revisit:**
- `IStoryWriteService.UploadCoverArtAsync` patches `StoryListing.CoverArtRelativeUrl` itself and
  commits: write the blob → persist the column → best-effort delete the old blob. Clearing a cover is
  its own operation, `RemoveCoverArtAsync`. The old two-step (upload, then the page patches the DTO and
  calls `UpdateStoryAsync`) is retired.
- `CoverArtRelativeUrl` is off `IEditableStoryProperties`, `StoryUpdateDTO` and `CreateStoryDTO`, so
  `UpdateStoryAsync` cannot touch the cover. `GetStoryForEditAsync` returns `StoryEditDTO`
  (`StoryUpdateDTO` plus the cover as a read echo).
- The editor uploads on pick in edit mode (the new cover shows without a save) and after
  `CreateStoryAsync` in create mode. Form-cancel no longer reverts a cover: an accepted consequence,
  with no revert action built.
- This supersedes the L3.5 note's "cover-art save-first-then-upload ordering" (WU24) below.
Rule of record: `layer2-services.md` §"Image Storage — blob ownership and cleanup".

**Author story deletion — settled (owner ruling D15, answered 2026-08-07; settled for
WU-AuthorStoryDelete 2026-10-13). Do not revisit:**
- Archive permanence is rejected. `DeleteStoryAsync` is a true hard delete: self-service, no reason,
  no moderator, irreversible. Rejected alternatives, recorded rather than built: soft delete,
  `IsWithdrawn`, a grace window, a tombstone or "[deleted story]" page, a cooldown, mod approval, a
  "can't delete evidence" gate, and co-author or joint deletion (D15: not a deferral).
- Primary author only (`AuthorId`); a co-author cannot delete, and a NULL author owns nothing.
- Unconditional under moderation: open reports, `PendingApproval` and `IsTakenDown` never block it.
- Mechanism: one transaction — story-scope `TptDelete`, the Spotlight slot release, the story row;
  everything else cascades at the database (`daily_story_stats` included — D15's "no FK" premise was
  false); the cover blob after the commit; no counter touched (`ApprovedStorySubmissions` monotonic).
- An unstarted Spotlight booking returns its slot to the sponsor; a started one is consumed.
- UX: a destructive control on the edit page opens a typed-title dialog that names what goes with the
  story and offers the two outs first — Unpublish instead (a published story only) and a download.
- A taken-down story keeps its delete on the edit page (D15 rules the takedown case; the review fixes,
  2026-10-13, built it): the author's edit read lifts the takedown and the page opens delete-only.
- Feature 5: no tombstone means the story's page and chapter URLs simply 404 afterwards; notifications
  that named the story render as inert rows (accepted dangles).

**Open (owner) — `roadmap.md` decision row 27 / tracker F24, plus F13:**
- (a) a block that is live when the story is deleted is treated as started (slot consumed);
- (b) the moderator hard delete does not return Spotlight slots;
- (c) the sponsor is not told a slot came back;
- (d) the dialog's download covers published chapters only, and a taken-down story's export 404s;
- (e) nothing links an author to a taken-down story's delete-only editor (the Authored tab, the story page
  and the takedown notification all leave it out), so they need its address;
- tracker **F13** site 5: open reports on the deleted story and its comments stay Open, out of the queue.
Rule of record: `layer2-services.md` §"Author story deletion".

- **L1 — Stage 5 (re-verified WU-StoryLifecycle, 2026-09-30 — migration `WU_StoryLifecycle`: nullable `published_date`, new `submitted_date`; see its Stage note; `ck_stories_published_date` since WU-SchemaHardening, 2026-10-13 — Stage note at the end of this feature).** Partition trio + `IEditableStoryProperties` plumbing is sound and matches spec §4/§7.
  Awaiting migration + build verification (no migrations exist). *Settled:* three-table vertical split;
  slug server-generated; explicit-interface edit contract. **Settled by owner ruling D1 (worksheet
  2026-08-04), before the WU-StoryLifecycle build (2026-09-30) — do not revisit: status is off the
  property-edit path.** `StoryStatusId` is not on `IEditableStoryProperties`/`CreateStoryDTO` and the
  mapper never copies it; a new story is always `Draft`; `UpdateStoryAsync` never changes status; every
  lifecycle move goes through `TransitionStatusAsync` (author) or approve/reject (moderator).
  `PostApprovalStatus` stays an ordinary editable property. Rule of record: `layer2-services.md`
  §"Story Lifecycle".
- **L2 — Stage 5 (the structured-tag gate trusts only `Tag` rows and refuses a doubled pairing member,
  WU-ParityAndRemaining 2026-10-13, its type read untracked since its review fixes the same day — Integration-tested, stage unchanged, rule and Stage note in
  `audit/Tags.md` Feature 12; a refused save's error list now reaches WASM line for line instead of
  "Story validation failed." (service audit §2.7.1), Unit- and Integration-tested — the
  WU-ParityAndRemaining note under Feature 4's Stage notes; re-verified WU-StoryLifecycle, 2026-09-30 — Draft-on-create, `TransitionStatusAsync`, status off the update path; re-verified WU-BlobCleanup, 2026-10-13 — the cover off the update path, `UploadCoverArtAsync` persists it, `RemoveCoverArtAsync`, `StoryEditDTO`; Integration-tested (`StoryCoverTests`, `StoryEndpointsTests`), and browser-driven on both render phases with `psql` by its browser pass the same day; extended WU-AuthorStoryDelete, 2026-10-13 — `DeleteStoryAsync`, owner ruling D15's author hard delete (story-scope `TptDelete`, the Spotlight slot return, the cover blob after the commit), Integration-tested (`StoryDeletionTests`, `BlobCleanupTests`, `StoryEndpointsTests`); and since its review fixes the same day the edit read reaches a taken-down story's author (flagged `IsTakenDown`; anyone else gets the missing answer), the story-scope comment delete locks the chapters first, and a lost commit acknowledgement is not replayed into a 404 (Integration `StoryDeletionTests` +3); browser-driven on both render phases with `psql` by its browser pass the same day, on Local and on Garage; see the Stage notes below).** **RESOLVED (2026-06-20):** this cell's prior "Stage 5, nothing to reconcile" call was
  wrong — `DbStoryWriteService`/`DbStoryReadService` injected `IDbContextFactory<T>`, which was never
  registered in `Program.cs` (only plain `AddDbContext<T>` existed), so DI container validation failed at
  app startup. Surfaced by actually running the Aspire AppHost end-to-end. Per spec §6.6 ("Why Direct
  DbContext Injection over IDbContextFactory" — superseded for thread-safety reasons that don't apply
  under scoped DI), rewrote as `ServerStoryReadService(ReadOnlyApplicationDbContext readDb)` and
  `ServerStoryWriteService(ReadOnlyApplicationDbContext readDb, ApplicationDbContext writeDb) :
  ServerStoryReadService(readDb), IStoryWriteService` — primary-constructor injection, `readDb` private or
  base. Registered via `AddScoped<>` in `Program.cs` (already scoped; only the implementation type names
  changed). *Open:* cover-art upload to R2/MinIO not implemented; slug generation not visible in the write
  path.
  **WU12 (2026-06-22) closed the open item, partially:** slug generation built (server-only,
  `ServerStoryWriteService.GenerateUniqueSlugAsync` — slugify `Title`, Tier-3 uniqueness check against
  `StoryDetails.Slug`, suffix-disambiguate on collision; never on any DTO, never client-editable). A
  real NRE was found and fixed in `StoryMappers.ToStory()` — a fresh `new Story()` had null
  `StoryListing`/`StoryDetail` navs, dereferenced one line later by
  `UpdateStoryEditableProperties()`; now both partitions are initialized before mapping. The original
  `CreateStoryAsync`'s `writeDb.Attach(...)` calls on those navs were also a second, compounding bug
  (`Attach` marks Unchanged — would have skipped inserting the listing/detail rows entirely) — removed;
  `Stories.Add(newStoryDB)` alone correctly cascades the connected graph as Added. Cover-art upload is
  **not** stubbed away — `IImageStorageService`/`LocalImageStorageService` now exist (see
  `audit/ImageStorage.md`); the write path still takes `CoverArtRelativeUrl` as a pass-through string
  (no upload UI wired here — that's WU24, which now has a ready service to call).
  **How verified:** `dotnet build` green (0 warnings/errors, all 4 projects); live server boot clean;
  via `/dev/wu12/*` diagnostics (kept as standing dev tools, not removed) — `CreateStoryAsync` called
  twice with the same title produced `wu12-mature-story` then `wu12-mature-story-2` (confirmed via
  `psql`), no NRE on either call. **WU12.5 (2026-06-22)** migrated this verification into asserted,
  CI-runnable tests — `StoryWriteServiceTests` in `TheCanalaveLibrary.Tests.Integration` covers the
  same NRE/`Attach`-vs-`Add`/slug-disambiguation regressions against a real Postgres; the dev-
  diagnostics endpoints are no longer the source of truth for this behavior (see
  `canalave-conventions/testing.md`).
- **L3-Logic — Stage 5 (re-verified WU-StoryLifecycle, 2026-09-30 — editor page drives the lifecycle panel in place; re-verified WU-BlobCleanup, 2026-10-13 — the editor uploads a cover on pick and removes it in place, never through the save, and since its review fixes the same day its three writes (a cover call, the save, a status change) never overlap; RazorComponents-tested (`StoryEditorPageTests`), and browser-driven on both render phases by its browser pass the same day; extended WU-AuthorStoryDelete, 2026-10-13 — the page drives the story delete (saved-title confirmation, draft backup cleared, leaves for the author's Authored tab) and the dialog's Unpublish instead, and the delete is a fourth write that never overlaps the others; since its review fixes the same day a taken-down story opens delete-only (and the create route never inherits that state), and the post-delete navigation's `forceLoad` is pinned; RazorComponents-tested (`StoryEditorPageTests`), and browser-driven on both render phases by its browser pass the same day (tracker H34 closed); see the Stage notes).** **WU24 (2026-06-23):** `ServerStoryWriteService` now enforces author ownership
  on both create and update paths: `CreateStoryAsync` stamps `AuthorId` from `IActiveUserContext.UserId`
  (the client-settable `AuthorId` property is removed from `CreateStoryDTO`) and throws
  `InvalidOperationException` for unauthenticated callers; `UpdateStoryAsync` loads the story, checks
  `story.AuthorId != activeUser.UserId`, and throws `UnauthorizedAccessException` for non-owners. No
  mod/admin OR in this gate — moderation is a separate WU34 path (see `identity-and-authorization.md` "Security vs
  affordance"). `ITagReadService` gains `GetTagChipsByIdsAsync(IReadOnlyList<int>)` — bulk chip lookup
  by exact ID for edit prefill; `ServerTagReadService` implements it following the same
  sprite-resolve-at-projection pattern as `SearchTagChipsAsync`. `StoryPropertiesViewModel` no longer
  implements `IEditableStoryProperties` (decoupled from wire DTOs; mapping is at page layer).
  `Routes.razor` switched from `RouteView` to `AuthorizeRouteView` so `[Authorize]` attributes are
  enforced (was silently ignored with `RouteView`).
- **L3.5-Structure — Stage 5 (re-verified WU-StoryLifecycle, 2026-09-30 — the all-enum Status select is gone; new `StoryLifecyclePanel` + "Status when published" select; re-verified WU-BlobCleanup, 2026-10-13 — `StoryPropertiesForm` raises `OnCoverSelected`/`OnRemoveCover`, `CoverUploadBusy`, a "Remove cover" control, and since its review fixes `OtherWriteInFlight` plus a submit disabled under a cover call; RazorComponents-tested (`StoryPropertiesFormTests`); extended WU-AuthorStoryDelete, 2026-10-13 — new `StoryDeleteDialog` on `Modal` (typed-title input, Unpublish instead, the download links, Cancel then the filled-danger delete) and the editor's Delete story section; since its review fixes the same day the title match collapses whitespace runs (as the label shows them) and a taken-down story's dialog shows one line in place of both outs; RazorComponents-tested (`StoryDeleteDialogTests`), and browser-driven on both render phases by its browser pass the same day; see the Stage notes).** **WU24 (2026-06-23):** `StoryPropertiesForm` fully rebuilt (Bootstrap →
  Tailwind, all 6 `TagSelector` instances wired with `OnSelectionChanged` + `InitialTagsByType` prefill,
  `EditorView @ref` pull-on-submit via public `GetLongDescriptionAsync()`, `InputFile` for cover art,
  `Rating`+`Status` selects, server validation error display, `IsLoading` disabled-submit guard). No
  `@inject` — presentational, passes `StoryPropertiesViewModel` and `OnValidSubmit`; page owns all I/O.
  `StoryEditorPage.razor` created: both `@page "/story/new"` and `@page "/story/{StoryId:int}/edit"` +
  `[Authorize]`; thin dispatcher (loads story + tag chips for edit prefill, maps ViewModel ↔ DTOs on
  submit, cover-art save-first-then-upload ordering, surfaces `StoryValidationException` and
  `UnauthorizedAccessException` into `ViewModel.ServerValidationErrors`). Pattern 1 edit side (view
  side is WU25). Admin/edit controls: no named component — inline `@if` and edit-page links per the
  "Owner-Conditional Edit Affordances" convention (see `layer3.5-structure.md`).
- **L4-Style — Stage 5 (re-verified WU-BlobCleanup, 2026-10-13 — the "Remove cover" danger-text Control and the disabled picker and submit states use the role recipes; re-verified WU-AuthorStoryDelete, 2026-10-13 — the Delete story opener uses the danger-tint recipe and the dialog's delete the filled-danger one; `check-design-tokens` passes; visual sign-off still waits for the Phase-3 pass).** **WU24 (2026-06-23):** all form markup uses Tailwind v4 design tokens
  (`--color-text`, `--color-surface`, `--color-border`, `--color-primary`, `--color-danger`), consistent
  with the locked token set. Responsive: `grid-cols-1 md:grid-cols-2` for Rating/Status row; single
  column otherwise. Visual sign-off pending human review (Stage-6 gate: cannot verify Tailwind layout in
  bUnit).
- **L4.5-Browser — Stage 5 (1→5, WU-AuthorStoryDelete browser pass, 2026-10-13: the Delete story
  section and `StoryDeleteDialog` driven on the circuit and on WASM — the typed-title gate, the saved
  title under an unsaved edit, Unpublish instead in place, the downloads, the delete and its landing, a
  taken-down story's delete-only editor, a double-space title, a booked slot returned, and the
  refusals (404 on both phases, 403 on WASM) shown in the dialog — with `psql`, the upload folder and
  the Garage bucket after every write; tracker **H34** closed, no runtime bug. It went 5→1 at
  WU-AuthorStoryDelete earlier that day, with no browser. Before that, Stage 5 (1→5, WU-BlobCleanup browser pass, 2026-10-13:
  the cover flow — upload on pick, "Remove cover", create-with-cover, a refused file, the three writes
  kept apart — driven on the circuit and on WASM with `psql` and the upload folder after every write,
  which closed trackers H31 and H3; it had gone 5→1 at WU-BlobCleanup earlier that day, with no browser.
  Before that: browser-verified 2026-09-30 by the WU-StoryLifecycle browser pass; it was 1 from the
  review fixes earlier that day until that pass ran)).** The WU-StoryLifecycle pass drove every lifecycle move, the "Status when published"
  select and the published-story editor on both phases against `psql`, and found and fixed three runtime
  bugs: a refused save killed the circuit, a published story's editor 301'd to its story page on a full
  load, and the rejection reason rendered as "_rejectionReason". The WU-BlobCleanup pass drove the
  cover-art `InputFile`, the 2026-07-01 cluster note's standing exception, for the first time; it found
  no bug and filed the feedback polish as tracker **H32**. The WU-AuthorStoryDelete pass drove the
  delete dialog and its two outs as two authors, a sponsor and a non-author, and found no bug. Detail:
  the three browser Stage notes below.
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13; extended WU-StoryLifecycle, 2026-09-30 — `POST /api/stories/{id}/status` + client impl; extended WU-BlobCleanup, 2026-10-13 — `DELETE /api/stories/{id}/cover` + client impl, `GET …/edit` returns `StoryEditDTO`; the client impls Unit-tested (`ClientStoryCoverServiceTests`) since its review fixes; extended WU-AuthorStoryDelete, 2026-10-13 — `DELETE /api/stories/{id}` + client impl, Integration (`StoryEndpointsTests`: 204/403/401/404) and Unit (`ClientStoryDeleteServiceTests`); its review fixes the same day add the `IsTakenDown` echo to `GET …/edit`'s `StoryEditDTO`, a plain JSON field; the WASM leg browser-driven by its browser pass the same day — the `DELETE`, its 403 and 404 mapped to readable messages in the dialog).** (History: the dead pre-WU12 `HttpStoryReadService`/
  `HttpStoryWriteService` were deleted 2026-06-27 in the filter revamp; the real surface was rebuilt
  by WU-L5Sweep.) Endpoints + client impl live (WU-L5Sweep) and the site now runs global
  InteractiveAuto; story CREATE with tags verified end-to-end in a real WASM runtime during the
  flip's browser wave (`IStoryTag` polymorphism round-trip; create→edit `forceLoad` fix). Full wave
  narrative + the 7 bugs found/fixed: `workplan.md` WU-GlobalFlip.
- **L6 — Stage 5 (WU-L6, 2026-07-07 — resolved, no creation-side DDL).** The write path needs no
  index beyond the existing PK/unique/slug set; the story-table read indexes ("to be added by
  query need") landed as the sort spines under Feature 5 (see its L6 note).

### Feature 4 — WU-ParityAndRemaining Stage note (2026-10-13) — no flip: a refused save reads the same on WASM

Service audit §2.7.1, worst for stories: the server put a validation exception's error list into
`ProblemDetails.Detail` only, joined, and `StoryValidationException`'s message is the fixed "Story
validation failed.", so on WASM a refused save or transition showed that sentence instead of the
errors (the gap WU-StoryLifecycle's browser pass recorded; tracker **H12**). Since 2026-10-13
`EndpointHelpers.ExecuteAsync` adds an `errors` string array for every `CanalaveValidationException`,
and `ClientHttpHelpers.ThrowIfWriteFailedAsync` feeds it to the client's factory, now
`Func<IReadOnlyList<string>, Exception>` — a compile-enforced sweep of every client validation factory
(list families pass the list; Tag, Import and the BCL types join it). The read is tolerant: any other
`errors` shape, or none, falls back to `[detail]`. Rule: `layer5-wasm.md` §"The Error-Translation
Contract", `error-handling.md` §"The API error envelope". Tier: Unit `ValidationErrorsWireContractTests`
(11: both server halves, the round trip, the joined family, five fallback shapes, two non-JSON bodies)
and Integration `ApiErrorEnvelopeTests` (+3: `POST /api/stories` carries every `CanSave` message; the
real `ClientStoryWriteService` over the real host rebuilds the exact list; a non-validation 400 has no
`errors`). Mutation-checked by suppressing the extension. No browser was available: H12's remaining WASM
re-check is now step 1 of tracker **H37**. F4 L4.5 stays 5 (no markup changed).

### Feature 4 / Feature 5 — WU-StoryLifecycle Stage note (2026-09-30) — the story lifecycle is enforced; publish dates mean "first went live"

**No cell flips — F4 and F5 stay Stage 5 on every layer they had** *(F4 L4.5 was flipped 5→1 later
the same day by the review fixes — see the next note)*. This closed defects *beneath*
sound cells: owner rulings D1 (mandatory-for-first-submission approval queue + a server-side
transition table) and D2 (nullable publish dates, NULL = never published) were answered
2026-08-04 and had not been built. Before this WU an author set `StoryStatusId` freely through the
property mapper (self-publish, un-reject, enter the queue with an illegal post-approval status),
`CanSubmitForApproval` had no server caller, and `Story.PublishedDate` was stamped at creation,
so every long-drafted story sorted as stale and every draft counted as a "new story" in L8.

**What changed (F4):**
- **L1:** `Story.PublishedDate` → `DateTime?`; new `Story.SubmittedDate`. Migration
  `WU_StoryLifecycle` (shared with Chapters/Identity) backfills data-preservingly: pending rows
  keep their queue date in `submitted_date`, `published_date` is nulled for Draft/Pending/Rejected,
  and `post_approval_status` values outside the entry set are remapped to InProgress. Verified on
  a populated clone of the dev DB (Up and Down both run; every backfill row checked with `psql`),
  not only on the empty Testcontainers schema.
- **L2:** `StoryStatusId` left `IEditableStoryProperties`/`CreateStoryDTO`; the mapper no longer
  copies it; `CreateStoryAsync` stamps Draft and no publish date. New `TransitionStatusAsync` applies
  the pure `Core/Stories/StoryLifecycle` table (trust-waiver routing, one conditional
  `ExecuteUpdate` guarded on the status it read, `PublishedDate ?? now`, never touches
  `LastUpdatedDate`/`IsTakenDown`). `CanSave` rejects undefined `Rating`/`PostApprovalStatus`.
  `GetStoryForEditAsync` echoes `StoryStatusId` and `RejectionReason` (read-only).
- **L3/L3.5:** the all-enum Status select is gone. `StoryPropertiesForm` shows a "Status when
  published" select (entry set only) while the story is unpublished; the new presentational
  `StoryLifecyclePanel` (submit / withdraw / revise / move / unpublish) sits above the form in edit
  mode, and `StoryEditorPage` applies the RESULTING status in place — deliberately no same-route
  `NavigateTo` (the Quill `removeChild` hazard).
- **L5:** `POST /api/stories/{id}/status?target=` + `ClientStoryWriteService.TransitionStatusAsync`.
  Known interim gap: on WASM a refused transition shows "Story validation failed." (the fixed
  `StoryValidationException.Message`) until WU-ParityAndRemaining's P1 carries error lists over HTTP.

**What changed (F5):** `StoryDetailsDTO.PublishDate`/`StoryExportModel.PublishDate` are nullable;
`StoryPage` and every export writer render "Not yet published" for NULL (only the author can see
such a story). DatePublished sorts are unchanged: Postgres `DESC` puts NULLs first, so an author
sees their own never-published drafts at the top of their own DatePublished views — **accepted and
recorded, not a bug** (a NULLS LAST tweak would defeat `ix_stories_published_date`).

**How verified:** `dotnet build` green, no new warnings in touched files. `dotnet test` green —
Unit 1,010, RazorComponents 686, Integration 1,117. Tiers: **Unit** — `StoryLifecycleTests`
(exhaustive current × target × trusted theory against a literal legal-move table, plus routing,
refusal messages, set predicates), `StoryValidationsTests` (enum binding), `StoryMappersTests`
(status never copied, even from a forged echo), `ExportWritersTests` (null date in every writer),
`ClientStoryLifecycleServiceTests` (route + resulting-status round-trip). **Integration** —
`StoryLifecycleTests` (Draft-on-create incl. an import with an original date, untrusted/trusted/
revoked submit, withdraw, revise, six illegal targets, non-author/missing, published moves +
unpublish + trusted republish keep the original date, `LastUpdatedDate` untouched, no-op, an
in-queue edit stays Pending and ignores a forged echo, rejection-reason echo), `StoryEndpointsTests`
(200 with the resulting status, 400 illegal/undefined, 401, 403). **RazorComponents** —
`StoryLifecyclePanelTests`, `StoryPropertiesFormTests` (no live-status select; entry-set options).
Also exercised over HTTP against a freshly seeded scratch DB (unpublish → resubmit kept the
publish date; self-approve → 400). **Not browser-verified** — no browser was available; the
editor-page panel (including the in-place WASM re-render next to Quill) is tracker **H12**.
Open owner questions the build did not decide: tracker **F9**. Rule of record:
`layer2-services.md` §"Story Lifecycle".

### Feature 4 / Feature 5 — WU-StoryLifecycle review-fixes Stage note (2026-09-30) — F4 L4.5 5→1; takedown freezes status; unpublish confirms

**Cell change: F4 L4.5-Browser 5 → 1** (headline above) — the publishing flow it vouched for no longer
exists, and the replacement was never browser-driven. All other F4/F5 cells stay 5. Three independent
reviews of the WU-StoryLifecycle commit found:

- **L2 — a taken-down story could re-enter the queue.** D1 confines `Rejected` to pre-publication so
  that rejection and takedown never overlap, but nothing stopped an author from unpublishing a
  taken-down story over `POST /api/stories/{id}/status` (the editor can't load it, so the UI block was
  affordance only), resubmitting it, and having a moderator approve it (+1 trust) or reject it
  (overwriting the takedown's own reason/date). `TransitionStatusAsync` now refuses every move on an
  `IsTakenDown` row, and its conditional `WHERE` carries `!IsTakenDown` too (a takedown racing the
  move affects 0 rows). Rule: `layer2-services.md` §"Story Lifecycle" ("a taken-down story's status is
  frozen") — derived from D1's rationale, flagged in the workplan entry.
- **L3/L3.5 — Unpublish fired immediately.** `layer3.5-structure.md` names "unpublishing a story" as
  a `ConfirmDialog` consumer and `layer4-style.md` as a destructive confirm; the panel now opens a
  destructive `ConfirmDialog` whose copy says the story disappears for readers and may need a
  moderator's review to come back (true for an author whose auto-approve was revoked). The draft-state
  helper copy no longer promises that later stories always publish straight away.
- **F5:** the "Not yet published" branch on `StoryPage` gained its bUnit cases.
- **Migration comment corrected.** `WU_StoryLifecycle`'s backfill comment claimed a pulled-back Draft
  "didn't exist before this WU". It could: unpublish wasn't a *sanctioned* move, but the old all-enum
  Status select let an author set any status. So the backfill nulls `published_date` on any
  Draft/Pending/Rejected row, including one that was once live (no column recorded that), and such a
  row is stamped as new if republished. Accepted: pre-launch dev data only. From this WU on,
  `PublishedDate IS NULL` reliably means "never published".

**How verified:** `dotnet test` green (counts in `workplan.md`'s WU-StoryLifecycle entry, review-fixes
bullet). **Integration** — `StoryLifecycleTests.TakenDownStory_RefusesEveryAuthorMove_AndChangesNothing`
(4 cases) and `…_TrustedSubmit_IsAlsoRefused`; `Transition_StatusChangesBetweenReadAndWrite_*` and
`Transition_TakedownLandsBetweenReadAndWrite_*` drive the conditional `WHERE` itself through the new
`InterleavingCommandInterceptor` (the competing write lands between the read and the update — a
sequential test is always stopped by the pre-read; `testing.md` §"Testing a check-then-act guard").
Each was mutation-checked: dropping the status predicate, the `!IsTakenDown` predicate or the
pre-check fails it. **RazorComponents** — `StoryLifecyclePanelTests` (Unpublish asks first, is
destructive, and Cancel requests nothing); new `StoryEditorPageTests` (the page applies the RESULTING
status: a trusted submit asks for `PendingApproval` and shows Completed + "Published." with the
"Status when published" select gone; untrusted → Pending + "Submitted for review."; confirmed unpublish
→ Draft with the select back; a refusal keeps the status and shows the server's text; 403 → forbidden
state — mutation-checked against applying the target instead of the result); `StoryPageTests` (the
publish date, and "Not yet published" for NULL). **Not browser-verified** — no browser was available;
tracker **H12** now covers the confirm dialog too.

### Feature 4 / Feature 5 — WU-StoryLifecycle browser-verification Stage note (2026-09-30) — F4 L4.5 1→5; three runtime bugs fixed

**Cell change: F4 L4.5-Browser 1 → 5** (headline above). Closes tracker **H12**'s author half. The
moderator half is in `audit/Moderation.md` F47/F48. Dev DB wiped and reseeded first: it predated both
2026-09-30 migrations and still carried the WU-UserModeration leftovers. Each phase was confirmed from
the network log, not assumed. The circuit showed `_blazor/negotiate` and no `/api` call for the
action. WASM showed no negotiate and a `POST /api/stories/{id}/status` per move. The circuit pass was
forced by removing the `blazor-resource-hash:TheCanalaveLibrary.Client` localStorage key before a
full load.

**Driven, with `psql` after every write:**
- **TestUser (seeded untrusted):**
  - Create with "Status when published" left at *Choose…*: Draft, `published_date` NULL,
    `post_approval_status` 0. Submit is disabled and the panel shows the save-first hint.
  - Choose Complete and save: Submit enables.
  - Submit: Pending, with `submitted_date` stamped and `published_date` still NULL.
  - Withdraw: Draft.
  - Resubmit: Pending, `submitted_date` re-stamped.
  - Each of these moves was driven on both phases.
  - Own story page: "Not yet published".
- **Refusal (two tabs):** tab B saved the select back to *Choose…*, then the stale tab A clicked
  Submit. The circuit shows the server's sentence ("You must select a Status…"). WASM shows "Story
  validation failed." That is the recorded interim gap (WU-ParityAndRemaining P1); the server's 400
  body itself carries only that detail.
- **After one approval, TestUser is trusted:** the rejected story 8 went *Return to draft*, then
  Submit, then In Progress directly. `published_date` was stamped on its first publish.
- **AuthorAlpha (trusted), on the circuit (this is the GIF):** Submit published directly (In
  Progress, date stamped, `submitted_date` untouched, the select gone). Moved to Complete. Unpublish
  opened the destructive dialog; Cancel left it Completed. Confirm made it Draft with the date kept
  and the select back. Submit republished it with the original date.
- **The same flow on WASM:** On Hiatus, a confirmed unpublish, and a republish with the date kept.
- **Stale refusal on a published move:** tab B unpublished, then the stale tab A asked for a published
  status. The circuit shows "Submit the story for publication instead."; WASM shows the generic text.
  Nothing was written.
- **Revoked AuthorAlpha:** a resubmission went to the queue with its old `published_date` kept.
  After the moderator rejected it, *Return to draft* made it Draft. After the restore, Submit
  published it directly.
- **Anonymous** (fetch without credentials): published stories show "Published Oct 1, 2026"; a
  pending story is a 404.
- No Quill `removeChild` on any in-place status update, on either phase.

**Three runtime bugs found and fixed in this pass** (commit "WU-StoryLifecycle: browser-pass fixes";
each regression test fails when its fix is reverted, checked by hand):
1. **A refused save killed the circuit.**
   - On `/story/new`, any save the server refused (a missing Setting/Genre tag) logged `TypeError:
     Cannot read properties of null (reading 'removeChild')` and a `CircuitHost` fail. It happened
     even with an empty editor.
   - Cause: the page writes the HTML it pulls from Quill back into the view model bound to
     `EditorView.Html`, so the refusal's re-render changed markup Quill had already detached.
   - Fix: `EditorView` freezes its rendered content at the first render. The fix is cross-cutting,
     because `BlogPostEditorPage`, `SiteAnnouncementEditorPage` and `GroupBlogPostEditorPage` share
     the write-back. Rule: `layer5-wasm.md` §"WASM renderer vs third-party DOM" rule 3.
   - Tests (RazorComponents): `EditorViewTests`, and
     `StoryEditorPageTests.RefusedSave_ShowsTheServerMessage_AndLeavesTheQuillContentAlone`.
   - Re-verified: two refused saves on the circuit kept the Quill content, then the same circuit
     created the story. A refused save on WASM was followed by a successful one.
2. **A published story's editor was unreachable by full load** (refresh, bookmark, and the editor's
   own `forceLoad` after Save).
   - Cause: `UseCanonicalStorySlugRedirect` (Feature 64) read `/story/{id}/edit` as a stale slug.
   - Fix and verification: `audit/Seo.md` F64 Stage note.
   - Re-verified: a full load, a Save and the post-save landing on a published story's editor, on
     both phases.
3. **"Reason: _rejectionReason".**
   - `StoryEditorPage` bound the panel's `string` parameter without `@`, so Razor passed the
     literal. It was the only such binding in the UI.
   - Test (RazorComponents): `StoryEditorPageTests.RejectedStory_ShowsTheModeratorsReason_NotTheFieldName`.
   - Re-verified on both phases.

**How verified:** browser (above) and `dotnet test` green: Unit 1,058, RazorComponents 730,
Integration 1,253. Zero `fail:`/`crit:` lines and no console errors after the fixes. The three
`fail:` lines before them were the two crashes of bug 1 plus the expected one from the fresh
database's create. Still open:
- The WASM generic refusal text (P1), which is narrowed tracker **H12**.
- The blog editors were not driven live after the shared fix.

### Feature 4 / Feature 5 — Filter revamp Stage note (2026-06-27)

**What changed:** All four named EF display/visibility filters (`"ContentRating"` on `Story`,
`"GroupAudience"` on `Group`, `"IsTakenDown"` on `Story`/`BaseComment`/`BaseBlogPost`/`Recommendation`)
were moved from `ApplicationDbContext.OnModelCreating` to `ReadOnlyApplicationDbContext.OnModelCreating`.
`_activeUser` on `ApplicationDbContext` changed from `private` to `protected` to allow the subclass to
close over it in its own `OnModelCreating`. The write context (`ApplicationDbContext`) now carries no
visibility filters — it sees ground truth. All display/visibility filters live on the read context only.

**Latent bug closed:** `ServerStoryWriteService.UpdateStoryAsync:51` loaded the story via `writeDb.Stories`
with no `IgnoreQueryFilters` bypass. An author with `ShowMatureContent=false` editing their own M-rated
story got a null result (ContentRating filter on the write context filtered it out) → `KeyNotFoundException`
"Story not found" on their own edit. Fixed by construction: the write context no longer has any filter.

**Bypasses removed (~15):** All `IgnoreQueryFilters` on `writeDb` — 11 in `ServerModerationWriteService`
(`IsTakenDown` on all 4 roots), 1 in `ServerRecommendationWriteService` (`ContentRating`), 2 in
`ServerGroupWriteService` (`GroupAudience` + `ContentRating`), 1 in `ServerBlogPostWriteService`
(`GroupAudience`). None of these were defensive; all existed solely because the write context inherited
filters that writes should never see.

**Elevated reads kept (~7):** Moderation read queue (`IsTakenDown` on Story/Comment/BlogPost/Recommendation)
and `GetStoryIdsByAuthorAsync` (`ContentRating`). Each annotated `// elevated read:`.

**Migration tree removed:** `Migrations/ReadOnlyApplicationDb/` deleted — the read context is never
migrated separately (both contexts share the same schema); this had been accumulating dead artifacts.

**Tests (Integration tier):** `ContentRatingFilterTests` extended with 5 new tests:
- `TakenDownStory_IsInvisible_OnPublicRead` — IsTakenDown filter on read path
- `TakenDownStory_WriteContext_CanStillBeUpdated` — write path sees ground truth after takedown
- `MatureRatedStory_IsVisible_OnWriteContext_WhenAuthorHasMatureContentOff` — line-51 bug regression
- `MatureRatedStory_IsInvisible_OnReadContext_WhenViewerHasMatureContentOff` — companion filter check
`ModerationServiceTests.ResolveWithRemovalAsync_SoftHides_DropsFromPublicQuery_VisibleWithIgnoreFilter`
updated to use `ReadOnlyApplicationDbContext` for the public-visibility assertion (was incorrectly using
the unfiltered write context). All 1232 tests pass.

### WU-CounterAtomicity Stage note (2026-06-27)

**CS9107 eliminated — `ServerStoryReadService` / `ServerStoryWriteService`:**

`ServerStoryWriteService` was double-capturing the primary-constructor `activeUser` parameter — once in
its own body (lines using `activeUser.UserId`) and once passed to the base ctor
`ServerStoryReadService(readDb, activeUser)`. C# primary-constructor semantics count both captures,
raising CS9107. Fix mirrors the pattern already established in `ServerBlogPostReadService`:

- `ServerStoryReadService` now exposes `protected IActiveUserContext ActiveUser { get; } = activeUser;`
  (same comment about CS9107/CS9124 as the blog-post service). Internal reference in `ApplyFilters`
  updated to `ActiveUser.UserId`.
- `ServerStoryWriteService` two `activeUser.UserId` references (auth guard in `CreateStoryAsync`,
  ownership check in `UpdateStoryAsync`) updated to `ActiveUser.UserId`. The ctor parameter now only
  appears in the base-ctor argument — not a captured field — so the warning disappears.

No behavior change. `dotnet build` zero warnings. `dotnet test` 1232/1232 pass (Integration tier,
same existing Story tests — no new tests needed; this is a compiler-warning fix, not a behavioral
change).

### WU-SchemaHardening Stage note (2026-10-13) — Features 4, 8, 10: three CHECK constraints

**No cell flips — F4, F8 and F10 L1 stay Stage 5.** Schema audit §2.6 found no CHECK constraint in the
schema. WU-StoryLifecycle routed its D2 invariant here. Migration `WU_SchemaHardening` adds, declared
in the model (`layer1-data-model.md` §"CHECK constraints"):
- **F4** `ck_stories_published_date` — `story_status_id NOT BETWEEN 2 AND 7 OR published_date IS NOT
  NULL`. A published status carries its publish stamp (D2). It is one-directional, because an
  unpublished story keeps its date. The bounds are interpolated from `StoryStatusEnum`.
- **F8** `ck_story_arcs_bounds` — `start_chapter_number <= end_chapter_number`. The arc service already
  refuses `end < start`, and chapter delete and move drop an arc rather than invert it.
- **F10** `ck_story_lineages_no_self` — `source_story_id <> target_story_id`.

Before applying, every writer was checked: none can produce a violating row, and the dev DB had none.
DataSeeder, SeedTool (re-run on a migrated scratch clone of the dev DB, 2026-10-13: 3,000 stories
loaded) and `SeedStoryAsync` already keep D2.

Tests: **Integration** `SchemaConstraintTests` asserts each constraint by SQLSTATE 23514 and name, that
an unpublished story may keep or lack its date, and that the catalog's CHECK set equals the documented
one.

**HTTP-driven 2026-10-13** by the WU's browser pass (same-origin `fetch` as AuthorAlpha; no flip): an
arc on story 1 with start 3 and end 2 answers 400 "End chapter must not be before the start chapter.",
and a lineage from story 1 to itself 400 "A story cannot have a lineage link to itself.". The service
guards answer before the CHECKs can.

### Feature 4 — WU-BlobCleanup Stage note (2026-10-13; owner ruling D14's sub-ruling) — F4 L4.5 5→1, and 1→5 at its browser pass the same day; L2/L3-Logic/L3.5/L4/L5 stay 5

The Settled note at the top of this feature, built. Rule of record: `layer2-services.md` §"Image Storage
— blob ownership and cleanup". No migration.
- **L2.** `CoverArtRelativeUrl` left `IEditableStoryProperties`, `StoryUpdateDTO` and `CreateStoryDTO`;
  the mapper no longer copies it (the unconditional copy was D14's inverse hazard: an update whose DTO
  omitted the cover nulled the column, and `UpdateStoryAsync` then deleted the live blob), and
  `UpdateStoryAsync`'s old-cover capture and delete branch are gone. `GetStoryForEditAsync` returns the new
  `StoryEditDTO : StoryUpdateDTO` (the cover as a read echo; `IStoryTag` polymorphism rides the base class,
  so it deserializes on WASM). `UploadCoverArtAsync` loads the story with its listing and gates (missing →
  404; not the author, **including an authorless story** → 403 — service audit §2.12's `(int?)` item,
  closed), writes the blob, persists the column, then deletes the old blob best-effort.
  `RemoveCoverArtAsync` clears the column and deletes the blob; no cover is a no-op. **Derived, flagged:**
  both stamp `LastUpdatedDate`, preserving "stamped on every successful property edit"; D2's
  publish-anchor rules may revisit what bumps it.
- **L3-Logic / L3.5.** `StoryPropertiesForm` lost `SelectedCoverFile` (a registered retired term) and
  gained `OnCoverSelected`, `CoverUploadBusy` (the picker disabled, "Uploading…") and `OnRemoveCover` (a
  danger-text "Remove cover" Control, shown only while a cover is set; `check-design-tokens` passes).
  `StoryEditorPage`: in edit mode a picked file is uploaded at once and the returned path shown without a
  save; "Remove cover" calls `RemoveCoverArtAsync`; errors reach the form's list through
  `ExceptionPresenter`, and a 403 renders the forbidden state. In create mode the file waits in
  `_pendingCoverFile` and is uploaded right after `CreateStoryAsync`; the second `UpdateStoryAsync` is
  gone. The save path no longer carries a cover. **Accepted (D14):** form-cancel no longer reverts a
  cover. **Unchanged, noted:** if the create-mode upload fails, the story exists and the page stays on
  `/story/new`, so a resubmit duplicates it (pre-existing; the old flow had the same gap).
- **L5.** `DELETE /api/stories/{id}/cover` → 204 (author), 403, 401, 404;
  `ClientStoryWriteService.RemoveCoverArtAsync`. No throttle: a delete, exempt under D20's
  throttle-coverage half (WU-ThrottleCoverage lists it); uploads stay throttled in `ImageUploadProcessor`.
- **How verified:** build 0 errors, no new warnings in touched files; all three tiers green.
  - **Integration** `StoryCoverTests` (12, over a temp store): the upload alone persists (the edit echo
    shows it, `LastUpdatedDate` moves); a second upload deletes the first file, also through one scope;
    **the inverse-hazard regression** — upload, then `UpdateStoryAsync` from the edit DTO (its cover echo
    nulled) or through the same scope, and the cover and its file are intact; non-author 403 with no file
    written; missing 404; authorless 403; remove nulls and deletes; non-author remove refused; no-cover
    remove writes nothing; missing remove 404. `StoryEndpointsTests` +5: multipart POST persists and
    `GET …/edit` echoes the cover over the wire; DELETE 204/403/401/404.
  - **Unit** `StoryMappersTests` +2: a property edit leaves a stored cover untouched even when handed a
    `StoryEditDTO` with a different echo; `ToStory` leaves the cover unset.
  - **RazorComponents** `StoryEditorPageTests` +5: edit-mode pick uploads once and shows the cover with
    no `UpdateStoryAsync` (D14's RazorComponents test); remove calls `RemoveCoverArtAsync` and hides the
    cover; a refused upload shows its message, keeps the old cover and re-enables the picker; create mode
    uploads nothing until `CreateStoryAsync`, then uploads to the new id with no update; create with no
    cover only creates. `StoryPropertiesFormTests` +5: the pick raises `OnCoverSelected`, busy disables
    the picker and remove and shows "Uploading…", remove renders only with a cover and raises
    `OnRemoveCover`.
  - **Mutation-checked:** not persisting the upload, not deleting the replaced or removed blob, the old
    "authorless = not found" gate, an `UpdateStoryAsync` after the edit-mode or create-mode upload,
    deferring the edit-mode upload to the save, and dropping the busy disable each fail a test.
  - **No browser was available:** F4 L4.5 5→1; tracker **H31** holds the pass on both render phases
    (edit-mode upload, remove, create-with-cover, a refused file).
- **L4.** The "Remove cover" Control is danger-text (`text-(--color-danger)` with a danger-tinted hover
  and the `disabled:` state), and the picker takes the same `disabled:` state; `check-design-tokens`
  passes. No other style changed. Visual sign-off stays with the Phase-3 pass.
- **Review fixes (2026-10-13) — no further flip.**
  - **A cover call could overlap the save (L3-Logic).** The cover became its own immediate-commit call,
    but nothing kept it apart from the save: the habit of the old flow (pick, then Save) started the
    save while the upload streamed. The save's `forceLoad` reload aborted the upload with no error, and
    on a circuit both calls shared the one scoped write context, which refuses a second operation
    mid-flight (`layer2-services.md`, "the write context stays plain `AddDbContext`", which now says
    keeping a page's writes apart is the page's job). The page's three writes — a cover call, the save,
    the status change — are now mutually exclusive: each one's controls are disabled while another
    runs (the form's submit under `CoverUploadBusy` or the new `OtherWriteInFlight`; the picker and
    "Remove cover" under a save or a status change; the lifecycle panel's `IsBusy` under a cover call
    or a save), and each handler re-checks, for an event that beat the re-render. The save-vs-status
    pair predates this WU; it was closed in the same edit so the page states one rule.
  - **Untested paths.** **RazorComponents** `StoryEditorPageTests` +4: a 403 on a cover upload renders
    the forbidden state; while a cover call runs, the submit and the panel are disabled and neither a
    save nor a status change starts; while a save runs, the cover controls and the panel are disabled
    and no cover call or status change starts; while a status change runs, the cover controls and the
    submit are disabled and neither starts. `StoryPropertiesFormTests` +2 (and two extended): the submit
    is disabled under `CoverUploadBusy`; a save or another write in flight disables the picker, remove
    and submit. **Unit** `ClientStoryCoverServiceTests` (7): `RemoveCoverArtAsync` sends a bodiless
    `DELETE /api/stories/7/cover`, and 403/401/404 become `UnauthorizedAccessException` /
    `SessionExpiredException` / `KeyNotFoundException`; `UploadCoverArtAsync` posts one multipart
    `file` part and returns the stored path, and a 400 keeps the server's text; `GetStoryForEditAsync`
    reads the cover echo into `StoryEditDTO` beside the polymorphic tag list.
  - **Docs.** `layer5-wasm.md` and the endpoint comment no longer give "no body" as a CSRF rationale (it
    is none): the DELETE carries no antiforgery token because the middleware checks only form-binding
    endpoints, and its CSRF posture is the JSON API's, which worksheet D38 has yet to record.
  - **Mutation-checked:** dropping the handler guards, or the form's cross-disables, fails the overlap
    tests. No browser: tracker **H31** gains the overlap steps.
- **Browser verification (2026-10-13) — F4 L4.5 1→5; trackers H31 and H3 closed.** Every editor step
  of H31 ran on both render phases: the circuit, proven by `_blazor/negotiate` and no `/api` call, and
  WASM, proven by no negotiate and the `/api/stories/…` requests. After each write, `psql` read
  `story_listings.cover_art_relative_url` and the upload folder was listed. Actors: TestUser (an
  untrusted author), AuthorAlpha (trusted), anonymous. The `InputFile` was driven with a PNG drawn in
  the page (`run-server/SKILL.md`, "Driving an `InputFile`").
  - **Create with a cover.** Picking the file on `/story/new` wrote nothing. Create opened the edit page
    showing the cover, and the column held it. On WASM the network showed `POST /api/stories`, one
    `POST …/cover` and no `PUT`.
  - **Edit-mode upload.** The new cover rendered with no save, the column already held it, and the old
    file was gone. After leaving the page without saving, the story page showed the new cover and the
    old title.
  - **Save after upload.** The reload kept the cover, and the title edit saved (`PUT` 204 on WASM).
  - **Remove cover.** The image and button went at once, the column read NULL and the file was gone.
  - **A refused file.** A text file named `.png` got "This file isn't a JPEG, PNG or WebP image."
    (`POST …/cover` 400 on WASM). The old cover stayed and the picker re-enabled.
  - **No overlapping writes.** A `MutationObserver` recorded each state change. Under an 8.8 MB upload,
    the picker, "Remove cover", Save and the lifecycle buttons were all disabled while "Uploading…"
    showed. Save was then force-enabled and clicked mid-upload. The handler ignored it: no reload, no
    `PUT`, the title unsaved and the cover persisted. During a Save, and during a status change
    (Submit, Withdraw), the picker and "Remove cover" were disabled. The server log had no "second
    operation" line.
  - **The API's refusals.** Anonymous `POST`/`DELETE …/cover` answered 401. As a non-author they
    answered 403, and on a missing story 404; none wrote a file.
  - **Aspire (Garage).** A cover replaced on WASM made the old object's URL 404 and the new one 200.
    The bucket held 2 objects after the replace, as after the create: the cover and a July orphan.
    Create-with-cover on WASM stored its object.
  - **One runtime bug, fixed** (commit "browser-pass fixes"): on the server-only path, an upload that
    was in `wwwroot/uploads/` at build time kept answering 200 (empty) after its delete. Detail:
    `audit/ImageStorage.md`'s Stage note. It was never in the editor's flow, because a cover uploaded
    at run time 404s once deleted.
  - **Not bugs, filed as tracker H32 (polish):** a refused file is reported in the form's top alert,
    about 530 px above the picker. "Uploading…" also shows while "Remove cover" runs. The native picker
    keeps the last file name, and a status change leaves an old cover refusal on screen.
  - No browser console errors. The server-only log had no `fail:`/`crit:` line. The Aspire log's one
    `fail:` was the documented antiforgery line for a cookie from the other database's key ring.

### Feature 4 (and 5) — WU-AuthorStoryDelete Stage note (2026-10-13; owner ruling D15) — F4 L4.5 5→1, and 1→5 at its browser pass the same day; L2/L3-Logic/L3.5/L4/L5 stay 5

The "Author story deletion — settled" note at the top of this feature, built. Rule of record:
`layer2-services.md` §"Author story deletion" (the slot return in §"Community Spotlight"). Verified by
`dotnet build` (0 errors, no new warnings) and `dotnet test` green on all three tiers, plus the four gates.

- **L2.** `IStoryWriteService.DeleteStoryAsync` + `ServerStoryWriteService.DeleteStoryAsync`: owner gate
  (projection on the unfiltered write context, so a reported, pending or taken-down story passes it), then
  one execution-strategy transaction — tracker cleared, cover path captured, `TptDelete.StoryCommentsAsync`,
  `SpotlightPlacementRelease.ReleaseUnstartedForStoryAsync` (new, `Server/Spotlight/`, under the booking
  lock now shared through `SpotlightLocks.BookingLockSql`), the story `ExecuteDeleteAsync` with the author
  in its `WHERE` (0 rows → `KeyNotFoundException`, a lost race), commit — then the cover blob best-effort
  and one `LogInformation`. No counter moves. Integration `StoryDeletionTests` (18 cases): D15's own list
  (zero surviving `base_comments` for comments and replies across two chapters, contents gone past the
  RESTRICT `primary_content_id`; the reader's favorite, list/series/group entries, the recommendation, the
  lineage link and the co-author row gone while the containers stay; the profile blog post survives with
  `StoryId` NULL and the pin clears; `daily_story_stats` rows gone and `SiteDailyStatAggregator` counting
  only the surviving story's views; non-owner, co-author, anonymous and missing each refused with the story
  intact; open report / taken down / pending each deleted with the report unchanged;
  `ApprovedStorySubmissions` and `CanAutoApprove` untouched; a future block's slot back to Available, a live
  and an aired one consumed, another story's booking untouched), plus an authorless story refused, a
  dangling notification reading back with a null title and link, a delete through a scope still tracking
  the author's reply, a sequential second delete (404), an interleaved lost race (404, rolled back) and
  the booking lock held when the story row is deleted. `BlobCleanupTests` +4 (the cover deleted after the
  commit with the Information trace; no cover, no attempt; a failed blob delete still deletes, warns, and
  the sweep collects; a refused delete keeps the blob). **Mutation-checked:** removing the story-scope
  `TptDelete`, the slot release, the tracker clear, the 0-row check, the blob delete, the release's
  `StartDate` test (or widening it to `EndDate`), its story filter, or its booking lock each fails at least
  one test.
- **L3-Logic / L3.5.** New `StoryDeleteDialog` (`SharedUI/Stories/`, presentational, built on `Modal`):
  irreversibility copy, "Before you delete" with Unpublish instead (published stories only) and
  `StoryDownloadLinks` ("only the chapters that are published"), a labelled title input, then Cancel and
  the filled-danger delete, enabled only on an exact trimmed ordinal match. `StoryEditorPage`: a Delete story
  section last in edit mode; `_savedTitle` captured at load; `HandleDeleteConfirmedAsync` deletes, clears
  the `draft:story:{id}` backup, and navigates with `forceLoad` to `/user/{id}/authored` (identity from
  the `AuthenticationState` cascade, never `IActiveUserContext` — the spec named the latter, which would
  have been a third exception to the two-identity-source rule); a refusal stays in the dialog. Unpublish
  instead closes the dialog and runs the lifecycle panel's own transition **in place**, not the spec's
  `forceLoad` reload, so unsaved form edits survive — the page's established lifecycle behavior. The
  delete joins the page's no-overlap rule (`CoverChangeBlocked`). RazorComponents
  `StoryDeleteDialogTests` (16) and `StoryEditorPageTests` +9; mutation-checked (the live title as the
  confirmation string, the delete outside `CoverChangeBlocked`, no draft clear, a case-insensitive match,
  no clear on reopen, a Cancel while busy each fail a test).
- **L4.** The opener is the danger-tint button recipe in a standard Container; the dialog's delete is the
  filled-danger recipe (`bg-(--color-danger)` + `hover:bg-(--color-danger-strong)`). `check-design-tokens`
  and `check-a11y` (gate A: `story-delete-confirm` label) pass. Visual sign-off waits for Phase 3.
- **L4.5 — 5→1.** No browser was available; tracker **H34** holds the eight steps.
- **L5.** `DELETE /api/stories/{id}` (204/401/403/404) and `ClientStoryWriteService.DeleteStoryAsync`;
  the endpoint class doc now lists delete among the ownership-gated writes. Integration
  `StoryEndpointsTests` +4; Unit `ClientStoryDeleteServiceTests` (4).
- **Feature 5:** no tombstone, so the story's page and chapter URLs 404 afterwards; nothing else changed.
- **Corrected owner premise (reported, not built):** D15 said `daily_story_stats` has no FK and must be
  deleted explicitly. It carries `REFERENCES stories ON DELETE CASCADE`, so the cascade removes the rows;
  the aggregator test D15 asked for passes without a raw `DELETE` (which bullet 2 would forbid anyway).
  `layer1-data-model.md` now marks that clause load-bearing.
- **Left open:** decision row **27** / tracker **F24** (a live block, the moderator path, telling the
  sponsor, the download's scope, and — narrowed by the review fixes below — how an author finds a
  taken-down story's editor); tracker **F13** site 5 (zombie
  reports); tracker **F3** (the ToS line). Orchestrator amendment X3 (moot): the per-report account action
  on a deleted story's report still throws "no account to act on", as WU-ModerationIntegrity kept it, and
  the queue drops such reports anyway; the user-targeted path stays available.

### Feature 4 — WU-AuthorStoryDelete review-fixes Stage note (2026-10-13) — F4 L4.5 stays 1 (H34 extended), and 1→5 at the browser pass the same day; L2/L3-Logic/L3.5/L4/L5 stay 5

Three reviews raised 11 findings, 9 distinct; all were real, and none needed an owner ruling. Verified by
`dotnet build` (0 errors, no new warnings) and `dotnet test` green on all three tiers, plus the four gates.

- **A taken-down story had no Delete button (L2, L3-Logic, L3.5).** D15 rules this case outright: "an
  active `IsTakenDown` does not block it", the control is "on the story edit surface", and the
  taken-down author is its "most load-bearing case". The build had filed it as owner-open (row 27 (e)).
  Now `GetStoryForEditAsync` lifts `"IsTakenDown"` beside `"ContentRating"` (the author reads their own
  taken-down content, `content-safety.md`) and echoes `StoryEditDTO.IsTakenDown`; anyone but the author
  gets `null` for such a story, the missing-id answer (a 403 would confirm the hidden id). The editor
  renders **delete-only**: a warning notice and the Delete story section; no form, no lifecycle panel (the
  status is frozen under a takedown), no chapter panels, no draft backup; the dialog shows "can't be
  unpublished or downloaded first" in place of both outs (the export 404s). The branch also requires
  edit mode, so the create route on a reused instance (tracker D18) never inherits it.
  `ChapterEditorPage` treats the flag as not found, as before (`audit/Chapters.md` F6). What D15 does not
  rule stays open, narrowed: nothing links the author to that editor (row 27 (e), tracker F24 (e)).
  The deletion page's Stories line now says the taken-down case works (`audit/Identity.md` F52).
  **Integration** `StoryDeletionTests.TheEditRead_OfATakenDownStory_*`; **RazorComponents**
  `StoryEditorPageTests` +3 (delete-only state and its delete, the create route on a reused instance, a
  live story's form) and `StoryDeleteDialogTests.ATakenDownStory_OffersNeitherOut_ButStillDeletes`.
- **The title match failed on whitespace (L3.5).** Titles are stored as typed, and the label shows the
  saved title in normal flow, which renders any whitespace run as one space, so a title with a double
  space, a tab or a non-breaking space could never be matched. The match now collapses every run
  (`char.IsWhiteSpace`, NBSP included) to one space on both sides before the ordinal compare.
  **RazorComponents** `StoryDeleteDialogTests` +5 cases (four collapsed matches, a whitespace-only saved
  title) and one more mismatch (whitespace removed, not collapsed).
- **A comment could land mid-delete (L2).** Between the story-scope comment delete and the story delete,
  the transaction waits on the booking lock and returns slots; a comment committed then passed its FK
  check and failed the cascade's RESTRICT with 23001. `TptDelete.StoryCommentsAsync` now locks the
  story's chapters `FOR UPDATE` first, in `chapter_id` order, so the moderation hard delete has the lock
  too. **Integration** `StoryDeletionTests.ACommentPostedWhileTheDeleteRuns_*` (a lock-timeout `DO` block
  interleaved before the story `DELETE`).
- **A lost commit acknowledgement became a 404 (L2).** A replay found the story gone and threw, skipping
  the blob delete and the trace. The delete now runs through `ExecuteInTransactionAsync` with
  `verifySucceeded` (the story is gone); the check runs only for a transient failure, so a lost race still
  throws. **Integration** `StoryDeletionTests.ACommitWhoseAcknowledgementIsLost_*` (a transaction
  interceptor throws a transient error after the first commit). The same two windows remain at the
  chapter, blog-post and profile-wall scopes: tracker **D19**.
- **The post-delete `forceLoad` was unpinned (L3-Logic).** Both delete tests now assert it from
  `BunitNavigationManager.History`.
- **Docs.** `cross-cutting.md` quoted D11/D13's survival test as "once its subject is gone"; the owner's
  words are "once the actor is gone", and D15 applies that test to find a story's dependents do not
  outlive their subject. The advisory-lock registry (`layer2-services.md`) names the second holder
  (`SpotlightPlacementRelease`). `TptDelete`'s doc and tracker F13 lost their "once built" future tense.
- **Mutation-checked**, each failing its test: the chapter lock removed, `verifySucceeded` answering
  false, the edit read's takedown lift removed, the non-author `null` removed, the chapter editor's flag
  check removed, the whitespace collapse reverted to a trim, `forceLoad: false`, the edit-mode condition
  removed from the taken-down branch.
- **L4.5** stays 1: tracker **H34** gains step 9 (the taken-down editor, as its author and as another
  user, and its chapter route) and step 10 (a double-space title). No browser was available.

### Feature 4 — WU-AuthorStoryDelete browser-verification Stage note (2026-10-13) — F4 L4.5 1→5; tracker H34 closed; no runtime bug

All ten steps of H34 ran. The server-only path ran against the standing workbench (not reset), and the
Aspire path against its own database. Every write was checked with `psql`, the upload folder (Local) or
`garage bucket info` (Garage), the cover URL, and the server log's `Story {id} deleted by its author`
trace. Circuit runs were proven by `_blazor/negotiate` and no `/api` call, with the Auto hash removed
before each full load. WASM runs were proven by no negotiate and the action's `/api` request. Actors:
AuthorAlpha and AuthorBeta (authors), TestUser (sponsor, replier, non-author), ReaderGamma (commenter),
ModUser (slot grants), and anonymous. Fixtures were built through the app's own JSON API, in-page `fetch`
as each actor: stories 17–21, published chapters, a comment and a reply, `started` rows, covers, and two
slots booked through `/spotlight`. GIF: `e2e-WU-AuthorStoryDelete.gif` (WASM: an unsaved title edit,
the dialog asking for the saved title, the delete, the Authored tab).
- **Circuit, story 17** (published, two chapters, a comment and a reply, two readers' rows, a cover).
  - "Delete story…" is the page's last element.
  - The dialog reads "Delete this story permanently?" and its copy names chapters, comments,
    recommendations and reading progress.
  - Delete stays disabled for an empty box, the title in lower case, and a title one letter short. It
    enables for the exact title, and for the exact title plus a trailing space (trimmed, as built).
  - With the title field edited and unsaved, the dialog still asks for the saved title, and the typed
    text clears on reopen.
  - The six download links each answered 200 with an attachment, and the TXT held both chapters.
  - **Unpublish instead** closed the dialog. The panel read "Unpublished — the story is a draft again."
    and `psql` held status 0, while the unsaved title stayed in the field (no reload).
  - Reopened on the Draft story, the dialog had no Unpublish button. Its downloads still answered 200
    for the author.
  - The delete showed "Deleting…" with every control disabled (a `MutationObserver` record), then left
    for `/user/4/authored`, which no longer listed the story. `draft:story:17` was gone.
  - `psql` had no rows left for the story, its listing, tags, chapters, contents, the two comments or
    the two readers' rows. The cover file and its URL were gone (404), and `/story/17` answered 404.
- **Circuit, story 16** (taken down; AuthorAlpha's fixture from WU-BlobCleanup's pass, with a cover).
  - As its author: a warning notice and the Delete story section only. The dialog shows "can't be
    unpublished or downloaded first" in place of both outs, with no links.
  - A backdrop click cancels. The delete left for the Authored tab, and the cover file and URL were gone.
  - Report 13 against the story survives, unchanged.
  - As TestUser, `/story/16/edit` reads "Page not found". `/story/16/chapter/new` is not found for both
    users.
- **Circuit, story 19** (AuthorBeta, saved as `E2E Delete:  Double  Space`).
  - The label shows the title with single spaces, and typing it that way enables the delete.
  - The story was then deleted from a second request while the dialog stayed open. The dialog's delete
    showed "That content couldn't be found — it may have been removed." inside the dialog, and the page
    stayed.
  - Its live-block booking (slot 2, the current week) kept its slot `Redeemed`, and the log reported 0
    slots returned.
- **Circuit, TestUser's story 8** (Pending Approval): no Unpublish instead. As TestUser on AuthorAlpha's
  story 18, the editor read "You don't have permission to edit this story."
- **WASM, story 18** (the GIF).
  - The typed-title gate behaves as on the circuit.
  - The cookie was switched to TestUser under the running app. The dialog's delete then sent
    `DELETE /api/stories/18` → 403 and showed "You don't have permission to do that." in the dialog,
    and `psql` showed the story intact.
  - Back as AuthorAlpha, the delete sent the `DELETE` and left for `/user/4/authored`.
    `draft:story:18` was gone, and `psql`, the upload folder and the cover URL showed it all gone.
- **WASM, story 20** (AuthorBeta; TestUser had booked a future week through `/spotlight`).
  - Unpublish instead sent `POST …/status?target=0`, read Draft in place, and kept an unsaved short
    description.
  - The delete left for `/user/5/authored`. `psql` held slot 1 `Available` and no placement, and the log
    reported 1 slot returned.
  - TestUser's `/spotlight` read "You have 1 spotlight slot" again.
- **WASM, story 21** (a Draft created for the race): a delete from a second request, then the dialog's
  `DELETE` → 404 showed the same not-found message in the dialog.
- **Aspire (Garage), story 14** (taken down; AuthorAlpha's fixture from WU-BlobCleanup's pass, with a
  cover object).
  - On WASM, the delete-only editor rendered for its author. As TestUser, the edit read answered empty
    and the page was not found.
  - The delete left for the Authored tab. The cover URL answered 404, and the bucket went from 2 objects
    to 1. Report 8 survives.
- **Endpoint:** anonymous `DELETE /api/stories/18` answered 401, TestUser's 403, and a missing id 404.
  None deleted anything.
- **Logs:** the server-only log had no `fail:`/`crit:` line. The Aspire web log's one `fail:` was the
  documented antiforgery line, from a cookie of the other database's key ring. No browser console errors.
- **Not covered here:** keyboard-only use and focus placement. The dialog sits on the shared `Modal`,
  which deliberately has no initial focus, focus trap or Escape yet: tracker **F6b**
  (WU-A11y-Keyboard).

## Feature 5 — Story Browsing & Display

### WU-RecLifecycle Stage note (2026-07-25) — empty candidate set no longer means "no narrowing"

**Browser-caught during WU-RecLifecycle's L4.5 pass; cells stay Stage 5 (correctness fix under an
already-Stage-5 cell).** `ServerStoryReadService.GetListingsAsync` guarded its candidate narrowing
with `restrictToStoryIds is { Count: > 0 }`, so an **empty** restrict set fell through to an
unnarrowed query: **every bookshelf tab and profile story tab with zero candidates listed the entire
library** (observed live on TestUser's always-empty Hidden Gems tab — 9 stories shown). Pre-existing
since WU23, invisible to the automated tiers because no test ever passed an empty restrict set.

It also silently undid this WU's own **D1** fix: with all of a user's recommendations hidden, the
profile Recommendations tab's candidate set is empty → the whole library rendered where the point of
D1 was to render nothing. Fix: `restrictToStoryIds is not null` — **null = no narrowing** (the
`/discover` path), **empty = narrow to nothing**; `personalScope` follows the same predicate.
Contract now stated on `IStoryReadService.GetListingsAsync`. Covering tier: **Integration**
(`BookshelfStoryIdsTests.GetListingsAsync_EmptyRestrict_ReturnsNothing` — pins items empty *and*
`TotalCount == 0`; the existing null-restrict test pins the other half). Re-verified in-browser:
empty Hidden Gems → "No stories in Hidden Gems."; the profile Recommendations tab of a user whose
only rec is hidden → "No recommendations given yet."

- **WU38c/WU38d additive touches (2026-07-11), cells stay Stage 5:**
  - **Download links (Feature 54's trigger):** new `StoryDownloadLinks` leaf (six per-format
    anchors with the `download` attribute — a file download must bypass the circuit,
    `layer2-services.md` §"File Downloads Bypass the Circuit") mounted in the metadata block of
    `StoryDesktop`/`StoryMobile`; `StoryCard`'s dead `OnDownload` EventCallback replaced by an
    expandable Download submenu of the same links (parameter removed; `StoryCardTests` updated).
  - **"Also posted on" (Feature 53's display + edit surfaces):** `StoryExternalLinksRow` after the
    chapter list / before `RecommendationSection` on both story layouts;
    `StoryPropertiesForm`/`StoryEditorPage` gained the link rows + original-dates section and
    `ExternalPlatforms` plumbing; `StoryDetailsDTO.ExternalLinks` + `GetExternalPlatformsAsync`
    added to the read service. Detail + verification: `audit/Moderation.md` Feature 53.
  - **Bulk chapter import entry point (Feature 63):** `StoryChapterImport` section on
    `StoryEditorPage` (edit mode). Detail: `audit/Import.md`.
- **L1 — Stage 5 (re-verified WU-StoryLifecycle, 2026-09-30 — `Story.PublishedDate` nullable, NULL = never published; see the F4 Stage note).** `StoryListing` warm partition is the projection anchor; sound.
- **L2 — Stage 5 (re-verified WU-StoryLifecycle, 2026-09-30 — nullable publish date through detail/export; author-only NULLS-first accepted; see the F4 Stage note)** (was Stage 2, reclassified from 4 before that). `ServerStoryReadService` (renamed
  from `DbStoryReadService` — see Feature 4's L2 RESOLVED note for the `IDbContextFactory` →
  direct-injection fix, same cell) has `GetStoryByIdAsync` (→ `StoryDetailsDTO`) and
  `GetStoryForEditAsync`, both correct `ReadOnlyApplicationDbContext` `.Select()` projections — they
  *work* and match spec/conventions. **WU12 (2026-06-22) closed the listing gap:** the content-rating
  filter is a global EF named query filter (`ApplicationDbContext.OnModelCreating`, named
  `"ContentRating"`) sourced from a new `IActiveUserContext` (see `content-safety.md` "Content Rating
  Filtering", `identity-and-authorization.md` "Active-User Context"), not a per-method `.Where` — so it applies automatically to every
  `Stories` query, including these listing methods, with no per-call vigilance required. Listing scope
  landed minimal as planned: `StoryListingDto` minted, plus `GetListingsByIdsAsync(int[])` (the §6.6
  building block, reorders results to match input id order, silently drops ids the filter excludes) and
  `GetRecentListingsAsync(page, pageSize)` (one unfiltered-by-criteria browse projection, ordered by
  `LastUpdatedDate DESC`). `GetListingsAsync(StoryFilterDto)` remains explicitly deferred to WU23 — its
  filter shape isn't real until `ResultsFilterPanel` exists, and adding it later is purely additive.
  **WU13 (2026-06-23) additive DTO extension:** `StoryListingDto` gains `ShortDescription (string?)`
  projected from `StoryListing.ShortDescription` (warm partition, already stored, MaxLength 500).
  No migration. Additive — does not contradict the WU12 contract; L2 remains Stage 5.
  **How verified (DTO extension):** via `/dev/wu12/listings/recent` and `/dev/wu12/listings/by-ids` (kept as standing
  dev tools, not removed) against fixture stories (ids 5/6/7, fixture tags 10/11, kept per explicit user
  instruction for later analysis rather than deleted as the original plan's step 4 specified) —
  confirmed the content filter both directions: anonymous (`/dev/wu12/whoami` unauthenticated) saw only
  the Teen-rated fixture story, while `TestUser` (claims `ShowMatureContent=true`, via
  `/dev/wu12/login-as/TestUser`) saw all 4 stories including the Mature-rated ones.
  `GetListingsByIdsAsync` confirmed reordering a shuffled id list back to input order and silently
  dropping an id excluded by the active filter, per spec. **WU12.5 (2026-06-22)** migrated this
  verification into asserted, CI-runnable tests — `ContentRatingFilterTests` and `RecentListingsTests`
  in `TheCanalaveLibrary.Tests.Integration` cover the same both-directions filter check and
  reorder/drop behavior against a real Postgres; the dev-diagnostics endpoints are no longer the
  source of truth for this behavior (see `canalave-conventions/testing.md`).
  **WU13 (2026-06-23) StoryCard slice complete (Stage 5 for this slice):** `SharedUI/Stories/StoryCard.razor`
  built as a pure leaf (no service injection). Contract: `[EditorRequired] StoryListingDto Story`,
  `UserStoryInteractionStateDto? InteractionState` (batch-loaded by parent, forwarded to
  `UserStoryInteractionPanel`), `bool IsOwnStory`, 4 gated `EventCallback`s (OnDiscoverFromStory,
  OnCopyLink, OnReport, OnDownload). Composes `TagChip` (read-only) + `UserStoryInteractionPanel` in
  Listing context. Author byline is a plain hyperlink — NOT `UserCard` (spec §5.30.7). Cover art uses
  stored `CoverArtRelativeUrl` verbatim with `_coverArtFailed` `@onerror` fallback placeholder. Computed
  display: `WordCountDisplay` (3-tier K/M suffix), `StatusLabel`/`StatusBadgeClass` (switch over all 9
  `StoryStatusEnum` values), `RatingLabel`/`RatingBadgeClass` (switch over `Rating` E/T/M). Caret: always-
  present "View Story" link + `HasDelegate`-gated optional items, `InvokeAndClose` helper (mirrors
  `UserCard`). Root is `relative flex flex-col` with internal padding, no outer margin (Outer Margin Rule).
  *This slice only*: L3-Logic / L3.5-Structure / L4-Style cells for Feature 5 stay at 4/4/1 because
  `StoryPage` dispatcher (flicker, catch-all route), `StoryDesktop`/`StoryMobile` layout, and `StoryDeck`
  still hold those cells. The StoryCard contract is now minted, so WU14 (`StoryDeck`) and all listing
  consumers have their key dep satisfied. Step 0 of WU13 also renamed the WU16 component from
  `StoryInteractionPanel` to `UserStoryInteractionPanel` (all references updated repo-wide before any
  StoryCard work, so the card references the final name from the start).
  **How verified (WU13 slice):** `dotnet build` green (8 projects, 0 errors, 2 pre-existing warnings).
  `dotnet test` green: 105 Unit + 107 RazorComponents + 109 Integration = 321 total. RazorComponents tier
  (`StoryCardTests.cs`, 30 tests, JSInterop.Loose, registers `FakeUserStoryInteractionWriteService`):
  title link, author byline link/plain-text null-author, tag count + read-only, ShortDescription
  tooltip/null, WordCountDisplay theory (8 InlineData cases), cover-art fallback/lazy-loading, status/
  rating badge theories, panel composition (blank-slate + IsOwnStory), caret HasDelegate gating.
  L4-Style visual sign-off pending — requires live server check (fixture stories 5/6/7; cover art with/
  without; with/without tags/ShortDescription; one IsOwnStory; caret; gap spacing in a grid parent).
  **WU14 (2026-06-23) StoryDeck deck-slice built (Stage 5 for this slice — cells stay 4/4/1):**
  `SharedUI/Stories/StoryDeck.razor` built as a pass-through layout composite. Three-state
  internally: `Stories is null` → loading (inline text, skeleton-card upgrade is additive); `Count == 0`
  → customisable `EmptyMessage`; populated → `grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6`
  of `StoryCard` leaves + unconditional `PaginationControls` (self-hides at `TotalPages ≤ 1`).
  Contract: `[EditorRequired] IReadOnlyList<StoryListingDto>? Stories`, `IReadOnlyDictionary<int,
  UserStoryInteractionStateDto>? InteractionStates` (batch-loaded by parent, keyed by `StoryId`),
  `int? CurrentUserId` (deck computes `IsOwnStory` per card), `string EmptyMessage`, pagination
  forwards (`CurrentPage`, `PageSize`, `TotalCount`, `EventCallback<int> OnPageChanged`). No service
  injection. Caret callbacks deferred — additive when first consumer (WU28/34/38) needs them.
  Pre-implementation doc-touch (moment 1): fixed two stale areas in `layer3.5-structure.md` —
  deleted a duplicate StoryDeck paragraph; corrected the "Loading States" code example (old example
  showed the *page* doing the null/empty branch and passing only a populated array into `<StoryDeck>`,
  contradicting the plan; new example shows the page passing nullable `Stories` to the deck, which
  branches internally). **Test tier:** RazorComponents — `StoryDeckTests.cs` (14 tests): null →
  loading/no-card; empty → default+custom EmptyMessage/no-card; populated → N cards + grid; IsOwnStory
  match/non-match/null CurrentUserId; InteractionStates IsFavorite forwarding (active span appears);
  null states → no active span; PaginationControls shown/hidden/self-hidden; OnPageChanged bubbles.
  Mutation sanity confirmed: inverting the populated branch (`Count >= 0`) → 6 tests fail; reverted,
  suite green. L4 visual sign-off pending (Stage-6 gate): bUnit cannot verify Tailwind responsive
  breakpoints or the loading visual; requires live-server render check (1→2→3-col grid, empty message,
  loading state, pager shown/hidden). **How verified:** `dotnet build` green (8 projects, 0
  warnings/errors). `dotnet test` green: 112 Unit + 136 RazorComponents + 133 Integration = 381 total.
- **L3-Logic — Stage 5 (WU25, 2026-06-24; the page's interaction state now also syncs with its
  recommendation section — WU-InertFeatures and its review fixes, 2026-09-30, Stage note at the end of
  this feature; L4.5 unaffected, see there).** `StoryPage` rebuilt: route `/story/{StoryId:int}/{*StorySlug}`
  (catch-all cosmetic slug); `[PersistentState]` on `Story` and `Chapters` (kills prerender→interactive
  double-fetch flicker); anonymous-safe `[CascadingParameter] Task<AuthenticationState>? AuthState`
  to resolve `_currentUserId`; loads `GetStoryByIdAsync` + `GetChapterListAsync`; computes
  `_isAuthor = _currentUserId.HasValue && Story.AuthorId == _currentUserId`.
  `StoryDetailsDTO` extended (additive): `int? AuthorId`, `string? CoverArtRelativeUrl`, `Rating Rating`,
  `StoryStatusEnum Status`, `IReadOnlyList<TagChipDto> Tags`. `GetStoryByIdAsync` projection updated to
  two-step intermediate row (mirrors listing service; sprites resolved in memory via existing `ToTagChip`).
  `GetChapterListAsync(int storyId)` added to `IChapterReadService` + `ServerChapterReadService`:
  two-step query (chapters + non-primary versions via `SelectMany`, grouped in memory per the
  `GetChapterVersionsAsync` pattern); returns `IReadOnlyList<ChapterListEntryDto>`.
  `ChapterListEntryDto` minted in `Core/Chapters/` — `(int ChapterNumber, string Title, int WordCount,
  bool IsPublished, IReadOnlyList<ChapterVersionDto> AlternateVersions)` (non-primary accessible versions
  only; reuses existing `ChapterVersionDto`).
  **How verified (2026-06-24):** `dotnet build` green (0 errors, 0 new warnings). Integration tier:
  `StoryDetailTests` (15 tests, Testcontainers Postgres) — `GetStoryByIdAsync` new fields (`AuthorId`,
  `CoverArtRelativeUrl`, `Rating`, `Status`, empty tags, sprite-resolved tags, mature-content filter both
  directions, null-story); `GetChapterListAsync` (empty list, ordering, single-version empty alternates,
  non-primary alternate present/absent by rating ceiling, mature viewer vs. non-mature, unpublished chapter
  `IsPublished=false`). All 15 pass.
- **L3.5-Structure — Stage 5 (WU25, 2026-06-24).** `StoryDesktop`/`StoryMobile` rebuilt: params
  `StoryDetailsDTO Story`, `IReadOnlyList<ChapterListEntryDto> Chapters`, `int? CurrentUserId`,
  `bool IsAuthor`. §5.28 layout: title → metadata row (author link, rating, status, word count,
  publish/updated dates, tag chips, author-only "Edit Story" link) → cover art (with `@onerror`
  fallback) → long description (`RichTextView`) → chapter selection (`ChapterList` leaf) →
  recommendations (`RecommendationSection`).
  **`ChapterNavigation` is NOT used on the story landing page.** It is reading-context-only
  (`CurrentChapterNumber` is `[EditorRequired]`; renders prev/next + "Chapter N" dropdown). The story
  landing page uses the new `ChapterList` leaf (`SharedUI/Chapters/`): one row per chapter linking to
  the primary version, with non-primary alternates as indented sub-rows labeled `Title - VersionName`.
  `ShowDrafts=true` for authors (shows unpublished chapter rows with a "Draft" marker).
  **How verified (2026-06-24):** RazorComponents tier — `ChapterListTests` (14 tests): single-version
  row + primary URL, multi-version sub-rows with `Title — VersionName` label + version URLs, fallback to
  `"Version {N}"` when `VersionName=null`, `ShowDrafts=false` hides unpublished / `=true` shows with
  "Draft" marker, empty list message, word-count display theory (3 cases). `StoryDesktopTests` (22 tests):
  title in `<h1>`, author link/plain-text, Edit Story link gated by `IsAuthor`, status/rating badge
  theories (4+3), word-count theory (3), tag chips read-only, cover img vs. null, long description content,
  chapter list composition, chapters section absent/present by `IsAuthor`, `RecommendationSection`
  composition (GetForStory called with StoryId; anonymous no CTA). `StoryMobileTests` (21 tests): same
  coverage as Desktop. All 57 RazorComponents tests pass.
- **L4-Style — Stage 1.** All Tailwind tokens; visual sign-off pending human review (Stage-6 gate,
  consistent with WU13/WU14/WU24 precedent — bUnit cannot verify layout/responsive breakpoints).
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto; story browse verified in a real WASM runtime during the
  flip's browser wave (random batch + filtered `POST /query`; story-page persisted-state hydration
  confirmed by network log). Full wave narrative + the 7 bugs found/fixed: `workplan.md` WU-GlobalFlip.
- **L6 — Stage 5 (WU-L6, 2026-07-07).** The two discovery sort spines built in `L6_IndexBatch`:
  `ix_stories_published_date` (+ `ix_stories_last_updated_date` for `GetRecentListingsAsync` /
  Relevance tie-break). Measured at 3k seeded stories: DatePublished page-1 p50 0.39→0.09 ms
  (−76%); the §8.7 exclusion-probe page −68% (riding these + the restored USI `ignored` partial).
  Story-centric USI mirror indexes were REJECTED under R4 — no story-centric interaction query
  exists (favorite counts are denormalized on `UserStat`). Detail: `layer6-indexes.md`.

### WU-InertFeatures and its review fixes — Stage note (2026-09-30): the story page keeps one copy of the viewer's interaction state

**No cell flips.** F5's L4.5 stays 5: the reading-list button on the recommendation cards is
Feature 30's surface and the panel's adoption is Feature 16's. Both have L4.5 = 1, and tracker **H14**
step 1 drives them on this page, so they carry the browser debt. The page's own browsing and display
behavior is unchanged. The WU left this file untouched; the review fixes added this note.

**What changed.** `StoryPage` passes the viewer's Read It Later bit to `RecommendationSection`
(`StoryIsReadItLater`) and re-reads its state when a card saves (`OnReadItLaterSaved`), so the
interaction panel adopts the save instead of flushing it back (the WU). Since the review fixes it also
takes the panel's `OnStateSaved`, so its copy, and with it the cards' "Saved for later", follows a Read
It Later cleared in the panel. The section no longer latches its own copy. A misplaced comment was
also fixed: `ReloadInteractionStateAsync` had been inserted between `LoadSupplementaryAsync`'s comment
and that method.

**How verified:** RazorComponents `StoryPageTests` (+2 across the WU and its review fixes: a card save
shows in the panel and the cards; a panel clear re-enables the cards). The second test fails without
the page's `OnStateSaved` wiring. **Browser-driven 2026-09-30** by the WU-InertFeatures browser pass
(tracker H14 closed), on both render phases:
- a section card save showed in the panel and stayed after the debounce;
- a panel clear re-enabled the cards;
- a refused save (the rec moved to NeedsRevision behind the open page) showed inline;
- an anonymous click went to the login page.
Detail: `audit/Recommendations.md` F30's browser note.

## Feature 8 — Story Arcs

**Design settled 2026-07-12 (chat deliberation with Brian; resolves the §8.2 Stage-1 gap). Full
requirements record: WU45 in `workplan.md`. Settled — do not revisit without flagging:**
- **Model:** range-based (`StartChapterNumber`/`EndChapterNumber`) as built; the early "join table
  linking chapters" alternative from the source conversations was considered and rejected.
  **`SortOrder` is eliminated** (redundant: non-overlapping ranges order by `StartChapterNumber`;
  ordinal "Arc X" labels are computed, not stored). Unique `(StoryId,Title)` stays; the
  `(StoryId,SortOrder)` unique index is replaced by unique `(StoryId,StartChapterNumber)`.
- **Coverage:** no overlap (service-layer validation, not DB); gaps allowed (prologue/interlude/
  epilogue chapters may belong to no arc); `Start ≤ End`.
- **Renumbering:** chapter reorder/delete (added by WU45 to Feature 6) shifts arc bounds
  independently: `Start`/`End` each move when the change point falls at/before them; empty arcs
  (`Start > End`) auto-delete. Applied uniformly to all arcs → invariants preserved. Silent (no
  warning flow — explicitly waived 2026-07-12, including for published-chapter URL changes).
- **Reader UI:** arc headers compose into the `ChapterList` flat segmenter output (see
  `audit/Chapters.md` WU45 note) — sticky (always shown even fully-read), independently
  toggleable, frontier arc expanded by default, others collapsed; no windowing *inside* an
  expanded arc (arcs supersede the auto-collapse for chapters they cover). Reading page shows
  `Arc X — [name]` under the chapter title (gap chapters: nothing).
- **Author UI:** separate arc-manager panel (not merged into chapter reorder UI): editable rows
  (Title/Start/End) + live read-only chapter-list preview highlighting each arc's span. Range
  fields are the source of truth; no drag-reorder of arcs (order follows ranges).

**Cell status (WU45 build landed 2026-07-12):**
- **L1 — Stage 5 (`ck_story_arcs_bounds` since WU-SchemaHardening, 2026-10-13 — see its Stage note
  under Feature 4).** Migration `WU45_StoryArcDropSortOrder`: `sort_order` column + its unique index
  dropped; unique `(story_id, start_chapter_number)` added (ordering spine + non-overlap backstop).
  `StoryArc.cs` moved `Core/Models/` → `Core/Stories/` (legacy-folder rule). Verified: migration
  applies via the Integration suite's Testcontainers migrate; `dotnet build` green.
- **L2 — Stage 5 (re-verified WU-ThrottleCoverage, 2026-10-13 — `CreateArcAsync` spends a `ContentCreate` token, owner ruling D20; Integration `WriteThrottleTests`; see its Stage note under Feature 10).** `IStoryArcReadService`/`IStoryArcWriteService` (`Core/Stories/`),
  `ServerStoryArcReadService`/`ServerStoryArcWriteService` (`Server/Stories/`, Series-template
  shape: factory-per-method reads, author-gated writes, `StoryArcValidationException`).
  Verified: Integration tier — `StoryArcServiceTests` (23 tests: CRUD, author/anon gates, range
  rules incl. 5 overlap theories + adjacent/gapped legality, update-excludes-self, ordering by
  StartChapterNumber, delete). Arc-bound shifts under chapter reorder/delete are covered in
  `ChapterReorderDeleteTests` (see `audit/Chapters.md` WU45 note).
- **L3-Logic / L3.5 — Stage 5.** Reader side: arc headers/rows compose into the shared
  `ChapterListSegmenter` flat walk rendered by `ChapterList` (Unit: `ChapterListSegmenterTests`
  17; RazorComponents: `ChapterListTests` WU45 region — sticky header, frontier-arc expansion,
  toggle collapse). Reading page: `Arc X — [name]` under the chapter title
  (`ChapterReadingPage`). Author side: `StoryArcManagerPanel` (rows + live preview) on
  `StoryEditorPage` edit mode.
- **L4 — Stage 1.** All design tokens (`check-design-tokens.ps1` clean for these files); visual
  sign-off pending human review (WU13/WU14/WU24 precedent).
- **L4.5 — Stage 5 (1→5, WU-ThrottleCoverage browser pass, 2026-10-13: the sixth arc create in a burst reads "please wait 120 seconds" in the arc panel on the circuit and on WASM, and `psql` holds only the saved arcs — tracker H33 closed; see its Stage note under Feature 10. Lowered 5→1 by that WU's review fixes the same day; Stage 5 from WU-ChapterArcBrowserPass, 2026-07-24).** Real-circuit pass closes the WU45
  deferral — see Stage note below.
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto; arcs data loading verified in a real WASM runtime during the
  flip's browser wave (arcs fetched via API on story-page load). Full wave narrative + the 7 bugs
  found/fixed: `workplan.md` WU-GlobalFlip.

### WU-ChapterArcBrowserPass Stage note (2026-07-24) — closes the WU45 L4.5 deferral, no bugs found

Real-circuit Chrome pass on the flagship seed story (story 1) drove the full create-arc →
story-page-headers → reading-label loop the WU45 deferral named. **L4.5: `2 → 5`.** Full checklist
and mutation-by-mutation detail (chapter reorder/delete, mark-read repaint, New badge, download
headers — same session, same story) lives in `audit/Chapters.md`'s WU-ChapterArcBrowserPass Stage
note; this note covers the Story-Arcs-specific findings.

- **Author flow (`StoryArcManagerPanel` on `StoryEditorPage`):** added "Arc 1: Beginnings"
  (ch 1–2) and "Arc 2: The Long Middle" (ch 3–5). The read-only preview list updated live as the
  draft row's title/range fields changed — before Add/Save — confirming "arc-manager live preview
  interactivity." Deliberately triggered an overlapping range (Ch. 2–5 vs. the just-added
  Ch. 1–2): the panel surfaced an inline validation message naming both arcs and the rule
  ("Arcs cannot overlap"), not a raw exception string; corrected and saved cleanly. `psql
  story_arcs` confirmed both rows.
- **Reader surfaces:** the story page's `ChapterList` rendered "Arc 1 — Arc 1: Beginnings" /
  "Arc 2 — Arc 2: The Long Middle" sticky headers with per-arc "N/M read" counts; clicking a
  header's caret collapsed/expanded that arc independently of the other. Opening a mid-arc
  chapter (Chapter 3) showed "Arc 2 — Arc 2: The Long Middle" under the chapter title on
  `ChapterReadingPage`, exactly the settled `Arc X — [name]` format.
- **Fixture decision — the two arcs are left in place, not wiped.** The flagship story's stated
  role is "seed story exercising multi-chapter reading and chapter versioning" (`DataSeeder.cs`);
  before this pass it had zero arcs, so nothing exercised the Story Arcs UI end-to-end for future
  manual verification or screenshots. Leaving the two demonstration arcs makes the fixture
  actually exercise the feature it's meant to, per `run-server/SKILL.md` "Dev DB lifecycle" (new,
  non-conflicting state is kept by default — only wipe when prior state would confound what's
  being observed, which arcs on chapters 1–5 do not).
- **L4-Style unaffected — stays Stage 1.** Human visual sign-off (WU13/WU14/WU24 precedent) is
  out of scope for an agent-driven pass; tokens were already clean per `check-design-tokens.ps1`.

No runtime bugs found; no code changes made.

## Feature 9 — Series & Ordering

- **L1 — Stage 5.** `Series` (`Core/Series/Series.cs` as of WU41) + `SeriesEntry` (composite key
  `(SeriesId,StoryId)`, `OrderIndex`), unique `(AuthorId,Name)`.

### WU41 settled-vs-open note (2026-07-11, before build — Doc-Touch moment 1)

The historical Gemini planning discussions only ever modeled "series" as a `StoryRelationship`
*type* (`RelationshipType = 'Series'` with an `OrderIndex`); the first-class `Series`/`SeriesEntry`
tables were a later architectural decision, so those notes don't answer WU41's UX questions. The
following were settled with the user 2026-07-11 and must not be revisited without going back through
the user:

1. **Membership scope:** a series contains **only the owner's own stories**. The write service gates
   every add on `story.AuthorId == series.AuthorId == ActiveUser.UserId`.
2. **Management:** a **dedicated series page** (`/series/{id}/edit`) — no field added to the story
   editor/`StoryPropertiesForm`. Mirrors `GroupCreateEditPage`.
3. **Browse surfaces:** public per-series page (`/series/{id}`) + a **Series tab on the profile**
   (`ProfileTab.Series`) + a **"My Series"** owner list (`/series`). No global `/series` directory
   (post-MVP, not scoped here).
4. **Story-page display:** a "Part of series X — Part N of M" box **with prev/next in-series
   navigation**, rendered on `StoryDesktop`/`StoryMobile`.
5. **A story may belong to multiple series.** The existing `SeriesEntry` PK `(SeriesId, StoryId)` has
   no unique constraint on `StoryId` alone — the schema already permits this, so no L1 migration was
   needed. `StoryPage` therefore renders a *list* of `SeriesMembershipBox` (one per series), each with
   its own position + prev/next. AO3-style semantics.
6. **Viewer-visible counting:** `StorySeriesMembershipDto`'s Position/Count/PrevStoryId/NextStoryId are
   computed only over series members that survive the viewer's `ContentRating`/`IsTakenDown` read
   filters (an explicit join through `Story` in `ServerSeriesReadService.GetMembershipsForStoryAsync`,
   not a bare `SeriesEntry.StoryId` projection) — so "Part 2 of 3" always matches what the viewer can
   actually reach and prev/next never link to a story hidden from them. `SeriesListingDto.StoryCount`
   and `SeriesDetailDto.OrderedStoryIds`, by contrast, are the raw (unfiltered) entry set — mirroring
   `GroupDetailDto.StoryIds`/`Group.MemberCount`'s existing precedent of "count is a cheap raw number,
   the hydrated list (`IStoryReadService.GetListingsByIdsAsync`) is the authority and silently drops
   what the viewer can't see." This mismatch (a card might say "3 stories" but the deck shows 2) is the
   same accepted tradeoff Groups already ships with.

**Done (2026-07-11):** built per the above. `Core/Series/` (entities + DTOs + `ISeriesReadService`/
`ISeriesWriteService`), `Server/Series/` (`ServerSeriesReadService`/`ServerSeriesWriteService`,
CQRS-lite inheritance mirroring `ServerGroupReadService`/`ServerGroupWriteService`; owner-gate via
`RequireOwnerAsync`/`RequireAuthenticatedUser`; `Description` sanitized once on save;
pre-insert duplicate-name check surfaces as `SeriesValidationException`, matching
`ServerTagWriteService`'s pattern, rather than surfacing the raw unique-index `DbUpdateException`).
`SharedUI/Series/` — `SeriesCard` (leaf), `SeriesMembershipBox` (leaf), `SeriesPage` (public detail +
`StoryDeck`), `SeriesCreateEditPage` (owner-gated create/edit/add/remove/reorder/delete),
`MySeriesPage` (`/series`, owner listing). Integrated into `StoryPage`/`StoryDesktop`/`StoryMobile`
(membership boxes), `ProfilePage`/`ProfileDesktop`/`ProfileMobile` (Series tab), `CreateMenu` ("New
Series"), `UserMenu` ("My Series"). `ExceptionPresenter` extended with `SeriesValidationException`.
`ProfileTab` extended with `Series = 5` / slug `"series"` (additive — not persisted, so no migration
concern). One test-fixture gap found and fixed during full-suite verification (not anticipated in the
plan): `ProfilePageTests`/`FakeProfileTestServices` didn't register the newly-required
`ISeriesReadService`, breaking `TabSwitch_OnSameInstance_ReloadsTabPayload` — added
`FakeSeriesReadService` (all-empty defaults, same shape as the other Fake*ReadServices there).

**Two real runtime bugs found and fixed via the L4.5 browser-verification pass (not anticipated in
the plan; both are the same dispatcher-reload class as WU-ComponentSoundness's F1 StoryPage fix):**
1. `SeriesCreateEditPage` maps two `@page` routes ("/series/new" and "/series/{SeriesId:int}/edit")
   onto the same component type. `HandleSubmitAsync`'s post-create `Nav.NavigateTo($"/series/{newId}/edit")`
   reuses the same component instance — `OnInitializedAsync` (which loaded the create-mode blank
   state) never re-fires; the page only ever implemented that one lifecycle method. Symptom observed
   live: after creating a series, the "edit" page rendered as if still empty/create-mode (blank name,
   no member list, no add-story picker) even though the series had actually been created correctly in
   the DB. Fixed by adding `OnParametersSetAsync` with an initialized-guard + a route-changed guard
   (`SeriesId` vs. create-mode), mirroring `StoryPage`/`ProfilePage`/`GroupPage`'s dispatcher pattern.
   Regression test added: `SeriesCreateEditPageTests.PostCreateRedirect_OnSameInstance_ReloadsEditModeData`
   (bUnit `cut.Render(...)` re-set, same technique as `ProfilePageTests.TabSwitch_OnSameInstance_ReloadsTabPayload`).
2. `StoryPage.OnParametersSetAsync` (the existing in-place-story-navigation reload path, added
   WU-ComponentSoundness) reloads `Story`/`Chapters`/`_usiState` on a `StoryId` change but the new
   `_seriesMemberships` load was only wired into `OnInitializedAsync`. Symptom observed live: clicking
   the series membership box's "Next" link (an in-place `/story/{id}` navigation, same StoryPage
   instance) left the *previous* story's series box on screen — "Part 1 of 2" with Next pointing back
   at the story the viewer was now on. Fixed by adding the `SeriesReadService.GetMembershipsForStoryAsync`
   reload to `OnParametersSetAsync` alongside the other per-story state. No dedicated bUnit regression
   test — `StoryPage` has no bUnit suite of its own even for its original F1 fix (WU-ComponentSoundness);
   covered by this L4.5 pass instead, consistent with that precedent.

- **L2/L3-Logic/L3.5-Structure — Stage 5 (re-verified WU-ThrottleCoverage, 2026-10-13 — L2: `CreateSeriesAsync` spends a `ContentCreate` token, owner ruling D20, Integration `WriteThrottleTests`; L3-Logic: the create/edit page shows a throttle or session refusal through `ExceptionPresenter` instead of a fixed "unexpected error", RazorComponents `SeriesCreateEditPageTests`; and since that WU's browser pass the create/edit dispatcher keys on one route value, so create → edit → create no longer keeps the edited series' name, RazorComponents `SeriesCreateEditPageTests` +2; see its Stage note under Feature 10).** See test tiers below.
- **L4-Style — Stage 1** (visual sign-off pending, per WU8/WU13/WU23/WU28/WU37/WU42/WU43 precedent —
  functionally verified live, below, but no dedicated visual/token polish pass).
- **L4.5-Browser — Stage 5 (1→5, WU-ThrottleCoverage browser pass, 2026-10-13: the sixth series create in a burst reads "please wait 120 seconds" on `/series/new` on both render phases, and Write → New Series from an edit page opens a blank form after the pass's fix — tracker H33 closed; see its Stage note under Feature 10. Lowered 5→1 by that WU's review fixes the same day; Stage 5 from 2026-07-11).** Real-circuit verification via `mcp__claude-in-chrome__*`
  against the dev server (`AuthorAlpha`/`TestUser` fixtures): My Series (empty state, owner-scoped) →
  Create Series → post-create redirect into edit mode (bug 1 above, found + fixed here) → add two
  stories via the picker → reorder via ↑ (persisted) → public SeriesPage (`StoryDeck` in reordered
  order, owner Edit link) → StoryPage membership box ("Part 1 of 2", disabled Previous, active Next)
  → click Next → in-place navigation to the second story (bug 2 above, found + fixed here) — box now
  correctly reads "Part 2 of 2" with active Previous, no Next → profile Series tab as owner (Edit
  links present) and as a different viewer (`TestUser`, Edit links correctly absent) → `UserMenu`
  "My Series" and `CreateMenu` "New Series" entries present → duplicate-name create attempt surfaces
  "You already have a series named …" inline → delete via `ConfirmDialog` (destructive variant) →
  redirected to My Series, series gone from the list. Ground truth confirmed via `psql`: the deleted
  series' `SeriesEntry` rows cascaded away; its two member stories (`story_id` 1, 3, 7 range) survived
  untouched; the surviving series' `series_entries.order_index` matched the reorder performed in the UI.
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto; series memberships fetched via API on story-page load
  verified in a real WASM runtime during the flip's browser wave. Full wave narrative + the 7 bugs
  found/fixed: `workplan.md` WU-GlobalFlip.
- **Verified (2026-07-11):** `dotnet build` 0 errors/warnings (8 projects). `dotnet test` full
  solution green: 541 Unit + 533 RazorComponents + 462 Integration = **1536 tests**. Mutation-sanity:
  temporarily replaced the `Story`-joined query in `GetMembershipsForStoryAsync` with a bare
  `SeriesEntry.StoryId` projection (bypassing `ContentRating`/`IsTakenDown`) —
  `GetMemberships_MatureMemberHiddenFromMatureDisabledViewer_ExcludedFromPositionCountAndNext` failed
  as expected; reverted, suite green again. `check-design-tokens.ps1`: the only 2 findings are
  pre-existing in `TreeSearchResultBadge.razor` (WU44), unrelated to this unit.
- **Tests:** Unit (`SeriesValidationsTests` — 11 tests, `CreateSeriesDto`/`UpdateSeriesDto.CanSave`);
  Integration (`SeriesServiceTests` — 26 tests, Testcontainers Postgres — CRUD owner-gating,
  cross-author add rejection, append/reorder/remove `OrderIndex`, duplicate-name rejection (per-author,
  not global), cascade delete, multi-series membership, and the content-filter-drop case for
  Position/Count/Next); RazorComponents (`SeriesCardTests` + `SeriesMembershipBoxTests` — 15 tests,
  leaf components; `SeriesCreateEditPageTests` — 1 test, the same-instance-reload regression above).
  **Scope note:** no *general* `SeriesCreateEditPageTests` CRUD-UI coverage was added beyond that one
  regression test — matching the existing precedent that `GroupCreateEditPage` (the closest analog)
  has no page-level bUnit suite either; the owner-gate and CRUD logic are exercised at the Integration
  tier (the real authority — the page's pre-check is only a UX nicety).

## Feature 10 — Story Lineage (WU42, formerly "Story Relationships")

**Renamed 2026-07-12 (settled — do not revisit).** The spec/original entity name
`StoryRelationship` was ambiguous on this site two ways over: (a) `StoryCharacterPairing` (romantic/
platonic character pairings, itself renamed off `StoryCharacterRelationship` in WU37 specifically to
stop colliding with this feature — see `audit/Tags.md`), and (b) `UserStoryInteraction` (a user's
relationship *to* a story). Renamed feature-wide to **`StoryLineage`** — evokes how stories descend
from / accompany one another without personifying them, and reads correctly across all four seeded
type values (Inspired By / Prequel / Sequel / Companion Piece). `SourceStoryId`/`TargetStoryId` were
kept (spec §939 wording; ancestor/descendant-style names were rejected as wrong for Companion Piece).
Migration `AddStoryLineageRename` (2026-07-12) renamed the tables/constraints/indexes in place — no
data loss (verified: 4 seed rows survived, live dev DB rebooted clean, `dotnet build` 0 errors).

- **L1 — Stage 5 (`ck_story_lineages_no_self` since WU-SchemaHardening, 2026-10-13 — see its Stage
  note under Feature 4).** `StoryLineage` (`Core/Stories/StoryLineage.cs`) composite key
  `(SourceStoryId, TargetStoryId, RelationshipTypeId)`; `StoryLineageType` lookup table seeded
  (Inspired By/Prequel/Sequel/Companion Piece — unchanged data, only the container renamed);
  `StoryLineageStatus` enum (`Pending=0/Approved=1/Rejected=2`). One-way directional per §5 — absence
  of a reverse row means "don't show on the target." Cascade delete from both source and target story.
  Notification plumbing pre-wired: `NotificationTypeEnum.StoryLineageRequested`/`StoryLineageApproved`
  (renamed from `StoryRelationshipRequested`/`...Approved`), seed rows, `NotificationPresenter`
  message arms, and `ServerNotificationReadService.KindFor` → `Story` all already in place before this
  work-unit — only the two semantic `INotificationWriteService` methods are new (WU42 L2).

**Settled decisions (WU42, do not revisit):**
1. **Public display** on `StoryPage` (source story only, Approved links only) — a new
   `StoryLineageBox` leaf mirroring `SeriesMembershipBox`'s placement.
2. **Management is hybrid:** dedicated user-wide owner page `/story-lineages`
   (`MyStoryLineagesPage`, mirrors `MySeriesPage`) is the single create + approval-inbox surface;
   the story edit page (`StoryEditorPage`) carries only a "Manage story lineage →" link to it.
   Rejected a per-story-edit-page home because incoming approval requests need one aggregated inbox,
   not one scattered per story.
3. **Target picker:** a reusable `SharedUI/Stories/StoryTitlePicker` (Blazored.Typeahead, modeled on
   `TagSelector`) backed by a new `IStoryReadService.SearchStoriesByTitleAsync` (`ILike` substring,
   capped). **Deliberately not** the discovery FTS path — `StoryListing.SearchVector` is a
   whole-word/ranked `to_tsvector` GIN index tuned for browse relevance; a picker needs incremental
   substring/prefix matching, a different access pattern that can't share that index.
4. **Retrofit scope:** the reusable picker also replaces Groups' raw `<input type="number">`
   "Story ID" add-story entry (`GroupDesktop.razor`/`GroupMobile.razor`) — the worst existing
   story-entry UX and an exact fit (cross-author/unbounded). Series' and blog-posts' own-stories
   `<select>` dropdowns are left as-is (bounded, a dropdown already suffices).
5. **Self-links auto-approve:** if the requesting author owns both source and target, the link is
   created already `Approved` with no self-notification (matches the notification drop-self
   invariant). Only cross-author links start `Pending`.

- **L2/L3-Logic/L3.5-Structure — Stage 5 (2026-07-12; L2's by-story read gained the source-story guard, WU-AccessGateSweep2, 2026-09-30 — see its Stage note; approve is Pending-only, WU-CounterSymmetry, 2026-09-30, and L3-Logic's page reloads its list after a stale approve since that WU's browser pass — see its Stage note; a request that notifies spends a `RepeatRequest` token and the reject comment no longer claims a spam guard, WU-ThrottleCoverage, 2026-10-13 — see its Stage note).** **L4-Style — Stage 1** (pending human
  visual sign-off, WU8/WU13/WU23/WU28/WU44 precedent — an agent's own live-browser check does not
  close this gate). **L4.5-Browser — Stage 5 (1→5, WU-ThrottleCoverage browser pass, 2026-10-13: the sixth notifying request in a burst reads "please wait 180 seconds" on `/story-lineages` on both render phases and writes no row, and self-owned links still save with the bucket empty — tracker H33 closed; see its Stage note. Lowered 5→1 by that WU's review fixes the same day; Stage 5 from 2026-07-12, real-circuit verification, see below).**
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto; lineage page + `StoryTitlePicker` verified in a real WASM
  runtime during the flip's browser wave (picker rebuilt on the new in-house `CanalaveTypeahead`).
  Full wave narrative + the 7 bugs found/fixed: `workplan.md` WU-GlobalFlip.

**Done (2026-07-12):** built per the settled decisions above. `Core/Stories/`:
`IStoryLineageReadService`/`IStoryLineageWriteService`, `StoryLineageDto`/`CreateStoryLineageDto`/
`StoryLineageManageDto`(+`Outgoing`/`IncomingRequest` rows)/`StoryLineageTypeDto`/
`StoryTitleSearchDto`, `StoryLineageValidationException`. `Server/Stories/`:
`ServerStoryLineageReadService`/`ServerStoryLineageWriteService` (primary-ctor chaining, mirrors
`ServerSeriesReadService`/`WriteService`), plus `IStoryReadService.SearchStoriesByTitleAsync`
(`ILike` substring, capped at 10, `ServerStoryReadService`). Two new `INotificationWriteService`
methods (`NotifyStoryLineageRequestedAsync`/`ApprovedAsync`) — the only notification-side gap, since
the enum members/seed rows/presenter text/`KindFor` mapping were all already in place before this
work-unit. `SharedUI/Stories/`: `StoryTitlePicker` (reusable Blazored.Typeahead wrapper, modeled on
`TagSelector`), `StoryLineageBox` (public display leaf, mirrors `SeriesMembershipBox`),
`MyStoryLineagesPage` (`/story-lineages`, mirrors `MySeriesPage` — incoming-requests inbox +
outgoing-links list + create form). Wired into `StoryPage`/`StoryDesktop`/`StoryMobile` (new
`LineageLinks` parameter, loaded alongside `_seriesMemberships`), `StoryEditorPage` ("Manage story
lineage →" link), `UserMenu`/`CreateMenu` ("My Stories' Lineage" / "New Story Lineage"),
`ExceptionPresenter` (new arm). **Groups retrofit:** `GroupDesktop.razor`/`GroupMobile.razor`'s
add-story now uses `StoryTitlePicker` instead of a raw `<input type="number">` Story-ID field
(`_addStoryId`/`_addStoryError` fields removed — the latter was dead code, never actually assigned).

**Real bug found and fixed during verification, not anticipated in the plan:** `PostgresFixture`'s
Respawn `TablesToIgnore` list (which protects seeded lookup-table rows from being wiped between
every integration test) still referenced the pre-rename table name `story_relationship_types`. Since
this is a literal SQL string, not a C# identifier, it was invisible to the Phase-0 `StoryRelationship`
rename grep and broke every integration test that requested a lineage type — Respawn was wiping the
four seeded type rows after the first test's `InitializeAsync`, so all subsequent
`RequestLineageAsync` calls failed type-existence validation. Fixed by updating the ignore-list entry
to `story_lineage_types`. Lesson recorded here (not a skill-file change — this is a one-off
config-string miss, not a recurring convention gap): a table-rename's blast radius includes any
literal SQL/string table-name reference, not just C# type usages.

**Verified (2026-07-12):** `dotnet build` 0 errors/warnings (8 projects). `dotnet test` full solution
green: 615 Unit + 570 RazorComponents + 544 Integration = **1729 tests**, including 28 new Integration
tests (`StoryLineageServiceTests` — cross-author request/notify, self-owned auto-approve/no-notify,
ownership gating on both request and approve/reject/delete, re-request-after-rejection row reuse,
cascade delete from either story, viewer-visible display filtering, `SearchStoriesByTitleAsync`
substring/cap/content-rating) + 6 new Unit (`StoryLineageValidationsTests`) + 8 new RazorComponents
(`StoryLineageBoxTests`/`StoryTitlePickerTests`). Mutation-sanity: temporarily bypassed the
`ContentRating` filter on the target-story join in `GetLineageForStoryAsync` → the mature-drop test
failed as expected; reverted, suite green again.
**L4.5-Browser Stage 5 (2026-07-12)** — full cross-author flow driven live against the dev DB
(`AuthorAlpha`/`AuthorBeta` fixtures, `psql` ground truth at each step): AuthorAlpha requests a Sequel
link from story 1 to AuthorBeta's story 5 → row lands `Pending` → notification
`StoryLineageRequested` renders correctly for AuthorBeta ("AuthorAlpha requested a story lineage link
with…") → AuthorBeta approves on `/story-lineages` → row flips to `Approved` →
`StoryLineageApproved` notification renders for AuthorAlpha → the link now displays on story 1's
public page ("Story lineage: Sequel of Seed Story: On Hiatus (T)") → AuthorAlpha self-links two own
stories (2→3, Inspired By) → created `Approved` immediately, zero additional notification rows
(confirmed via `psql`) → Groups retrofit: `/group/1` add-story typeahead correctly excludes a Mature
story from search results for a mature-off viewer, then successfully cross-author-adds
`AuthorBeta`'s "Seed Story: Filler (T)" to `AuthorAlpha`'s group. Reject/Delete were not
separately driven live (identical button-wiring to the proven Approve path; both are covered by
dedicated Integration tests with real-DB assertions).
**Scope note:** no dedicated `MyStoryLineagesPageTests` (RazorComponents) — unlike
`SeriesCreateEditPage`, this page maps to a single `@page` route with no dispatcher-reuse trap to
regression-test; CRUD-UI authority for this page is the Integration tier, per the same precedent
`SeriesCreateEditPageTests`' scope note set.

**WU-StatBadgeProducers Stage note (2026-07-31) — Story Lineage becomes a producer.** Cell stays
Stage 5 (unchanged — this filled in a producer under already-Stage-5 cells, same shape as
WU-MsgArchive/WU-GroupsL5b). `ServerStoryLineageWriteService` now drives
`UserStat.AcknowledgedAsInspirationCount`: `ApproveLineageAsync` increments the TARGET author's
counter for type-1 ("Inspired By") links between two different authors; `RejectLineageAsync`/
`DeleteLineageAsync` decrement only when the link was Approved beforehand (transition-delta — both
methods can act on an already-Approved row with no status precondition, so the prior status is
captured before mutating). A self-owned link auto-approves via `RequestLineageAsync` but never
increments — same-author is excluded by the guard, mirroring
`UserStatRecalculator.AcknowledgedAsInspirationCountAgg`'s `IS DISTINCT FROM` exclusion exactly.

**This closes B3's "source is ambiguous" question.** `AcknowledgedAsInspirationCount` is sourced
from this already-built, already-consent-gated mechanism — not a new acknowledgment role. Role 5
"Inspiration" on `AcknowledgmentRole` is retired (seed row removed; roles 1–4 remain): crediting
someone as an inspiration is a claim about a relationship between two STORIES with the source
author's consent already modeled here, not a claim about a person that needs a separate consent
step. See `audit/Badges.md` §"WU-StatBadgeProducers" for the sibling `StoryAcknowledgment` feature
(role Beta Reader) this pairs with, and `.claude/hidden-deferrals-tracker.md` B3.

Verified: Integration tier — 6 new tests in `StoryLineageServiceTests`
(`ApproveLineage_InspiredByCrossAuthor_IncrementsTargetAuthorCounter`,
`ApproveLineage_NonInspiredByType_DoesNotIncrementCounter`,
`RequestLineage_InspiredBySelfOwned_AutoApprovedDoesNotIncrementCounter`,
`RejectLineage_PreviouslyApproved_DecrementsCounter`,
`RejectLineage_StillPending_DoesNotDecrementCounter`,
`DeleteLineage_PreviouslyApproved_DecrementsCounter`) plus the recalculator aggregate test in
`UserStatRecalculatorTests`. Full suite green (2,414 total). Not separately browser-driven this
round — the mechanism is a producer hook onto the already browser-verified (2026-07-12) approve/
reject/delete flow above; the sibling `StoryAcknowledgment` credit→accept→badge round trip was
browser-verified end to end instead (`audit/Badges.md`).

### Feature 10 L2 — WU-AccessGateSweep2 Stage note (2026-09-30): the lineage read guards its source story

**No cell flips — F10 stays Stage 5.** `GetLineageForStoryAsync` joined the *target* through the
filtered `Stories` set but filtered the *source* on the bare `SourceStoryId` FK, so an anonymous probe
of a hidden story id returned its lineage — an existence oracle plus a relationship leak (service
audit §2.6). It now returns empty unless `StoryVisibilityGuard.IsStoryVisibleAsync` passes for the
source (the `ServerStoryArcReadService` shape); the comment and `IStoryLineageReadService` doc say
both sides are gated, and that the target join also applies `StoryStatus`. Authors keep their own
drafts' links; a taken-down source is empty for everyone.

**How verified:** Integration — `ParentVisibilityContractTests`
`AcknowledgmentsAndLineage_DraftStory_EmptyForStranger_PopulatedForAuthor` and `…_TakenDownStory_EmptyForEveryone`
(both fail against the pre-fix read). `dotnet build` green, no new warnings in touched files; `dotnet test` green — Unit 1,022, RazorComponents 701, Integration 1,171; all four PowerShell gates pass.

### Feature 10 L2 — WU-CounterSymmetry Stage note (2026-09-30): approve is Pending → Approved only (orchestrator amendment U1)

**No cell flips — F10 stays Stage 5.**
- **The defect.** `ApproveLineageAsync` overwrote the status with no precondition. A double approve (a
  double click, two tabs, or re-approving an already-Approved or self-owned link) incremented the
  target author's `AcknowledgedAsInspirationCount` twice and sent a second `StoryLineageApproved`.
  The class doc's "a genuine Pending→Approved transition" described a guard that did not exist.
- **The guard.** It has `StoryAcknowledgment.AcceptAsync`'s shape, made race-proof: one conditional
  `ExecuteUpdateAsync` (`WHERE … status = Pending`). Zero rows means
  `StoryLineageValidationException("This lineage request is no longer pending.")`, a 400 that
  `MyStoryLineagesPage` shows inline. The counter and the notification run only after a real
  transition.
- **The tracker.** The set-based write bypasses the change tracker, so the tracked link is reloaded.
  Otherwise a later `FindAsync` in the same circuit would read a stale status.
- **Docs.** The class doc and `IStoryLineageWriteService`'s doc now state the guard. Reject and
  delete keep acting on an Approved row by design: revoking or removing an approved link.

**Test tier: Integration.**
- `CounterSymmetryTests.ApproveLineage_Twice_*`: the second approve is refused, the counter is 1, and
  there is one notification.
- `…_AnApprovalLandingBetweenTheReadAndTheWrite_*` (`InterleavingCommandInterceptor`, per `testing.md`
  §"Testing a check-then-act guard"): refused, the counter is 0, and no notification is sent.
- Mutation-checked: dropping the `Pending` predicate fails both.

**Browser verification (2026-09-30, WU-CounterSymmetry e2e pass) — one bug found and fixed; no flip,
L3-Logic and L4.5 stay 5.** Server-only path, reset workbench, `psql` after every write, render phase
read from the network log. AuthorBeta requested three Inspired-By links into AuthorAlpha's stories over
`POST /api/story-lineage` (6→1, 12→3, 5→7), and AuthorAlpha handled them at `/story-lineages`.
- **The stale-tab approve, both phases.** With the page open, each request was approved once over the
  API, playing a second tab, and the page's own Approve was then clicked.
  - On the circuit (6→1) and on WASM (12→3, whose click's `POST …/approve` answered 400), the page
    showed "This lineage request is no longer pending." inline, not the generic error.
  - Each link landed `status_id` 1, one `StoryLineageApproved` (type 51) and one `+1`: AuthorAlpha's
    `acknowledged_as_inspiration_count` read 1, then 2.
- **Three concurrent approves** of 5→7 over HTTP answered one 204 and two 400s carrying the same
  detail. The counter moved once, to 3, and one notification was sent.
- **Bug (fixed in the same pass): the refused row stayed listed.** The handler showed the refusal but
  did not reload, so the row, which the server no longer listed as Pending, still offered Approve and
  Reject. Reject acts on an Approved row by design, so it would have revoked the approval the other
  tab had just made.
  - **Fix.** `HandleApproveAsync`/`HandleRejectAsync` now treat a `StoryLineageValidationException`
    (no longer Pending) or a `KeyNotFoundException` (the requester removed it) as "moved under the
    page". They keep the message and reload through `ReloadAfterRefusalAsync`, the
    `ModSubmissionsPage` shape. A failed reload is appended to the refusal, never shown instead of it.
    The rule is now written in `error-handling.md` §"Layered error boundaries".
  - **Test tier: RazorComponents.** `MyStoryLineagesPageTests` (4): a stale Approve keeps the
    message and drops the row; an Approve or a Reject of a request the requester removed drops it too
    (Reject never refuses an Approved row, so that is its only stale case); and a reload that also
    fails keeps the refusal and appends its own message. Mutation-checked: all four fail with the
    reload removed.
  - **Re-verified in the browser after the fix,** after a rebuild and restart, with two fresh requests
    (4→2, 6→7). On the circuit (4→2) and on WASM (6→7: the click's `POST …/approve` answered 400,
    then the page re-read `GET /api/story-lineage/manage`), the message stayed and the stale row was
    gone. 6→7 was the last request, so the list read "No pending requests.". Each link landed
    `status_id` 1 with one `+1` (the counter read 5). The 6→7 approval sent no second
    `StoryLineageApproved`, because an unread one for the same target story (7) already existed: the
    notification service's existing dedup. The console was clean.

### Features 8, 9, 10 (and 45) — WU-ThrottleCoverage Stage note (2026-10-13; owner ruling D20's throttle half) — F8/F9/F10 L4.5 5→1 at its review fixes and 1→5 at its browser pass the same day (tracker H33 closed); every other cell stays 5

D20 (answered 2026-08-07) ruled that every user-reachable write either spends a bucket or is
enumerated as exempt with its reason (`security.md` §"Write Throttling"). Here:
- **F8 L2 (arcs) and F9 L2 (series):** `CreateArcAsync` and `CreateSeriesAsync` call
  `EnsureAllowed(ContentCreate)` right after their auth guard (D20: "user-visible content creates that
  shipped without the call"). Edits, deletes, membership and reorder stay unthrottled under the
  enumeration (the edit exemption is conditional on D20's caps half). The endpoint docs
  (`StoryArcEndpoints`, `SeriesEndpoints`) no longer say the service has no bucket.
- **F9 L3-Logic:** `SeriesCreateEditPage`'s save catch-all showed a fixed "An unexpected error
  occurred" for every non-validation failure, so a throttled author was never told to wait. It now
  follows the `ExceptionPresenter` catch-site pattern (typed refusals show their own message; an
  unexpected failure is logged and shown with its error id).
- **F10 L2 (lineage):** `RequestLineageAsync` spends a `RepeatRequest` token (5 burst, 1 per 3 min —
  D20's report tier) on every request that notifies the target's author. A self-owned link
  auto-approves, notifies nobody and never draws the bucket, and neither does a request to a target
  story with no author (its account gone: nobody to tell); the call sits after validation and before
  the tracked mutation (the placement exception — a refused re-request must not leave a dirty row on a
  circuit's write context). First requests are charged too: `DeleteLineageAsync` removes a Rejected
  row from the UI, so delete-then-request takes the fresh-insert branch with no trace of the repeat,
  which is why D20's alternative state guard would not close the vector. The reject path's comment and
  `IStoryLineageWriteService`'s doc claimed the kept Rejected row "prevents immediate re-request
  spam"; both now say the row is an audit trail and the bucket is the bound (service audit §4).
  The six-collaborator friction is routed to the owner (decision row 26 (c)).
- **F45:** the view ping's anonymous, unthrottled posture is ratified (D20 sub-ruling) — the Settled
  note under Feature 45. No code changed.
- **Verified by:** Integration `WriteThrottleTests` (real `ServerWriteRateLimitService`, tightened
  table): `StoryArcCreates_ThrottleUnderContentCreate`, `SeriesCreates_ThrottleUnderContentCreate`,
  `LineageRequests_ThrottleOnlyWhenNotifying` (four self-owned links free, then the bucket). Unit
  `WriteThrottleWireContractTests` pins the WASM leg for both clients (arc and series creates rebuild
  `WriteRateLimitExceededException` from the 429 body). RazorComponents
  `SeriesCreateEditPageTests` (+2: the throttle message, the generic message). Mutation-checked:
  charging self-owned lineage links fails the lineage test. No browser pass was run (none available);
  tracker H33 holds it. The build changed no `status.md` cell.
- **Review fixes (2026-10-13).**
  - **Cells: F8, F9 and F10 L4.5 5→1** (headlines above). Each surface can now refuse a write with a
    wait message, and F9's page error output changed; none was driven in a browser. This follows the
    campaign precedent (F46/F53 at WU-ModerationIntegrity: a new refusal changes what users see).
    Tracker H33's pass returns them to 5.
  - **F10 L2: the lineage placement was unproven.** `testing.md` requires a throttle call placed
    off method entry to prove its placement; only the acknowledgment side did. New Integration
    `WriteThrottleTests`: `LineageRequests_ThatFailValidation_SpendNoToken` (a duplicate Pending
    request and an unknown type, each repeated past the bucket, refused by validation and spending
    nothing), `AThrottledLineageReRequest_LeavesNoDirtyRowForTheScopesNextSave` (a refused re-request
    of a Rejected link, then a self-owned link saved on the same scope: the row is still Rejected),
    `LineageRequests_ToAnAuthorlessTarget_SpendNothing` (the build's authorless-target exemption,
    which spec R4's "only when `!selfOwned`" did not name: `RepeatRequest` guards a notification, and
    there is nobody to notify). Mutation-checked: the call at method entry fails the first (and three
    others), the call after the mutation fails the second, charging authorless targets fails the
    third.
  - **The respondent side is now in `security.md`'s enumeration:** `RejectLineageAsync` (silent,
    target-owner-scoped, bounded by the requester's `RepeatRequest`) was named nowhere.
- **Browser verification (2026-10-13) — F8, F9 and F10 L4.5 1→5; tracker H33 closed.** Server-only
  path, standing workbench (not reset), `psql` after every write. Each flow ran on the circuit
  (`_blazor/negotiate` and no `/api` call; the Auto hash removed before each full load), then on WASM
  after a server restart had emptied the in-memory buckets (the action's `/api` request and its 429).
  - **F8 (AuthorBeta, story 6's arc panel):** the sixth create call in a burst read "You're doing that
    a little too fast — please wait 120 seconds and try again." on both phases (`POST /api/story-arcs`
    429 on WASM); `story_arcs` held only the saved rows. The circuit saved five first. On WASM one of
    the five calls failed the overlap rule (a timing slip in the driving script) and still spent a
    token, so four saved: `CreateArcAsync` charges at method entry, where `security.md` places
    `ContentCreate`.
  - **F9 (AuthorAlpha, `/series/new`):** five series saved and the sixth showed the wait message, not
    "An unexpected error occurred", on both phases (`POST /api/series` 429 on WASM); `series` gained
    five rows per phase.
  - **F10 (AuthorAlpha, `/story-lineages`):** five requests to AuthorBeta's stories saved Pending and
    the sixth read "please wait 180 seconds" on both phases (`POST /api/story-lineage` 429 on WASM),
    leaving no row for the refused link. With the bucket empty, self-owned links still saved Approved
    (three on the circuit, one on WASM).
  - **Runtime bug, fixed (commit "browser-pass fixes"), F9 L3-Logic:** `SeriesCreateEditPage` kept a
    create flag and an edit id that nothing cleared, so on the router-reused instance a route already
    visited was skipped. Write → New Series from `/series/10/edit` opened "Create a New Series" with
    "E2E Throttle WASM Series 5" still in the name field; saving would have created a second series
    under that name. The dispatcher now keys on the one nullable route value, recorded at the start of
    every load (`layer3-logic.md` §"Route-Parameter Dispatchers" states the create/edit rule).
    RazorComponents `SeriesCreateEditPageTests` +2 (`CreateEditCreate_OnSameInstance_ShowsABlankCreateForm`,
    `EditCreateEdit_SameSeries_OnSameInstance_ReloadsTheSeries`), both failing before the fix.
    Re-verified in the browser on both phases. Four other create/edit pages have no dispatcher at all
    and show the same symptom (story, blog post, group: reproduced); outside this WU's surface, tracker
    **D18**.
  - Server log: no `fail:`/`crit:` from these flows. Browser console: no errors on a WASM refusal.

## Feature 45 — View Count Tracking

**Built end-to-end (WU-SignalBuffering, 2026-07-06).** Divergence from spec (§7 `Story.ViewCount`
"Updated by Redis background worker"; §5.3's view-count sort exclusion now closes the last gap):
views are a **non-sortable, on-demand informational metric** — never a sort key, never a permanent
badge (view-count-not-a-sort settled 2026-07-06, the anti-popularity-snowball philosophy applied to
the last popularity-shaped surface). `Story.ViewCount` (and the never-written
`ChapterContent`/`BaseBlogPost` copies) **dropped** — a hot mutable counter on the hot read row is
a write-amplification trap; accumulation lives in **`daily_story_stats`** (per-story/day,
migration-managed raw DDL, no EF model — ground truth, NOT a rebuildable mart; PK
`(story_id, stat_date)`, partition-ready; FK CASCADE cleans history on story delete). Migration:
`R2_ViewCountToDailyStoryStats`.

**Settled (owner ruling D20's view-ping sub-ruling, answered 2026-08-07; recorded WU-ThrottleCoverage,
2026-10-13 — do not revisit):** the ping stays **anonymous and unthrottled**. Views are never sort
keys, so inflation cannot distort discovery, and pings land in `ViewCountBuffer` keyed by story id,
so a bot loop grows a dictionary bounded by story count, not by ping count. The accepted
consequence — displayed view counts are inflatable, and authors read them — is recorded rather than
defended. No IP-partitioned limiter: the proxy-aware client-IP policy it would need does not exist
yet (Phase 7 ForwardedHeaders). Recorded in `security.md`'s unthrottled-surface enumeration.

- **L1 — Stage 5.** `daily_story_stats` DDL applied + verified (Testcontainers migrate + live dev DB).
- **L2 — Stage 5 (unthrottled by ratification — owner ruling D20, recorded WU-ThrottleCoverage, 2026-10-13; see the Settled note above).** `IViewCountWriteService.RecordViewAsync` (anonymous counts — no auth gate) →
  `ViewCountBuffer` (per-story sum, O(1)) → `ViewCountFlusher` (batched `unnest … ON CONFLICT`
  additive upsert into today's UTC row — the `+=` merge accepts a rare retry-replay over-count on
  a lossy metric, documented in-file; EXISTS guard vs deleted stories; restore-on-failure) →
  `ViewCountFlushWorker` (5 s, shutdown drain, removed from the test host by `TestAppFactory`).
  Lifetime total = `IStoryReadService.GetStoryTotalViewsAsync` (raw `SqlQuery` SUM — on-demand
  only, never in a listing projection). Telemetry: `CanalaveTelemetry.ViewCount`.
- **L3-Logic — Stage 5.** `view-ping.js` (fires once on first scroll OR 5 s dwell, never page
  load — bots/bounces filtered) registered by `StoryPage` (re-arms on in-place story change;
  `IAsyncDisposable`); `StoryViewStats` composite (sanctioned user-input-driven injection) in
  `StoryCard`'s caret dropdown — fetches the SUM only when the user asks.
- **L3.5/L4 — Stage 5.** Dropdown item reuses the caret menu's structure/classes.
- **L4.5-Browser — Stage 5 (2026-07-06).** Story-page scroll fired the ping → `daily_story_stats`
  row landed post-flush (psql ground truth); "View stats" reveal showed "1 view" (matches SUM);
  sort dropdowns verified view-free (`/discover`: Random | Date published only).
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto; the view-count ping verified in a real WASM runtime during
  the flip's browser wave (202 accepted; server log/DB ground truth). Full wave narrative + the
  7 bugs found/fixed: `workplan.md` WU-GlobalFlip. **L8 — N/A.**
- **Tests:** Unit (`ViewCountBufferTests`), Integration (`ViewCountFlushTests` — buffering,
  same-day accumulation, cross-day SUM, zero default, deleted-story guard, FK cascade),
  RazorComponents (`StoryViewStatsTests` — no fetch until asked, formatted reveal,
  singular/plural, reset on story change).

### WU-ComponentSoundness Stage note (2026-06-27)

**Cells affected:** F5 L3-Logic (StoryPage, StoryDeck) + F7 L3-Logic (ChapterReadingPage) — correctness
polishes inside already-aligned Stage-5 cells; no stage transition.

**F2 — StoryDeck list-keying (data-corruption bug, now closed):**

`StoryDeck.razor` now carries `@key="story.StoryId"` on `<StoryCard>` in the `@foreach` loop.

Root cause: `UserStoryInteractionPanel.OnParametersSet` caches `State → _localState` once (`if
(_localState is null) _localState = State;`) and stops syncing. Without `@key`, Blazor matched `<StoryCard>`
instances **positionally**, so paginating or filter-swapping recycled the position-0 instance for a new
story. The recycled panel's `_localState` still held the prior story's state; `FlushAsync` subsequently
wrote those booleans to the wrong `StoryId`, silently corrupting the interaction row.

Fix: `@key="story.StoryId"` forces Blazor to destroy and recreate the keyed component tree whenever the
`StoryId` in that slot changes — the fresh instance starts with `_localState = null` and seeds correctly
from the new story's `State` parameter. (Convention recorded in `layer3.5-structure.md`
§"`@key` on `@foreach` over stateful children".)

Covering tier: **RazorComponents** — `StoryDeckTests.KeyedList_WhenStorySwapped_PanelReflectsNewStorysState_NotPreviousStorysState`.

---

**F1 — StoryPage lifecycle reload (`[PersistentState]` gotcha):**

`StoryPage.razor` now implements the MessagesPage route-dispatcher pattern: `_initialized` flag +
`_loadedStoryId = int.MinValue` sentinel; `OnInitializedAsync` keeps auth-resolution + first load (using
`??=` for anti-flicker); `OnParametersSetAsync` guards on `StoryId == _loadedStoryId` then does a plain
reassignment (`Story = await …;` — **not** `??=`) before loading.

Root cause: Blazor reuses the same component instance on same-route-template navigation; `OnInitializedAsync`
does not re-fire. The prior code loaded only in `OnInitializedAsync`, so navigating from
`/story/1/slug-a` to `/story/2/slug-b` without a full page reload left the old story's data on screen.

`[PersistentState]` gotcha: the `??=` guard in `OnInitializedAsync` is correct (anti-flicker across
prerender → interactive). But in `OnParametersSetAsync` the field is already non-null from the prior
prerender payload — `??=` would short-circuit the DB call for the new StoryId. Plain assignment
(`Story = await …`) is required there.

Covering tier: **manual boot gate** (no bUnit test — `[PersistentState]` prerender-to-interactive
handoff cannot be asserted in bUnit). Listed in verification checklist for the upcoming human E2E pass.

---

**F1 — ChapterReadingPage lifecycle reload + scroll-JS re-registration:**

`ChapterReadingPage.razor` now implements the MessagesPage pattern with composite key
`(StoryId, ChapterNumber, VersionOrder)` (sentinel: `int.MaxValue` for `VersionOrder` because `null` is
a valid value meaning "primary version").

`DisposeJsRegistrationAsync()` extracted from `DisposeAsync()` and called at the top of
`OnParametersSetAsync` before `LoadChapterAsync()`, so `readingProgress.dispose` releases the old
chapter's scroll subscription before the new chapter DOM is rendered. `OnAfterRenderAsync` dropped the
`firstRender` gate in favor of the `_jsRegistered` flag alone — re-registration fires on the render
after the new chapter loads, binding to the fresh `#chapter-body` element. `_progressReached90` is
reset inside `LoadChapterAsync()` so the 90%-scroll → `MarkStartedAsync` milestone fires correctly for
new Chapter 1s.

Covering tier: **manual boot gate** (JS-interop + multi-service; listed in E2E verification checklist).
Convention for the route-dispatcher pattern recorded in `layer3-logic.md`
§"Route-parameter dispatchers reload in `OnParametersSetAsync`".

---

### Cluster-level notes
- The commented-out `[NotMapped] IReadOnlyCollection<IStoryTag> StoryTags => StoryTags.ToList();` in
  `Story.cs` would have been infinitely recursive — correctly left disabled; the explicit-interface version
  is the live one.

## L4.5-Browser verification (2026-07-01) — F4 + F5 → Stage 5, one bug fixed same-session

> **F4's half was superseded and then re-verified (2026-09-30):** WU-StoryLifecycle replaced the
> publishing flow this pass drove, and F4 L4.5 dropped to 1. The WU-StoryLifecycle browser pass later
> the same day drove the new flow and returned it to 5 (F4 headline and the browser-verification Stage
> note). F5's half is unaffected.

Real-form pass as TestUser: `/story/new` (title, short desc, Quill long description, character
typeahead → Cynthia w/ Primary priority row, setting → Canalave City, genre → Adventure) →
Create Story landed on `/story/{id}/edit` with all values retained → title edit → Save Changes →
story page shows updated title, tags, and description. Discover cards and story-page display (F5)
exercised across the seeded corpus and the newly created story.

**Bug fixed:** `CreateStoryAsync` never stamped `PublishedDate`/`LastUpdatedDate` (the mapper
covers only `IEditableStoryProperties`), so UI-created stories carried `DateTime.MinValue` →
Postgres `-infinity` → story pages showed "Published Jan 1, 0001". Fixed: server-stamps both at
create (like `AuthorId`) and bumps `LastUpdatedDate` in `UpdateStoryAsync`. Verified via psql
after a browser round-trip. *(Superseded in part 2026-09-30, WU-StoryLifecycle / owner ruling D2:
create still stamps `LastUpdatedDate` but no longer `PublishedDate` — the column is nullable and
NULL means "never published"; it is stamped on first publication instead.)*

**Coverage exception:** the cover-art `InputFile` → `IImageStorageService` browser interaction
could not be driven (browser-automation file-upload API mismatch in the 2026-07-11 WU38 pass's
tooling — not an app defect); the storage service itself is Integration-covered and was WU12
endpoint-verified. Re-verify the InputFile wiring when a browser pass with working file upload
is available. *(Closed 2026-10-13: the WU-BlobCleanup browser pass drove the `InputFile` on both
render phases with a file built in the page; the technique is in `run-server/SKILL.md`, and the pass
is in F4's WU-BlobCleanup Stage note.)*

### WU-AuditFixPass note (2026-07-18)

Security fix MA-201 closed: `ServerStoryWriteService` sanitizes `LongDescription` on create AND
update via `IHtmlSanitizationService` (stored-XSS on the story page; the sibling Series service
already did this). Also: `StoryPage`/`StoryEditorPage`/`SeriesCreateEditPage`/`SeriesPage`
missing-entity paths now use `NavigationManager.NotFound()` (real 404); `StoryPage`'s view-ping
dispose catch is typed; `MyStoryLineagesPage` + `SeriesCreateEditPage` error blocks normalized to
`InlineAlert`. Covered by Integration tier (`StoryWriteServiceTests` sanitize-on-create/update).
Full detail: `workplan.md` WU-AuditFixPass.

### WU-AuditFixPass-2 note (2026-07-18)

Endpoint-authz sweep + Tier-2, F4/F5 (cells stay Stage 5 — behavior corrected in place; resolves the
slice files' "proposes reopen" flags):
- **F4/F5 `GetStoryForEditAsync`:** was auth-floor-only — any authenticated user could read any story's
  edit DTO (incl. the moderation-only `PostApprovalStatus`). Now loads the owner and throws
  `UnauthorizedAccessException` (→403); endpoint wrapped in `ExecuteWriteAsync`; `ClientStoryReadService`
  + `StoryEditorPage` map 403→forbidden (MA-301 chapter shape).
- **F5 `GetStoryIdsByAuthorAsync`:** the `IgnoreQueryFilters(["ContentRating"])` bypass was keyed to the
  client-supplied authorId (any viewer could enumerate another author's rating-hidden story ids); now
  owner-only (`authorId == ActiveUser.UserId`).
- **F5 MA-204:** dead `ChapterNames` projection + DTO field removed (correlated subquery on the hottest
  read; also closed a latent draft-title leak).
- **F5 MA-203:** `StoryPage`'s 6 supplementary loads parallelized via `Task.WhenAll` in a shared
  `LoadSupplementaryAsync` (both lifecycle methods).
Covered: `StoryEndpointsTests` (Integration) + browser E2E (owner mature-on sees 5, mature-off non-owner
3; cross-author `/edit`→403). Full detail: `workplan.md` WU-AuditFixPass-2.

---

**WU-ParentVisibility slice (2026-07-26) — F4/F8/F9/F15.** New `StoryVisibilityGuard`, which leans
on the filtered `readDb.Stories` set rather than restating its rules and layers only per-story reveal
consent on top; it also exposes `IsStoryPublishedAsync` for the confidentiality axis alone, used by the
deliberately rating-permissive writes. Surfaces: `GetArcsForStoryAsync` — `ServerStoryArcReadService`
was the **only service in the codebase injecting no `IActiveUserContext`**, so it was structurally
incapable of gating, and leaked arc titles and chapter ranges (a story's narrative skeleton) for
M-unrevealed, Draft/PendingApproval/Rejected and taken-down stories; its constructor changed.
`GetStoryTotalViewsAsync` is raw SQL with no EF model and therefore no query filter of any kind, and
confirmed the existence and popularity of hidden stories; it now returns 0, which is what a story with
no recorded views returns anyway. `RequestLineageAsync` gates its **target** story (the source was
already ownership-gated) — the found-vs-not-found distinction was an id oracle across the whole
`Stories` keyspace. View counts validate at **drain time** via `DiscoveryMartSchema.VisibleStory`,
keeping the anonymous, highest-volume write path buffer-only.

Invariant, guards, and the two root causes: `identity-and-authorization.md` §"Parent-visibility guards" (conditionality kind (g)). Enforcement: `Tests.Integration/ParentVisibilityContractTests.cs`. Full narrative: `workplan.md` WU-ParentVisibility. **No Stage number changed — every affected cell was already Stage 5 and remains 5.**

---

**WU-QuickFixes slice (2026-09-20) — F8/F9/F10 + the acknowledgment seam.** Two composition-root /
idiom cleanups under cells that were already Stage 5 and stay 5; no behavior change.

- **`ServerStoryArcWriteService` field copies removed (MA-211).** It copied its primary-constructor
  parameters into `_writeDb`/`_activeUser` where every sibling write service (`ServerSeriesWriteService`,
  `ServerStoryLineageWriteService`, …) uses `writeDb` and the inherited `ActiveUser` directly. The
  `_activeUser` alias existed to dodge CS9107/CS9124 — but that warning only fires for a parameter
  *also* passed to the base constructor, and `ActiveUser` (the protected property the base already
  exposes) answers it without an alias. Build is clean, no new warnings.
- **Read/write DI registrations forwarded, not duplicated (MA-107).** `IStoryArcReadService`,
  `IStoryLineageReadService` and `IStoryAcknowledgmentReadService` were each registered against the
  *write* class alongside the write interface, so a scope that injected both got **two instances** of
  the same service. Each read interface now forwards to the write registration. Rule recorded in
  `layer2-services.md` §"Registering an inherited pair"; the full seven-cluster sweep is in
  `workplan.md`'s WU-QuickFixes entry.

**How verified:** `dotnet build` green; **Integration** tier covers both (the whole suite resolves
these services through the real host — `StoryArcServiceTests`, `StoryLineageServiceTests`,
`StoryAcknowledgment*` and every endpoint test exercise the new registrations). **No Stage number
changed.**

