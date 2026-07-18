# Slice 2 — Stories & Series (Features 4, 5, 8, 9, 10, 45)

Audited 2026-07-17 by the S2 slice agent. Read-only pass; no builds/tests run — all `verify: [pending]`.
Scope: Stories + Series clusters (Story Creation/Editing, Browsing/Display, Story Arcs, Series &
Ordering, Story Lineage, View Count Tracking). Tags/Characters/Pairings internals are S4's; the
Stories-side composition/mapping of `StoryPropertiesForm`/`CharacterEntry`/`PairingBuilder` is audited
here. EF configs (`StoryConfigurations.cs`) are S1's (MA-102/MA-118) — cited where relevant, not
re-audited.

**Overall read:** quality is very high — the write skeletons, signal buffering (ViewCount is textbook),
factory-per-method reads, dispatcher-reload discipline, and the whole test suite are exemplary and
convention-conformant. Findings are the exceptions, headed by one real security gap (unsanitized story
long-description) and a cluster-wide not-found-mechanism divergence.

## File inventory (path + LOC)

### Product — Core/Stories (measured)
| LOC | File |
|---|---|
| 74 | Story.cs |
| 152 | StoryMappers.cs |
| 68 | StoryValidations.cs |
| 28 | CreateStoryDTO.cs |
| 28 | StoryUpdateDTO.cs |
| 76 | StoryDetailsDTO.cs |
| 21 | StorySlug.cs |
| 30 | IEditableStoryProperties.cs |
| 108 | IStoryReadService.cs |
| 34 | IStoryWriteService.cs |
| 29 | StoryListing.cs / 27 StoryDetail.cs / 30 StoryListingDto.cs |
| 20 StoryTag.cs / 29 IStoryTag.cs / 9 StoryTagDTO.cs |
| 29 StoryCharacter.cs / 14 StoryCharacterDto.cs / 23 SettingDetail.cs / 13 SettingDetailDto.cs |
| 16 StoryCharacterPairing.cs / 13 StoryCharacterPairingDto.cs / 12 StoryCharacterPairingMember.cs |
| 1 | **StoryCharacterRelationship.cs** (tombstone comment file — MA-208) |
| 10 | **StoryListingPageDto.cs** (dead — MA-207) |
| 11 StoryValidationException.cs |
| 28 StoryLineage.cs / 20 StoryLineageType.cs / 16 StoryLineageDto.cs / 45 StoryLineageManageDto.cs / 40 CreateStoryLineageDto.cs / 13 StoryTitleSearchDto.cs / 9 StoryLineageTypeDto.cs / 11 StoryLineageValidationException.cs / 31 IStoryLineageReadService.cs / 50 IStoryLineageWriteService.cs |
| 31 StoryArc.cs / 26 StoryArcDtos.cs / 12 StoryArcValidationException.cs / 15 IStoryArcReadService.cs / 27 IStoryArcWriteService.cs |
| 33 StoryExternalLink.cs / 27 StoryExternalLinkDtos.cs / 28 ExternalPlatform.cs |
| 19 IViewCountWriteService.cs |

### Product — Core/Series
| LOC | File |
|---|---|
| 22 Series.cs / 14 SeriesEntry.cs / 17 SeriesConstants.cs / 12 SeriesValidationException.cs |
| 34 CreateSeriesDto.cs / 35 UpdateSeriesDto.cs / 17 SeriesListingDto.cs / 20 SeriesDetailDto.cs / 20 StorySeriesMembershipDto.cs |
| 31 ISeriesReadService.cs / 39 ISeriesWriteService.cs |

### Product — Server/Stories + Server/Series
| LOC | File |
|---|---|
| 498 | ServerStoryReadService.cs |
| 274 | ServerStoryWriteService.cs |
| 105 ServerStoryLineageReadService.cs / 148 ServerStoryLineageWriteService.cs |
| 26 ServerStoryArcReadService.cs / 129 ServerStoryArcWriteService.cs |
| 134 ServerSeriesReadService.cs / 182 ServerSeriesWriteService.cs |
| 66 ViewCountBuffer.cs / 84 ViewCountFlusher.cs / 44 ViewCountFlushWorker.cs / 17 ServerViewCountWriteService.cs |
| 151 StoryEndpoints.cs / 87 StoryLineageEndpoints.cs / 69 StoryArcEndpoints.cs / 34 ViewCountEndpoints.cs / 114 SeriesEndpoints.cs |

