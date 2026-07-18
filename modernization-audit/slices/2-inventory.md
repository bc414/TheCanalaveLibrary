# Slice 2 — Patterns Inventory (Stories & Series)

Headings per `modernization-audit/dimensions.md`, in order. `mechanism` / `exemplar` / `deviations`.

## 1. Pagination
mechanism: Offset paging on scalar StoryId page, then `GetListingsByIdsAsync` hydration (two-step); `PagedResult<T>` envelope at the HTTP boundary only (tuple return in the interface). `StoryDeck` embeds the `PaginationControls` atom, which self-hides at `TotalPages ≤ 1`. Series/Arc/Lineage lists are unpaged (small owner-scoped sets).
exemplar: `Server/Stories/ServerStoryReadService.cs:185-202` (`GetRecentListingsAsync`)
deviations: `StoryListingPageDto` (MA-207) is a dead tuple-wire record superseded by `PagedResult<T>`.

## 2. DTO mapping
mechanism: Two-step row-projection (lean EF-translatable `record` intermediate → in-memory DTO build) on the dense reads (`GetStoryByIdAsync`); direct `.Select()` to positional `record` DTOs on the leaner ones. Sprite identifiers passed through raw (resolved at render). DTOs are records; mutable classes only where a form binds them (`StoryPropertiesViewModel`, `StoryExternalLinkEditDto`, `CreateStoryDTO`/`StoryUpdateDTO` which implement `IEditableStoryProperties`).
exemplar: `Server/Stories/ServerStoryReadService.cs:26-107` + private `StoryDetailRow`/`StoryListingRow` records
deviations: `StoryDetailsDTO.ChapterNames` is a dead projected field (MA-204). Two near-parallel edit/read DTO shapes (`StoryUpdateDTO` vs `StoryDetailsDTO` vs `StoryPropertiesViewModel`) — deliberate firewall layering, not duplication (see §15).

## 3. Error surfacing
mechanism: Typed validation exceptions (`StoryValidationException`, `SeriesValidationException`, `StoryArcValidationException`, `StoryLineageValidationException`) carrying an `Errors` list; endpoints wrap in `EndpointHelpers.ExecuteWriteAsync` (exception→status); client impls invert via per-class `ThrowIfWriteFailedAsync`; UI surfaces via `InlineAlert` + `ExceptionPresenter`.
exemplar: `SharedUI/Stories/StoryEditorPage.razor:282-291` (typed-catch → `_forbidden`; generic → `ExceptionPresenter.GetUserMessages`)
deviations: `SeriesCreateEditPage`/`MyStoryLineagesPage` hand-roll validation danger-`<div>`s instead of `InlineAlert` (MA-205, H-20).

## 4. Form patterns
mechanism: Two shapes coexist by design. `StoryPropertiesForm` = full `EditForm` + `StoryPropertiesViewModel` (DataAnnotations) + `EditorView` pull-on-submit + structured tag state rebuilt on every change. `SeriesCreateEditPage`/`MyStoryLineagesPage` = plain `@code` state + `@bind` inputs + manual `HandleSubmitAsync` (no ViewModel — matches the "toggle/selection state = @code is the model" rule). Enum/bool selects use name-option-values or explicit `@onchange` handlers (no bool-`@bind`/enum-mix traps).
exemplar: `SharedUI/Stories/StoryPropertiesForm.razor:15-219`
deviations: none in the select idioms (PairingBuilder `@bind` on enum uses name option values — the sanctioned pattern 2).

## 5. Flyout/overlay mechanics
mechanism: `StoryCard` caret = `relative` container + `@onclick="Toggle"` + `_menuOpen` flag + full-screen `data-flyout-catcher` catcher-div at `z-(--z-dropdown)` + absolute panel (the UserCard caret pattern). `ConfirmDialog` (atom) for destructive series delete. Tokenized z/shadow throughout.
exemplar: `SharedUI/Stories/StoryCard.razor:38-82`
deviations: none observed.

## 6. Optimistic updates & debounce
mechanism: The slice's only buffered write is `ViewCount` — signal-buffering, not per-component debounce: synchronous `buffer.Record`, worker-flushed (5s), coalesce-by-sum. No optimistic-toggle debounce here (that's the USI panel, S4).
exemplar: `Server/Stories/ServerViewCountWriteService.cs:12-16` → `ViewCountBuffer.cs:39`
deviations: none observed.

