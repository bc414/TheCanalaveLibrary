# Slice 7 — Patterns Inventory (Publishing & Moderation)

1. **Pagination**
mechanism: offset (`page`/`pageSize`) → `PagedResult<T>` envelope at the HTTP boundary only; interface keeps a `(Items,TotalCount)` tuple. Blog listings only; moderation/spotlight/polls return unpaged bounded lists (queue, active placements, site polls).
exemplar: `ClientBlogPostReadService.cs:24-27` (`PagedResult<BlogPostListingDto>` unwrap).
deviations: none observed — mod queue/spotlight are deliberately unpaged (bounded sets).

2. **DTO mapping**
mechanism: two-step for polymorphic/composed reads (materialize rows → batch-load per kind → stitch); direct `.Select(new Dto(...))` for flat reads; `record` DTOs throughout; `(Items,TotalCount)` ValueTuples for paged returns.
exemplar: `ServerModerationReadService.BatchLoadTargetsAsync:155-260` (one query per `ReportedEntityType`); `ServerSpotlightReadService.GetActiveSpotlightsAsync:36-75` (compose via `IStoryReadService`+`IRecommendationReadService`).
deviations: `SpotlightSlotDto` vs `SpotlightSlotAdminDto` near-dup (viewer vs admin projection — deliberate audience split).

3. **Error surfacing**
mechanism: two co-existing patterns. Correct: `ExceptionPresenter.GetUserMessage`/typed `when` filter → `InlineAlert`. Incorrect: blanket `catch (Exception)` → raw `ex.Message` → hand-rolled `text-danger <p>`.
exemplar (correct): `PollView.razor:330,162`; `SpotlightRedemptionPage.razor:291-293,176`. (incorrect): `ModReportsPage.razor:194,124`.
deviations: **MA-703** (mod pages + BlogPostPage raw ex.Message), **MA-704** (ReportDialog unlogged catch-all).

4. **Form patterns**
mechanism: `EditForm`+ViewModel with `DataAnnotationsValidator` for blog; plain `@code`+@bind state for polls/mod/spotlight; the sanctioned explicit-string `<select>` idiom for bool (`value="single|multiple"` + `@onchange`), enum `<select>` via `@bind` to typed options.
exemplar: `PollEditorForm.razor:81-87` (bool-select fix, browser-found 2026-07-12); `BlogPostPropertiesForm.razor:16-18` (EditForm+VM).
deviations: none unsound — poll config uses raw `@code` (presentational, page owns I/O).

5. **Flyout/overlay mechanics**
mechanism: fixed-inset backdrop + panel with `@onclick` backdrop-dismiss + `@onclick:stopPropagation`; `z-(--z-modal)` token; `ConfirmDialog` reused for destructive poll delete; mod action panels are inline `@if` sections (not modals).
exemplar: `ReportDialog.razor:16-17` (backdrop/panel + stopPropagation); `PollView.razor:198-204` (ConfirmDialog delete).
deviations: none — consistent modal-shell reuse.

6. **Optimistic updates & debounce**
mechanism: optimistic-then-reconcile/rollback for like + poll vote (no timer/debounce); server returns the authoritative DTO which replaces local state.
exemplar: `BlogPostPage.HandleLikeAsync:246-276` (optimistic +/-, rollback in catch); `PollView.ApplyUpdatedAsync:311-317`.
deviations: none — no per-component debounce anywhere in slice (H-10 n/a).

7. **Disposal & lifecycle**
mechanism: pages use `[PersistentState]` + guarded dispatcher; workers are `BackgroundService`+`PeriodicTimer` with drain-after-cancel + catch-and-continue; no IDisposable needed (no subscriptions/CTS/JS in slice components).
exemplar: `BlogPostPage:150-151,202-210` ([PersistentState] + OnParametersSetAsync guard); worker split `SpotlightGoLive{Sweeper,Worker}`, `PollEditNotification{Sweeper,Worker}`, `SiteDailyStat{Aggregator,Worker}` (testable body + hosted shell).
deviations: none.