### Product — SharedUI/Stories + SharedUI/Series
| LOC | File |
|---|---|
| 236 StoryPage.razor / 228 StoryDesktop.razor / 221 StoryMobile.razor / 224 StoryCard.razor / 117 StoryDeck.razor |
| 465 StoryPropertiesForm.razor / 54 StoryPropertiesViewModel.cs / 322 StoryEditorPage.razor |
| 97 CharacterEntry.razor / 32 SettingEntry.razor / 112 PairingBuilder.razor |
| 204 StoryArcManagerPanel.razor / 50 StoryTitlePicker.razor / 30 StoryLineageBox.razor / 350 MyStoryLineagesPage.razor |
| 49 StoryViewStats.razor / 43 StoryDownloadLinks.razor / 31 StoryExternalLinksRow.razor |
| 180 SeriesPage.razor / 483 SeriesCreateEditPage.razor / 76 MySeriesPage.razor / 44 SeriesCard.razor / 48 SeriesMembershipBox.razor |

### Product — Client/Stories + Client/Series
| LOC | File |
|---|---|
| 89 ClientStoryReadService.cs / 71 ClientStoryWriteService.cs |
| 18 ClientStoryArcReadService.cs / 59 ClientStoryArcWriteService.cs |
| 24 ClientStoryLineageReadService.cs / 67 ClientStoryLineageWriteService.cs |
| 19 ClientViewCountWriteService.cs |
| 27 ClientSeriesReadService.cs / 81 ClientSeriesWriteService.cs |

**Product total ≈ 7,960 LOC.**

### Tests owned by this slice (all read in full)
| Tier | Files |
|---|---|
| Integration | StoryWriteServiceTests, StoryDetailTests, StoryListingsTests, StoryTaggingTests, RecentListingsTests, SeriesServiceTests, StoryArcServiceTests, StoryLineageServiceTests, ViewCountFlushTests |
| Unit | StoryValidationsTests, StoryMappersTests, StorySlugTests, SeriesValidationsTests, StoryLineageValidationsTests, ViewCountBufferTests |
| RazorComponents | StoryCardTests, StoryDeckTests, StoryDesktopTests, StoryMobileTests, StoryPropertiesFormTests, SeriesCardTests, SeriesMembershipBoxTests, SeriesCreateEditPageTests, StoryLineageBoxTests, StoryTitlePickerTests, StoryViewStatsTests, CharacterEntryTests, PairingBuilderTests |

**Test total ≈ 5,576 LOC.** Scope notes: no dedicated `StoryPageTests`, `StoryEditorPageTests`,
`StoryArcManagerPanelTests`, or `MyStoryLineagesPageTests` exist (StoryPage's lifecycle/PersistentState
handoff is manual-boot-gated per `audit/Stories.md` WU-ComponentSoundness; the lineage/arc pages map
single routes with no dispatcher-reuse trap — consistent with recorded precedent). `ChapterListSegmenterTests`/
`ChapterListTests` are S3's (Chapters) even though they render arc headers; the arc reader-side segmenter
lives in `SharedUI/Chapters/`.

---