## 7. Disposal & lifecycle
mechanism: `StoryPage` implements `IAsyncDisposable` (view-ping `DotNetObjectReference` + JS dispose; re-arms on in-place story change). Route-param dispatchers use the guarded sentinel + `_initialized` reload with plain-assign for `[PersistentState]` fields. `StoryViewStats` self-heals its reveal on StoryId change. `ViewCountFlushWorker` drains once more after cancellation.
exemplar: `SharedUI/Stories/StoryPage.razor:194-235`
deviations: `StoryPage.razor:225` bare `catch {}` on the dispose JS call (MA-206).

## 8. Query shape
mechanism: Factory-per-method reads; shared `ApplyFilters(IQueryable<Story>, filter, hasFts)` composition reused by `GetListingsAsync`/`GetRandomBatchAsync`/`FilterCandidateIdsAsync`; character-vs-flat-tag `||` include branch; FTS via `EF.Property<NpgsqlTsVector>(...).Matches(PlainToTsQuery)` + `.Rank()` for Relevance; `RecentlyRead` uses `Any()`-first-key to sink never-read stories under DESC. Explicit joins through `Stories` on Series/Lineage reads so viewer filters apply. Anonymous-type projection for nullable-FK-vs-missing-row distinction not needed here (write lookups use unfiltered writeDb).
exemplar: `Server/Stories/ServerStoryReadService.cs:353-427` (`ApplyFilters`)
deviations: `ServerSeriesReadService.GetMembershipsForStoryAsync` runs a per-series loop (2 queries × N series) — pragmatic in-memory assembly, documented "small N" (`:91-133`); a mild, acknowledged N+1.

## 9. Write-method skeleton
mechanism: auth guard (`RequireAuthenticatedUser`/`UserId is not int`) → `dto.CanSave()` → existence + ownership checks (via unfiltered writeDb) → construct/mutate → `SaveChangesAsync` → best-effort post-commit notify. Story create additionally: `rateLimit.EnsureAllowed(ContentCreate)` + structured-tag gates + slug generation + `ExecuteUpdateAsync` counter. Rate-limit is deliberately absent on Series/Arc/Lineage writes (documented in each endpoint class summary — author-gated, lower-abuse; not a finding).
exemplar: `Server/Stories/ServerStoryWriteService.cs:15-67` (`CreateStoryAsync`)
deviations: **`ServerStoryWriteService` does NOT sanitize `LongDescription`** (MA-201, Tier 1) — the one write path in the cluster that skips the sanitize step every EditorView-fed sibling performs. `RequireAuthenticatedUser` guard duplicated ×4 (MA-210). Arc service stores ctor params in fields (MA-211).

## 10. Endpoint & client shape
mechanism: Kebab-plural route groups (`/api/stories`, `/api/series`, `/api/story-arcs`, `/api/story-lineage`, `/api/view-counts`); thin pass-throughs; writes wrapped in `ExecuteWriteAsync`; per-endpoint auth documented with rationale (public reads mirror the public consuming page; genuinely-unsure reads default to `RequireAuthorization`); ViewCount ping returns `202 Accepted`; cover upload `DisableAntiforgery()` (cookie-auth stateless API). Client: protected `Http`, per-class `ThrowIfWriteFailedAsync` (400→typed validation, 401/403→Unauthorized, 404→KeyNotFound), nullable-tolerant reads.
exemplar: `Server/Stories/StoryEndpoints.cs:7-144` (exemplary auth-rationale docs)
deviations: four near-identical `ThrowIfWriteFailedAsync` switches (Story/Arc/Lineage/Series) differing only in exception type/message — the MA-008 (S0) validation-exception-family root cause; not re-filed here.

## 11. Sanitization & derived fields
mechanism: `ServerSeriesWriteService` sanitizes `Description` once on save; `StorySlug.Slugify` (pure Core transform, unit-tested) + `ServerStoryWriteService.GenerateUniqueSlugAsync` (server-only Tier-3 uniqueness scan). Word count for the *story* is maintained by the chapter write path (S3), not here.
exemplar: `Server/Series/ServerSeriesWriteService.cs:40` + `Core/Stories/StorySlug.cs:13-17`
deviations: **story `LongDescription` is never sanitized** (MA-201) — the glaring gap in this dimension.