8. **Query shape**
mechanism: factory-per-method reads; TPT filtered via `Where(p is TChild)` never `OfType` into a shared base-typed projection; composition over duplication (spotlight joins `IStoryReadService`); mod queue = 2-pass batch (never N+1); join-through-filtered-DbSet for visibility.
exemplar: `ServerPollReadService.cs:20-40` (TPT `is`-filter + documented coercion trap); `ServerSpotlightReadService.cs:44-45` (filtered-DbSet join).
deviations: none — TPT projection safety verified clean.

9. **Write-method skeleton**
mechanism: auth-guard (`ActiveUser.UserId is not int` → InvalidOp) → role/owner gate → `rateLimit.EnsureAllowed` → `dto.CanSave()`/validate → existence check on unfiltered writeDb → `sanitizer.Sanitize` (blog) → `SaveChangesAsync` → atomic counter `ExecuteUpdateAsync` → best-effort post-commit notify (try/catch LogWarning).
exemplar: `ServerBlogPostWriteService.CreateProfileBlogPostAsync:31-67`.
deviations: **MA-705** (like-counter absolute not atomic-delta); moderation role gate throws InvalidOp not UnauthorizedAccess (**MA-701**).

10. **Endpoint & client shape**
mechanism: thin pass-through endpoints wrapping `EndpointHelpers.ExecuteWriteAsync` (exception→status: ValidationException→400, UnauthorizedAccess→403, KeyNotFound→404, RateLimit→429, InvalidOp→401); client impls uniform — `GetNullableFromJsonAsync` nullable reads, `ThrowIfWriteFailedAsync`+`ClientHttpHelpers.ReadProblemDetailAsync` writes.
exemplar: `EndpointHelpers.cs:14-72`; `ClientSpotlightWriteService.cs:34-52`.
deviations: **MA-702** (mod-write endpoints lack edge role gate that sibling mod endpoints carry). Client collapses 401/403 → one `UnauthorizedAccessException` (masks MA-701 client-side but not on the wire).

11. **Sanitization & derived fields**
mechanism: `sanitizer.Sanitize(dto.Content)` before persist on every blog write path; poll/report text is plain (Razor auto-encoded, no sanitize needed); `SiteDailyStatAggregator` derives counters via parameterized raw-SQL scalar subqueries (idempotent upsert).
exemplar: `ServerBlogPostWriteService.cs:40,88,203`; `SiteDailyStatAggregator.cs:37-100`.
deviations: none — MA-201 class does not recur (blog sanitizes; polls are plain text).

12. **Notification triggering**
mechanism: best-effort post-commit try/catch (LogWarning/LogError, never rolls back primary); semantic per-event methods (`NotifyReportReceived/Resolved/ContentRemoved/StoryApproved/Rejected/Account*/SpotlightSlotGranted`); go-live notifications deferred to worker sweep (never at booking).
exemplar: `ServerModerationWriteService.ResolveWithRemovalAsync:150-160`; `ServerSpotlightSlotAllocator.cs:54-62`.
deviations: **MA-709** (profile-blog follower-notify unbuilt TODO; group path does fan out).

13. **Counter updates**
mechanism: atomic `ExecuteUpdateAsync(SetProperty(x => x.col, x => x.col + delta))` for report counts and UserStats; set-based, no tracked `++`.
exemplar: `ServerModerationWriteService.AdjustActiveReportCountAsync:327-360`.
deviations: **MA-705** — blog `LikeCount` uses a C#-computed absolute value (lost-update window).