### MA-201 | Tier 1 | Bucket A | Slice 2
claim: The story long-description (rich HTML authored in Quill/`EditorView`) is persisted **without sanitization** — `ServerStoryWriteService` injects no `IHtmlSanitizationService` and neither `CreateStoryAsync` nor `UpdateStoryAsync` sanitizes `LongDescription` before saving; it is then rendered via `RichTextView` (`MarkupString`, no sanitization by contract) — a stored-XSS gap on the site's most-viewed page. Every other `EditorView`-fed write path in the slice/codebase sanitizes on save.
evidence: `TheCanalaveLibrary.Server/Stories/ServerStoryWriteService.cs:6-14` — ctor deps `(readDbFactory, writeDb, activeUser, imageStorage, rateLimit, logger)` (no sanitizer); `:52-60` `CreateStoryAsync` persists `newStoryDTO.ToStory()` (which copies `LongDescription` verbatim, `StoryMappers.cs:91` `actualStory.StoryDetail.LongDescription = tempStory.LongDescription;`) with no sanitize call; `:100` `UpdateStoryAsync` calls `UpdateStoryEditableProperties(dto)` (same verbatim copy) then `SaveChangesAsync` — no sanitize. LongDescription IS EditorView output: `TheCanalaveLibrary.SharedUI/Stories/StoryPropertiesForm.razor:78-79` `<ContentSurface Variant="Input"><EditorView @ref="_editor" Html="@ViewModel.LongDescription" .../></ContentSurface>`; pulled raw at submit `:265-266` `await _editor.GetHtmlAsync()`. Rendered raw: `TheCanalaveLibrary.SharedUI/Stories/StoryDesktop.razor:137` / `StoryMobile.razor:130` `<RichTextView HtmlContent="@Story.LongDescription" />`. Contrast the sibling: `TheCanalaveLibrary.Server/Series/ServerSeriesWriteService.cs:17` injects `IHtmlSanitizationService sanitizer` and `:40,:70` `Description = ... sanitizer.Sanitize(dto.Description)`. Convention: `layer2-services.md` §"User HTML Is Sanitized Once, On Save — Never On Display" — "Any write path that accepts user-authored rich text (chapters, vouch text, comments, recommendations, blog posts, profile bios, messages — everywhere `EditorView` is used) runs it through `HtmlSanitizer`'s allow-list in the write service, before persisting."
cells: F4 L2 — **proposes reopen** (status.md F4 L2 = Stage 5); F4 L3-Logic touches the same service
effort: S | route: Stage-4 reconcile (inject `IHtmlSanitizationService`, sanitize `LongDescription` on create + update; add a regression test — no test currently pins this because no sanitization happens)
verify: [pending]

### MA-202 | Tier 2 | Bucket A | Slice 2
claim: No story/series dispatcher uses the sanctioned `NavigationManager.NotFound()` for a missing entity — four sites navigate to a literal `/not-found` route (200 + client redirect, not an HTTP 404) and one renders inline. Deleted-story / deleted-series URLs therefore return 200, which the SEO/crawler concern (F64) and `render-and-layout.md` both call out.
evidence: `TheCanalaveLibrary.SharedUI/Stories/StoryPage.razor:138` — `NavigationManager.NavigateTo("/not-found");` (also `:174`, the OnParametersSetAsync reload path); `TheCanalaveLibrary.SharedUI/Stories/StoryEditorPage.razor:116` — `NavigationManager.NavigateTo("/not-found");`; `TheCanalaveLibrary.SharedUI/Series/SeriesCreateEditPage.razor:277` — `Nav.NavigateTo("/not-found");`; `TheCanalaveLibrary.SharedUI/Series/SeriesPage.razor:24-27` — missing series renders `<p ...>Series not found.</p>` inline (no `NotFound()` at all). Convention: `render-and-layout.md` §"NavigationManager.NotFound() (.NET 10)" — "Use in page dispatchers when the requested entity doesn't exist ... Replaces manual navigation to error pages"; `layer3-logic.md` §"Page Dispatcher: Entity Not Found" — same. `IStoryReadService.GetStoryByIdAsync` returns null for a filtered/absent story, so this is the exact missing-entity branch the rule governs.
cells: F5 L3-Logic (StoryPage), F4 L3-Logic (StoryEditorPage), F9 L3-Logic (SeriesPage/SeriesCreateEditPage) — all Stage 5, **proposes reopen**
effort: S | route: mechanical sweep (swap all five to `Nav.NotFound()`; browser-verify a deleted-story URL returns 404)
verify: [pending]

### MA-203 | Tier 2 | Bucket A | Slice 2
claim: `StoryPage` loads seven awaits strictly sequentially (story → chapters → USI state → series memberships → lineage → arcs → watermark); the last six are mutually independent and each opens its own factory-per-method read context, so they are exactly the shape `layer2` sanctions `Task.WhenAll` for. Under WASM these are six serial HTTP round-trips on the load path of the heaviest page in the app; the sequential block is duplicated verbatim in both lifecycle methods.
evidence: `TheCanalaveLibrary.SharedUI/Stories/StoryPage.razor:132-150` (OnInitializedAsync) — `Story ??= await GetStoryByIdAsync(...)` then sequential `await GetChapterListAsync` (:142), `await GetStateAsync` (:145), `await GetMembershipsForStoryAsync` (:147), `await GetLineageForStoryAsync` (:148), `await GetArcsForStoryAsync` (:149), `await GetViewerLastInteractionUtcAsync` (:150); the same six-await chain repeats at `:178-183` (OnParametersSetAsync). Convention: `layer2-services.md` §"Read-Context Concurrency" — "pages themselves parallel-load via `Task.WhenAll` (ChapterReadingPage, SettingsPage, GroupPage, NotificationsPage) ... `Task.WhenAll` parallel loading in pages/components is *sanctioned* by this pattern — do not sequentialize awaits." StoryPage is absent from that parallelizing list despite being the densest loader.
cells: F5 L3-Logic (Stage 5) — proposes reopen
effort: M | route: Stage-4 reconcile (Task.WhenAll the six independent loads after `Story` resolves; extract a shared `LoadSupplementaryAsync` so both lifecycle points share it)
verify: [pending]