## 12. Notification triggering
mechanism: Best-effort post-commit `try { await notifications.NotifyX...Async(...) } catch (Exception ex) { logger.LogError(...) }` after the primary `SaveChangesAsync`; semantic methods (`NotifyStoryLineageRequestedAsync`/`ApprovedAsync`); drop-self honored (self-owned lineage auto-approves with no notification).
exemplar: `Server/Stories/ServerStoryLineageWriteService.cs:69-84`
deviations: none observed (Series/Arc/ViewCount trigger no notifications by design).

## 13. Counter updates
mechanism: One counter in slice — `StoriesWritten` via atomic `ExecuteUpdateAsync(SetProperty(+1))` after story create.
exemplar: `Server/Stories/ServerStoryWriteService.cs:63-64`
deviations: none (no tracked `++`; missing-row no-op unacknowledged by comment but rows exist by construction).

## 14. Test idioms
mechanism: Exemplary and uniform across all three tiers. Integration: `SeedUserAsync`/`SeedStoryAsync` GUID-suffixed, per-test FK-parent seeding with real-world-flow comments, absolute assertions off Respawn resets AND relative-order assertions where the "Postgres" collection accumulates shared state, mutation-sanity checks (drop-the-predicate baseline), raw-ground-truth helpers bypassing the service. RazorComponents: bUnit Loose JS, aria-label / `type='submit'` / `[href=...]` collision-free selectors, `FindComponents<T>`, the `@key` F2 regression (`cut.Render(...)` re-set), fakes registered per tested surface. Unit: directly-constructed Core types, theory-driven.
exemplar: `Tests.Integration/StoryLineageServiceTests.cs` (cross-author workflow, notification assertions, cascade, viewer-filter drop) + `Tests.RazorComponents/StoryDeckTests.cs:257-291` (@key regression)
deviations: `StoryListingsTests.cs:114` stale "DataSeeder's TestUser" comment vs seeded `_testUserId` (MA-213); `StoryWriteServiceTests` mixes `SetActiveUser` with a direct `FakeActiveUserContext.UserId =` in one helper (trivial). No test pins `LongDescription` sanitization — because there is none (MA-201).

## 15. Code economy
Product ≈ 7,960 LOC; owned test ≈ 5,576 LOC. Densest cluster in the audit; the growth over calibration's ~7.0k estimate is real feature scope (Lineage, Arcs, External links, ViewCount all landed).

**(a) Per-sub-cluster LOC + pattern-tax share:**
| Sub-cluster | product | test | pattern-tax note |
|---|---|---|---|
| Stories read/write svc + endpoints (Server) | ~1,240 | ~1,600 (Detail/Listings/Tagging/WriteService/Recent) | `ServerStoryReadService` (498) carries 7 read methods + shared `ApplyFilters` — dense but not wasteful |
| Story components (SharedUI) | ~2,300 | ~1,900 (Card/Deck/Desktop/Mobile/PropertiesForm) | StoryPropertiesForm (465) + editor page (322) are the structured-tagging cost; Desktop/Mobile pair = the headline (see (c)) |
| Series (all layers) | ~750 | ~700 (SeriesService 493 + card/box/create-edit) | smallest full CQRS+endpoint+client+page stack; SeriesCreateEditPage (483) is the membership-management cost |
| Lineage (all layers) | ~700 | ~600 | request/approve/reject/delete + owner inbox page (350) |
| Arcs (all layers) | ~330 | ~280 | range-validation service + manager panel |
| ViewCount (signal buffering) | ~230 | ~250 | textbook 4-piece pattern; near-zero waste |
| Client impls | ~455 | — | 4× `ThrowIfWriteFailedAsync` boilerplate (root cause MA-008/S0) |