14. **Test idioms**
mechanism: Integration (Testcontainers-Postgres, per-test seed via base helpers, second user seeded for authz gates); non-mod-rejection + owner-rejection tests present; advisory-lock two-racers concurrency test; upsert-recomputes-not-accumulates test; aria-label/absent-when-empty RazorComponents.
exemplar: SpotlightServiceTests (grant cap/roles/non-mod/two-racers), ModerationServiceTests (resolve/approve/AdjustActiveReportCount no-op), PollServiceTests (TPT list, config-lock, non-owner gates), SiteDailyStatAggregatorTests (boundary + recompute).
deviations: none observed — strong authz regression coverage (53 authz-related hits across the 4 write-service test files).

15. **Code economy** (fixed feature set)
**(a) Per-cluster product LOC (product only, excl. tests):**
| Cluster | Core | Server | SharedUI | Client | Total |
|---|---|---|---|---|---|
| BlogPosts (blog+polls, 2 features) | 853 | 1184 | 1621 | 230 | **3888** |
| Moderation (report+queue+approval+stat) | 352 | 1188 | 1088 | 154 | **2782** |
| Spotlight | 392 | 680 | 634 | 141 | **1847** |
| SiteSettings | 97 | 124 | — | 62 | **283** |

Pattern-tax (endpoints + client-impl + DTO boilerplate) is the largest share in BlogPosts because it carries **two** full CQRS stacks (blog + poll), each with the TPT root+2-child machinery.

**(b) Compression candidates:**
- *Moderation 5-parallel-switches-over-`ReportedEntityType`* (`LoadModeratableAsync`, `AdjustActiveReportCountAsync`, `ApplyRemovalAsync`, `ApplyHardDeleteAsync`, `BatchLoadTargetsAsync` each switch the same 6-value enum). Saved ≈40-60 LOC / 5 sites collapsed; cost: a per-type strategy/registry abstraction the reader must learn, and each switch body does genuinely different work (load vs count vs delete vs label). Classify: **trade** (Brian decides).
- *Mod-page error-state boilerplate* (`_actionError`/`_rejectError`/`_grantError` + hand-rolled `<p>` repeated 4×) — root cause is **MA-703**; fixing it (InlineAlert) removes ~4 near-identical markup blocks. Classify: **pure win** (rides the MA-703 fix).

**(c) Near-identical pairs / near-dup DTOs:** Desktop/Mobile duplication is **below the codebase norm** — BlogPostPage is a single centered layout (no Desktop/Mobile split); mod pages are desktop-only single-layout by convention. Near-dup DTO: `SpotlightSlotDto`/`SpotlightSlotAdminDto` (deliberate viewer-vs-admin). `CreateProfileBlogPostDto`/`CreateGroupBlogPostDto` differ only by `GroupId` — but their write paths differ (membership vs none) so the DTO split mirrors real divergence.

**(d) Mechanical repetition w/ fixable root cause:** MA-703 (4× error boilerplate), MA-706 (DI-shape one-off). Both cited.

**(e) False economies considered & rejected:** (1) *Per-context blog/poll create methods* (`CreateProfileBlogPost` vs `CreateGroupBlogPost`; `CreateSitePoll` vs `CreateBlogPostPoll`) — DRY-ing into one generic with a context enum would obscure the divergent authorization (owner vs group-membership vs mod) and the per-context existence check; per layer2 "Per-Context Method Pattern," disciplined repetition, keep separate. (2) *SitePoll vs BlogPostPoll TPT split* — the shadow-FK diamond fix (documented WU-Polls L1 reconcile) proves the NOT-NULL child-FK guarantee is load-bearing; collapsing to one table with a nullable BlogPostId would reintroduce the exact bug fixed. Keep.

**Outlier note (last slice):** **BlogPosts is the slice's — and a codebase — code-economy outlier**: the heaviest single cluster in S7 (3888 LOC product), carrying two complete TPT-rooted CQRS features (blog + polls) in one folder. The weight is *structurally justified* (dual features, real TPT guarantees, config-lock/sweep machinery) rather than accidental — a "trade," not a false economy. Moderation Server-side is dense but justified by the polymorphic-report-target model (the 5 parallel enum switches). Spotlight and SiteSettings are lean and at-norm.