### MA-204 | Tier 2 | Bucket A | Slice 2
claim: `GetStoryByIdAsync` still projects `ChapterNames` (a correlated `Chapters.Select(c => c.Title)` collection subquery) on every story-detail read, but the field is documented-dead — its own DTO XML-doc says it's a legacy leftover superseded by `GetChapterListAsync` and "will be removed"; no UI reads it. Net: an extra collection join on the hottest read for nothing, plus a latent leak (it projects *all* chapters' titles, including unpublished drafts, with no `IsPublished`/rating gate).
evidence: `TheCanalaveLibrary.Server/Stories/ServerStoryReadService.cs:43` — `s.Chapters.Select(c => c.Title).ToList()` inside the `GetStoryByIdAsync` projection, mapped at `:96` `ChapterNames = row.ChapterNames`; `TheCanalaveLibrary.Core/Stories/StoryDetailsDTO.cs:53-58` — "`Legacy:` chapter title list from the original L5 JSON-endpoint design. The story landing page (WU25) uses `IChapterReadService.GetChapterListAsync` instead ... This field will be removed when an L5 endpoint is rebuilt (post-MVP)." Repo-wide grep for `ChapterNames`: only the definition, the projection, and coverage XML — zero UI consumers (`StoryDesktop`/`StoryMobile` render `Chapters` from `GetChapterListAsync`, not `Story.ChapterNames`).
cells: F5 L2 (Stage 5) — proposes reopen (remove the projection + field)
effort: S | route: mechanical sweep (drop `ChapterNames` from the DTO, the `StoryDetailRow` record, and the projection)
verify: [pending]

### MA-205 | Tier 2 | Bucket A | Slice 2
claim: Two edit pages hand-roll validation-error `<div role="alert"><ul>` blocks instead of using the `InlineAlert` atom, which `error-handling.md` names the ONLY channel for validation feedback — and which their own sibling forms (`StoryPropertiesForm`, `StoryArcManagerPanel`) correctly use. The two channels drift in markup/styling for identical purpose.
evidence: `TheCanalaveLibrary.SharedUI/Series/SeriesCreateEditPage.razor:39-49` — `@if (_errors.Count > 0) { <div class="rounded-lg bg-(--color-danger)/10 p-4 ..." role="alert"><ul ...>@foreach (string error in _errors) { <li>@error</li> }</ul></div> }` (`_errors` are `SeriesValidationException.Errors`, `:365`); `TheCanalaveLibrary.SharedUI/Stories/MyStoryLineagesPage.razor:108-118` — same hand-rolled block for `_createErrors` (`StoryLineageValidationException.Errors`, `:267`). Contrast: `TheCanalaveLibrary.SharedUI/Stories/StoryPropertiesForm.razor:13` `<InlineAlert Messages="@ViewModel.ServerValidationErrors" />` and `StoryArcManagerPanel.razor:21` `<InlineAlert Messages="_errorMessages"/>`. Convention: `error-handling.md` / calibration seam record — "`InlineAlert` — ... the ONLY channel for validation feedback." (Both pages also surface per-operation errors as bare `<p class="text-(--color-danger)">` — `SeriesCreateEditPage.razor:96`, `MyStoryLineagesPage.razor:39`.)
cells: F9 L3.5 (SeriesCreateEditPage) + F10 L3.5 (MyStoryLineagesPage), both Stage 5 — proposes reopen
effort: S | route: mechanical sweep (replace the hand-rolled alert blocks with `<InlineAlert Messages="...">`)
verify: [pending]