**(b) Compression candidates (LOC saved / sites collapsed / machinery cost):**
- **Delete dead types** — `StoryListingPageDto` (10 LOC / 1 / zero — MA-207); `StoryCharacterRelationship.cs` tombstone (1 LOC / 1 / zero — MA-208); `StoryDetailsDTO.ChapterNames` field + projection (~5 LOC + one hot-path subquery removed / 1 / zero — MA-204). **Classification: pure win** (all three).
- **Extract a `StoryDisplayFormat` static** for the triplicated `WordCountDisplay`/`StatusLabel`/`RatingLabel` (~60 LOC saved / 3 sites collapsed / one small static class; fixes the 999,999 edge quirk once — MA-209). **Classification: trade.**
- **`IActiveUserContext.RequireUserId()` extension** collapsing the 4 `RequireAuthenticatedUser` copies (~12 LOC / 4 / one Core extension — MA-210). **Classification: pure win** (shape unification).
- **Marker interface on the validation-exception family** would collapse the 4 client `ThrowIfWriteFailedAsync` switches (cross-slice, root cause MA-008/S0). **Classification: trade** (Brian decides; noted, not re-scoped here).

**(c) Near-identical pairs — StoryDesktop/StoryMobile headline assessment:**
`StoryDesktop.razor` (228) and `StoryMobile.razor` (221) have **byte-identical `@code` blocks** (same parameters, same `WordCountDisplay`/`StatusLabel`/`StatusBadgeClass`/`RatingLabel`/`RatingBadgeClass`/`_coverArtFailed`/`HandleCoverArtError`) and render the **same composites in the same order** (StoryDownloadLinks, UserStoryInteractionPanel, TagChip, OC names, Pairings, SeriesMembershipBox, StoryLineageBox, RichTextView, ChapterList, StoryExternalLinksRow, RecommendationSection, RelatedStoriesSection). Measured against the codebase's own "separate components only when structurally different" rule (`layer3.5-structure.md`), the **actual** structural difference is small: (1) cover art is moved above the metadata block on mobile, (2) the "Updated" date is shown on desktop only, (3) spacing/text-size tokens (`max-w-3xl px-4 py-8 text-3xl gap-8` vs `px-3 py-5 text-2xl gap-6`). That is cover-reorder + one field + responsive sizing — the boundary case the rule warns about, not the "top-bar-vs-bottom-sheet" structural divergence it sanctions. The duplication is **doubled at the test tier**: `StoryMobileTests` (322 LOC) is a near-verbatim mirror of `StoryDesktopTests` (345 LOC) — its own doc says "Tests here mirror StoryDesktopTests." So the pair costs ≈ 450 product + ≈ 670 test LOC for a difference a responsive component (`@if`-ordered cover + `md:` prefixes) could carry in one file + one test. **Classification: trade** — a real (if thin) structural difference exists, so no verdict; but this is the strongest merge candidate in the audit's Desktop/Mobile pairs, and unlike `HomeDesktop`/`HomeMobile` (S1, spacing-only placeholder) it also duplicates ~35 LOC of live display logic. Contrast `StoryDeck` (single responsive component — correctly *not* split).

**(d) Mechanical repetition with a fixable root cause:**
- Triplicated story display logic (MA-209) — root cause: `StatusBadges` centralized only the badge *class*, not the label/wordcount switches.
- 4× `RequireAuthenticatedUser` (MA-210); 4× client `ThrowIfWriteFailedAsync` (MA-008/S0 root cause).
- StoryDesktop/StoryMobile + their test mirrors (see (c)).

**(e) False economies considered and rejected:**
- **Merging `StoryDetailsDTO` / `StoryUpdateDTO` / `StoryPropertiesViewModel`** — they look overlapping but are the deliberate DTO-firewall layering (read DTO vs wire-edit DTO vs UI ViewModel that shields server-only fields). Merging reintroduces the AuthorId/Slug/StoryId leakage the split prevents. Rejected.
- **Generic notification `CreateAsync` / generic per-context comment method** — the semantic-method + per-context pattern is settled (`layer2-services.md`); the lineage services correctly use semantic methods. Not a compression target.
- **Collapsing the ViewCount 4-piece (Buffer/Flusher/Worker/Service)** — that's the ratified signal-buffering shape (N≥2 body-swap seam); "simplifying" it reopens the loss-window contract. Rejected.
- **Series' per-series membership loop → one query** — the join-through-Stories-per-series exists to apply the viewer's ContentRating filter to Position/Count/Prev/Next; a single bare `SeriesEntry` projection is the exact mutation the settled WU41 test rejects. Rejected (documented small-N).