### MA-206 | Tier 2 | Bucket A | Slice 2
claim: `StoryPage`'s view-ping disposal swallows all exceptions in a bare `catch {}` with a prose-only comment — the same unregistered-silent-catch class `logging.md` forbids and that S0 flagged as MA-001/MA-002. A non-disconnection failure of the dispose JS call is hidden with no log and no registry entry.
evidence: `TheCanalaveLibrary.SharedUI/Stories/StoryPage.razor:225` — `try { await JS.InvokeVoidAsync("viewPing.dispose"); } catch { /* page already gone or no JS */ }` (bare `catch`, no `// sanctioned-silent:` annotation, not in the registry). `logging.md` §"No silent catches": "A silent catch is legal only when annotated at the catch site: `// sanctioned-silent:` ... Registry of sanctioned sites." (The sibling `catch (JSException)` at `:209` is typed + expected/commented — the register path — and is not flagged; the `:225` bare catch is.)
cells: F45 L3-Logic (StoryPage view-ping, Stage 5)
effort: S | route: mechanical sweep (narrow to `catch (JSDisconnectedException)`/`catch (JSException)`, or annotate + register)
verify: [pending]

### MA-207 | Tier 3 | Bucket C | Slice 2
claim: `StoryListingPageDto` is dead — a wire-shape record for `GetRecentListingsAsync` that the generic `PagedResult<T>` (minted in S1's Core/Http) fully superseded; both the server endpoint and the client impl use `PagedResult<StoryListingDto>`, and nothing references `StoryListingPageDto`.
evidence: `TheCanalaveLibrary.Core/Stories/StoryListingPageDto.cs:10` — `public record StoryListingPageDto(StoryListingDto[] Items, int TotalCount);` (its own doc calls it "the Client HTTP impl's JSON shape"); repo-wide grep: only the definition + coverage XML, zero usages. Actual shape in use: `TheCanalaveLibrary.Server/Stories/StoryEndpoints.cs:64` `new PagedResult<StoryListingDto>(...)` and `TheCanalaveLibrary.Client/Stories/ClientStoryReadService.cs:34` `GetFromJsonAsync<PagedResult<StoryListingDto>>(...)`.
cells: F5-area Core (dead type — no cell change)
effort: S | route: mechanical sweep (delete the file)
verify: [pending]

### MA-208 | Tier 3 | Bucket C | Slice 2
claim: `StoryCharacterRelationship.cs` is a one-line tombstone-comment file left after the WU37 rename to `StoryCharacterPairing` — dead residue (SDK-style projects don't need it; it invites grep confusion, which is exactly the collision WU37/WU42 renames existed to remove).
evidence: `TheCanalaveLibrary.Core/Stories/StoryCharacterRelationship.cs:1` (whole file) — `// Class renamed to StoryCharacterPairing in WU37 — see StoryCharacterPairing.cs.`; grep for `StoryCharacterRelationship` in product/test code: zero (only this file, spec, and excluded GeminiDiscussions).
cells: F12-area Core organization (no cell change)
effort: S | route: mechanical sweep (delete the file)
verify: [pending]

### MA-209 | Tier 3 | Bucket C | Slice 2
claim: The K/M word-count formatter and the status/rating label switch tables are copy-pasted verbatim across three components (`StoryCard`, `StoryDesktop`, `StoryMobile`) — `StatusBadges.ForStatus/ForRating` already centralizes the badge *class*, but the parallel `StatusLabel`/`RatingLabel`/`WordCountDisplay` display logic was not, so it lives in triplicate (and the shared 999,999→"1000K words" edge quirk is baked into all three).
evidence: `TheCanalaveLibrary.SharedUI/Stories/StoryCard.razor:163-194`, `StoryDesktop.razor:196-227`, `StoryMobile.razor:189-220` — identical `WordCountDisplay` (`< 1_000 => "$… words"`), `StatusLabel` (9-arm switch), `RatingLabel` (E/T/M switch) blocks. The edge quirk is pinned as expected in `StoryCardTests.cs:163` `[InlineData(999_999, "1000K words")]`. Contrast the already-shared half: all three call `StatusBadges.ForStatus(...)`/`.ForRating(...)` for the class string.
cells: F5 L3-Logic (display logic; no cell change)
effort: S | route: seam — direction undetermined (extract a `StoryDisplayFormat` static, parallel to `StatusBadges`; fixes the edge quirk once) — Brian decides whether the triplication is worth collapsing
verify: [pending]

### MA-210 | Tier 3 | Bucket C | Slice 2
claim: The `RequireAuthenticatedUser()` guard (identical 3-line `UserId is not int → throw InvalidOperationException`) is re-implemented in every write service of the cluster instead of a shared helper — a small mechanical repetition with a fixable root cause.
evidence: `TheCanalaveLibrary.Server/Series/ServerSeriesWriteService.cs:170-175`; `TheCanalaveLibrary.Server/Stories/ServerStoryArcWriteService.cs:123-128` (private); `TheCanalaveLibrary.Server/Stories/ServerStoryLineageReadService.cs:99-104` (protected, shared with its write service); inline equivalent in `ServerStoryWriteService.cs:18-19`. Four copies of the same guard.
cells: F8/F9/F10 L2 (no cell change)
effort: S | route: seam — direction undetermined (an `IActiveUserContext.RequireUserId()` extension in Core would collapse all four)
verify: [pending]

### MA-211 | Tier 3 | Bucket C | Slice 2
claim: `ServerStoryArcWriteService` copies its primary-constructor params into `private readonly` fields (`_writeDb`, `_activeUser`) and uses those, whereas every other write service in the slice (`ServerStoryLineageWriteService`, `ServerSeriesWriteService`) uses the primary-ctor parameters directly. Both compile; it's an intra-slice idiom divergence.
evidence: `TheCanalaveLibrary.Server/Stories/ServerStoryArcWriteService.cs:20-22` — `private readonly ApplicationDbContext _writeDb = writeDb; ... private readonly IActiveUserContext _activeUser = activeUser;` vs `ServerStoryLineageWriteService.cs:29,45` (uses `writeDb`/`activeUser` param names directly) and `ServerSeriesWriteService.cs:29,44` (same).
cells: F8 L2 (cosmetic)
effort: S | route: mechanical sweep (drop the field copies, use the params)
verify: [pending]

### MA-212 | Tier 3 | Bucket A | Slice 2
claim: `StoryPropertiesForm` wraps `EditorView` but its submit button and "+ Add link" button carry no `aria-label`, against `testing.md`'s rule that every button in an `EditorView`-wrapping component must have a unique aria-label to stay selectable past Quill's toolbar buttons. Impact is currently nil (the test selects the submit via `button[type='submit']`), but the rule is stated as MUST and the "+ Add link" button is text-only.
evidence: `TheCanalaveLibrary.SharedUI/Stories/StoryPropertiesForm.razor:78-79` embeds `<EditorView>`; `:214-218` submit `<button type="submit" ...>` (no aria-label); `:196-197` `<button ...>+ Add link</button>` (no aria-label). The remove-link button `:192` DOES carry `aria-label="Remove link"`. Convention: `testing.md` §"BlazoredTextEditor button collision" — "any button on a component that wraps `EditorView` MUST carry a unique `aria-label`."
cells: F4 L4.5-area / test-friction (no cell change)
effort: S | route: mechanical sweep (add aria-labels)
verify: [pending]

### MA-213 | Tier 3 | Bucket C | Slice 2
claim: `StoryListingsTests` carries a stale comment claiming it uses the DataSeeder's `TestUser` ("only user guaranteed to exist"), but the test actually seeds and uses its own `_testUserId` via `SeedUserAsync` — the exact never-query-seeded-names discipline the comment contradicts.
evidence: `TheCanalaveLibrary.Tests.Integration/StoryListingsTests.cs:114` — "`// Use the DataSeeder's TestUser — only user guaranteed to exist in the shared container.`" vs `:39` `_testUserId = await SeedUserAsync();` and `:238-240` `SetViewer` sets `_fake.UserId = _testUserId`. The seeded-own-user approach is correct per `testing.md`; only the comment is wrong.
cells: F5 test quality (no cell change)
effort: S | route: mechanical sweep (delete/fix the comment)
verify: [pending]

---

## Hypothesis results (slice 2)

- **H-01** (`@key` on stateful list children): **clean** — `StoryDeck.razor:33` keys `<CanalaveErrorBoundary @key="story.StoryId">` wrapping the stateful `StoryCard`→`UserStoryInteractionPanel` (the WU-ComponentSoundness F2 fix, regression-pinned in `StoryDeckTests.KeyedList_WhenStorySwapped_...`); `StoryArcManagerPanel.razor:34` keys its `@bind`-holding `ArcRowModel` rows `@key="row"`; `StoryPropertiesForm.razor:89,119` key the init-only-seeded `TagSelector` loops. `StoryViewStats` holds per-slot state but self-heals via `OnParametersSet` StoryId reset (`:31-41`) and is keyed by StoryCard above it.
- **H-02** (route-param reload discipline): **clean** — `StoryPage` (`:104-105,157-189`), `SeriesPage` (`:104-105,141-169`), `SeriesCreateEditPage` (`:220-259`, incl. the `_loadedForCreate` sentinel for the two-route reuse), and `StoryEditorPage` all implement the guarded sentinel + `_initialized` reload with **plain-assign (not `??=`) on reload** for `[PersistentState]` fields. `MySeriesPage`/`MyStoryLineagesPage` map single routes (no reuse trap). `StoryArcManagerPanel`'s post-write `ReloadArcsAsync` plain-assigns.
- **H-03** (unnamed `HasIndex` overwrite): **n/a** — EF configs are S1's (`StoryConfigurations.cs`); no `HasIndex` in slice product code.
- **H-04** (read-context factory-per-method): **clean** — every read service (`ServerStoryReadService`, `ServerSeriesReadService`, `ServerStoryLineageReadService`, `ServerStoryArcReadService`) creates `await using ... readDbFactory.CreateDbContextAsync()` per method; write services hold only `writeDb`; base services expose `protected ReadDbFactory`/`ActiveUser` per the CS9107 idiom. No shared read-context field anywhere in slice.
- **H-05** (dead Tailwind classes): **clean** — paren-form tokens throughout; no v3 bracket-form, no raw palette/hex in slice. `StoryCard`/`StoryViewStats` use bare-name semantic utilities (`bg-surface`, `text-text`, `hover:bg-surface-hover`) alongside paren-form — but bare-name semantic tokens are codebase-wide (108 occurrences across 26 files, incl. the convention docs' own `Card.razor`/`ConfirmDialog` examples) and pass CI, i.e. a sanctioned dual style, not dead classes. Not filed.
- **H-06** (unregistered silent catches): **MA-206** — `StoryPage.razor:225` bare `catch {}` on the view-ping dispose JS call. Every other catch in slice logs (write-service notify `LogError`; cover-delete `LogWarning`) or is a typed-expected `catch (JSException)` with a comment.
- **H-07** (stale/untracked TODO comments): **clean** — no `TODO`/`HACK`/`FIXME` in slice; the closest is `StoryDetailsDTO.ChapterNames`'s "will be removed" legacy note, filed as **MA-204** (dead code, not a TODO).
- **H-08** (`Nav.NotFound()` vs manual `/not-found`): **MA-202** — StoryPage (×2), StoryEditorPage, SeriesCreateEditPage all `NavigateTo("/not-found")`; SeriesPage renders inline. Resolves the S0 "MA-candidate: StoryPage" flag — confirmed real, and slice-wide (five sites, zero uses of `NotFound()`).
- **H-09** (dispatcher load parallelism): **MA-203** — StoryPage runs six independent awaits sequentially in both lifecycle methods; the layer2-sanctioned `Task.WhenAll` (used by ChapterReadingPage/SettingsPage/GroupPage/NotificationsPage) is not applied. Resolves the S0 "MA-candidate: StoryPage" flag. (SeriesPage/MyStoryLineagesPage also load sequentially but with fewer/dependent awaits — StoryPage is the material case.)
- **H-10** (debounced/pending writes lost on dispose): **n/a** — the slice's write signal (`ViewCount`) has no per-component debounce: `ServerViewCountWriteService.RecordViewAsync` writes to the singleton buffer synchronously (`ViewCountBuffer.Record`, O(1)); the `ViewCountFlushWorker` drains globally and drains once more after cancellation. `StoryPage.DisposeAsync` only tears down the JS ping ref — no pending write to lose. The USI-panel 2s debounce is S4's (`UserStoryInteractionPanel`); this slice's `StoryCard`/`StoryDesktop` merely compose it.
- **H-11** (doc-vs-code staleness): **clean for new instances** — no fresh doc contradictions surfaced in slice. Re-confirms S1's **MA-118** in-code: `StoryLineage.RelationshipTypeId`/nav `RelationshipType` (`StoryLineage.cs:17,23`) and `StoryLineageType.RelationshipTypeId` still carry "Relationship" despite the WU42 rename, visible in `ServerStoryLineageReadService.cs:32,63,93` — cross-referenced, not re-raised.
- **H-12** (fire-and-forget without observation): **clean** — no `_ = SomeAsync(...)` launches in slice; `ClientViewCountWriteService.RecordViewAsync` awaits its `PostAsync` (fire-and-forget-by-contract at the caller, but the HTTP call itself is awaited); `StoryPage.OnViewPing` is `[JSInvokable]` awaited.
- **H-13** (denormalized counter discipline): **clean** — the one counter mutation (`ServerStoryWriteService.cs:63-64`, `StoriesWritten`) uses atomic `ExecuteUpdateAsync(SetProperty(us => us.StoriesWritten + 1))` — no tracked `++`. Missing-row would no-op (UserStat rows are created at registration); an explicit no-op comment is absent but not required.
- **H-14** (elevated reads annotated + named): **clean** — `GetStoryIdsByAuthorAsync` (`ServerStoryReadService.cs:222`) `.IgnoreQueryFilters(["ContentRating"]) // elevated read: author always sees their own stories`; `GetManageDataForUserAsync` (`ServerStoryLineageReadService.cs:50-53,68-72`) `.IgnoreQueryFilters(["ContentRating","IsTakenDown"])` with a named-filter list + "Elevated, owner-scoped read" comment. Both named + annotated + justified.
- **H-15** (write-path by-id lookups bypass ContentRating): **n/a/clean by construction** — every write-service existence check reads through the **unfiltered** `writeDb` (`ServerStoryLineageWriteService.cs:29,35,94`; `ServerSeriesWriteService.cs:29,54,99`; `ServerStoryArcWriteService.cs:29`; `ServerStoryWriteService.UploadCoverArtAsync:156`), which carries no named filters — so the phantom-`KeyNotFound` class can't occur; no readDb PK fetch in a write path.
- **H-16** (`[FromQuery]` on non-GET arrays): **clean** — `StoryEndpoints.cs:76-77` (`/query`) and `:90-91` (`/filter-candidates`) both carry explicit `[FromQuery] int[]` alongside a body-bound `StoryFilterDto`, with a comment documenting the startup-crash trap; `SeriesEndpoints.cs:97-98` reorder uses `[FromBody] List<int>`. Correctly handled.
- **H-17** (nullable client reads use tolerant helpers): **clean** — `ClientStoryReadService.GetStoryByIdAsync`/`GetStoryForEditAsync` (`:18-22`) and `ClientSeriesReadService.GetSeriesByIdAsync` (`:18-19`) use `GetNullableFromJsonAsync`; non-nullable list reads use `GetFromJsonAsync<List<T>> ?? []`. The server side returns `Results.Json(nullable)` (`StoryEndpoints.cs:47-48`, `SeriesEndpoints.cs:42-43`), which the tolerant helpers cover.
- **H-18** (aria-labels on icon-only + EditorView-adjacent buttons): **mostly clean; MA-212** — icon-only controls carry labels (StoryCard caret `aria-label="Story options"`, SeriesCreateEditPage move-up/down, StoryPropertiesForm platform select + remove-link, StoryExternalLinksRow verified ✓). The gap is `StoryPropertiesForm`'s submit + "+ Add link" buttons lacking aria-labels while the form wraps `EditorView` (MA-212).
- **H-19** (AuthorizeView-gated DI wrapper/inner split): **clean** — the DI-consuming edit surfaces (`StoryEditorPage`, `SeriesCreateEditPage`, `MyStoryLineagesPage`) are page-level `@attribute [Authorize]`, so the router never constructs them for anonymous viewers (the NotificationBell anonymous-construction crash class can't fire); their inner `<AuthorizeView>` is belt-and-suspenders. No auth-gated DI-consuming *leaf* in slice.
- **H-20** (feedback-channel discipline): **MA-205** — `SeriesCreateEditPage`/`MyStoryLineagesPage` hand-roll validation `<div role="alert"><ul>` blocks instead of `InlineAlert` (which `StoryPropertiesForm`/`StoryArcManagerPanel` use correctly). Catches surface through `ExceptionPresenter` (`StoryEditorPage.razor:286-291`); no raw `ex.Message` in UI; toasts used only for delete/remove *operation* failures (`SeriesCreateEditPage.razor:480`, borderline-but-not-validation).
