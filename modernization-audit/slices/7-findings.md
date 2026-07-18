# Slice 7 — Publishing & Moderation — Findings

Clusters: **BlogPosts** (F35/36 blog, F37 polls, F56 feature-contributions), **Moderation**
(F46 report, F47 queue/actions, F48 approval, F53 external-link verify, F62 SiteDailyStat),
**Spotlight** (F55), **SiteSettings** (cross-cutting service logic + MA-123 divergence).
Read-only audit; no builds/tests run.

## Inventory (path + LOC; non-migration product + tests)

### Core
| File | LOC |
|---|---|
| Core/BlogPosts/ (16 files: BaseBlogPost→{Profile,Group}BlogPost, BasePoll→{Site,BlogPost}Poll, PollOption/PollVote/BlogPostLike, DTOs, IBlogPost/IPoll Read+Write, BlogPostValidations, PollRules) | 853 |
| Core/Moderation/ (Report, ReportReason, ReportStatus, IModeratableContent, IModeration Read/Write, ModerationDtos, SiteDailyStat(+Dto+read iface)) | 352 |
| Core/Spotlight/ (SpotlightSlot, CommunitySpotlight, SpotlightEnums, SpotlightBlocks, ISpotlight Read/Write/SlotAllocator, 7 DTOs, ValidationException) | 392 |
| Core/SiteSettings/ (SiteSetting, SiteSettingKeys, ISiteSettings Read/Write) | 97 |

### Server
| File | LOC |
|---|---|
| Server/BlogPosts/ (ServerBlogPost{Read,Write}Service, ServerPoll{Read,Write}Service, PollEditNotification{Sweeper,Worker}, BlogPostEndpoints, PollEndpoints) | 1184 |
| Server/Moderation/ (ServerModeration{Read,Write}Service, SiteDailyStat{Aggregator,Worker}, ServerSiteDailyStatReadService, ModerationEndpoints, SiteDailyStatEndpoints) | 1188 |
| Server/Spotlight/ (ServerSpotlight{Read,Write}Service, ServerSpotlightSlotAllocator, SpotlightGoLive{Sweeper,Worker}, SpotlightEndpoints, SpotlightSlotAllocatorEndpoints) | 680 |
| Server/SiteSettings/ (ServerSiteSettings{Read,Write}Service, SiteSettingsEndpoints) | 124 |

### SharedUI
| File | LOC |
|---|---|
| SharedUI/BlogPosts/ (BlogPostPage, BlogPostEditorPage, BlogPostCard, BlogPostPropertiesForm(+VM), PollView, PollEditorForm, PollsPage) | 1621 |
| SharedUI/Moderation/ (ModReportsPage, ModUsersPage, ModSubmissionsPage, ModStatsPage, ReportDialog, DailyStatLineChart, StatTile, ActivityRow) | 1088 |
| SharedUI/Spotlight/ (CommunitySpotlightDisplay, SpotlightRedemptionPage, ModSpotlightPage) | 634 |

### Client
| File | LOC |
|---|---|
| Client/BlogPosts/ (ClientBlogPost{Read,Write}Service, ClientPoll{Read,Write}Service) | 230 |
| Client/Moderation/ (ClientModeration{Read,Write}Service, ClientSiteDailyStatReadService) | 154 |
| Client/Spotlight/ (ClientSpotlight{Read,Write}Service, ClientSpotlightSlotAllocator) | 141 |
| Client/SiteSettings/ (ClientSiteSettings{Read,Write}Service) | 62 |

### Tests (slice-relevant)
Integration: BlogPostWriteServiceTests, ModerationServiceTests, SpotlightServiceTests,
PollServiceTests, SiteDailyStatAggregatorTests, AccountStatusEnforcementTests. Unit:
BlogPostValidationsTests, ModerationValidationsTests, SpotlightBlocksTests, PollRulesTests,
PollEditDtoTests, SiteDailyStatWorkerTests. RazorComponents: BlogPostPropertiesFormTests,
CommunitySpotlightDisplayTests, SpotlightRedemptionPageTests, ModSpotlightPageTests,
AccountStatusBannerTests, FakeModerationWriteService.

---

## ★ HEADLINE: the systemic L5 endpoint authorization-deferral class DOES NOT RECUR in this slice.

Unlike MA-301/601/602 (Chapters/Badges/UserProfile), **every** moderation, spotlight, poll, and
site-settings **mutation gates in its backing service, server-side, on `IActiveUserContext` — no
client-supplied `userId`/`moderatorId`/role/flag is trusted.** The endpoint authorization table:

| Endpoint | Method | Endpoint auth | Backing-service gate | Trusts client id? |
|---|---|---|---|---|
| `/api/moderation/report-reasons` | GET | `RequireAuthorization()` | none (public reason list) | N/A |
| `/api/moderation/reports` | GET | `RequireAuthorization(ModeratorOnly)` | none (edge-gated) | N |
| `/api/moderation/submissions` | GET | `RequireAuthorization(ModeratorOnly)` | none (edge-gated) | N |
| `/api/moderation/reports` (submit) | POST | `RequireAuthorization()` | allow-set + rate-limit; reporter = `activeUser.UserId` | N |
| `/api/moderation/reports/{id}/claim` | POST | `RequireAuthorization()` | **`RequireModerator()`**; modId = activeUser | N |
| `…/resolve-no-action` | POST | `RequireAuthorization()` | **`RequireModerator()`** | N |
| `…/resolve-removal` | POST | `RequireAuthorization()` | **`RequireModerator()`** | N |
| `…/account-action` | POST | `RequireAuthorization()` | **`RequireModerator()`**; **target from `report.ReportedEntityId`**, not client | N |
| `/submissions/{id}/approve` | POST | `RequireAuthorization()` | **`RequireModerator()`** | N |
| `/submissions/{id}/reject` | POST | `RequireAuthorization()` | **`RequireModerator()`** | N |
| `/api/spotlight/active` | GET | anonymous | join-through-filtered-DbSet | N/A |
| `/api/spotlight/my-*`, `/blocks` | GET | `RequireAuthorization()` | scoped to `activeUser.UserId` | N |
| `/api/spotlight/redeem` | POST | `RequireAuthorization()` | userId=activeUser; **`slot.GrantedToUserId != userId` throws**; story author≠sponsor | N |
| `/api/spotlight-slots/` (grant) | POST | `RequireAuthorization(ModeratorOnly)` | **`RequireModerator()`**; granter=activeUser; `toUserId`=grant target (legit) | N |
| `/api/spotlight-slots/{id}` (revoke) | DELETE | `RequireAuthorization(ModeratorOnly)` | **`RequireModerator()`** | N |
| `/api/spotlight-slots/remaining-capacity`,`/recent-grants` | GET | `RequireAuthorization(ModeratorOnly)` | `RequireModerator()` on recent-grants | N |
| `/api/blog-posts/{id}` , `/by-author` , `/by-group` | GET | public | rating/draft projection | N/A |
| `/api/blog-posts/{id}/edit` | GET | `RequireAuthorization()` | UX pre-check; write gates on save | N |
| `/api/blog-posts/` create, `/{id}` update/delete, `/like` | POST/PUT/DELETE | `RequireAuthorization()` | **`AuthorId == activeUser.UserId`** (owner) | N |
| `/api/blog-posts/group` create | POST | `RequireAuthorization()` | **group-membership check** | N |
| `/api/polls/` , `/by-blog-post` , `/{id}` | GET | public | tallies/names blanked server-side | N/A |
| `/api/polls/site` create, `/{id}/archive` | POST | `RequireAuthorization()` | **`IsModerator||IsAdmin`** | N |
| `/api/polls/blog-post/{id}` create | POST | `RequireAuthorization()` | **owner (`post.AuthorId==userId`)** | N |
| `/api/polls/{id}` update/close/delete | PUT/POST/DELETE | `RequireAuthorization()` | **`LoadAuthorizedPollWithOptionsAsync`** (mod for SitePoll, owner for BlogPostPoll) | N |
| `/api/polls/{id}/vote` | POST | `RequireAuthorization()` | userId=activeUser | N |
| `/api/site-daily-stats/latest`,`/series` | GET | `RequireAuthorization(ModeratorOnly)` | none (edge-gated) | N |
| `/api/site-settings/*` (write) | POST | (S1) | **`RequireModerator()`** | N |

No non-mod-reachable moderation or spotlight mutation exists. The **service layer is the enforcement
point and it enforces** — the "correct" side of the L5 class. (One status-code wrinkle, not an
access hole: MA-701.)

---

### MA-701 | Tier 2 | Bucket A | Slice 7
claim: The Moderation service's `RequireModerator()` throws `InvalidOperationException` (→ **401**) for a signed-in **non-mod** caller, whereas Spotlight/SiteSettings/Poll throw `UnauthorizedAccessException` (→ **403**) for the identical "authenticated but not a moderator" condition — the MA-123 divergence, confirmed. A signed-in non-mod is authenticated-but-forbidden = 403; the Moderation side is the wrong one.
evidence: `Server/Moderation/ServerModerationWriteService.cs:278-285` — `private int RequireModerator() { if (activeUser.UserId is not int id) throw new InvalidOperationException("...requires an authenticated user."); if (!activeUser.IsModerator && !activeUser.IsAdmin) throw new InvalidOperationException("Moderator action requires the Moderator or Admin role."); return id; }` mapped by `Server/Http/EndpointHelpers.cs:61-68` (`catch (InvalidOperationException) → Status401Unauthorized`). Contrast `Server/Spotlight/ServerSpotlightSlotAllocator.cs:118-125`, `Server/SiteSettings/ServerSiteSettingsWriteService.cs:32-37`, `Server/BlogPosts/ServerPollWriteService.cs:38-39,204-205,310-311` — all `throw new UnauthorizedAccessException("...requires a moderator.")` → `EndpointHelpers.cs:28-31` `Status403Forbidden`.
cells: F47 L2 (Stage 5 — proposes reopen), cross-ref F55/SiteSettings L2
effort: S | route: Stage-4 reconcile — split Moderation's role branch to `UnauthorizedAccessException` (403); the unauth branch is already 401'd at the endpoint by `.RequireAuthorization()`
verify: [pending]

### MA-702 | Tier 3 | Bucket A | Slice 7
claim: The Moderation **write** endpoints carry only the plain `.RequireAuthorization()` floor and lean entirely on the service's `RequireModerator()`, while the two sibling mod-only endpoint files in this same slice (`SpotlightSlotAllocatorEndpoints`, `SiteDailyStatEndpoints`) apply an edge-level `RequireAuthorization(ModeratorOnly)` role gate. Not exploitable (the service enforces), but it is the exact mechanism producing MA-701's wrong 401 and an inconsistent defense-in-depth posture within one slice.
evidence: `Server/Moderation/ModerationEndpoints.cs:94-155` — every write handler ends `.RequireAuthorization();` (floor only); the class doc (`:36-45`) explicitly defers to the service. Contrast `Server/Spotlight/SpotlightSlotAllocatorEndpoints.cs:21-22` — `MapGroup(...).RequireAuthorization(ModeratorOnly)` — and `Server/Moderation/SiteDailyStatEndpoints.cs:37,41`.
cells: F47 L5 (Stage 5 — proposes reopen)
effort: S | route: mechanical sweep — add `RequireAuthorization(ModeratorOnly)` to the mod-write group (resolves MA-701 at the edge too)
verify: [pending]

### MA-703 | Tier 2 | Bucket A | Slice 7
claim: The three moderation pages and `BlogPostPage` hand-roll a `text-danger`/`text-(--color-danger)` `<p>` and assign **raw `ex.Message`** from a blanket `catch (Exception ex)` — the MA-501/MA-504/MA-603 feedback-channel class. Validation feedback must go through `InlineAlert`; unexpected exceptions through `ExceptionPresenter` (never raw `ex.Message`). PollView, PollsPage, BlogPostEditorPage, and both Spotlight pages are the in-slice correct references.
evidence: `SharedUI/Moderation/ModReportsPage.razor:122-125` `@if (_actionError is not null) { <p class="mb-2 text-sm text-danger">@_actionError</p> }` fed by `:194,222,242,263` `catch (Exception ex) { _actionError = ex.Message; }`; same shape `ModUsersPage.razor:120,216`, `ModSubmissionsPage.razor:97,168,202`, and `BlogPostPage.razor:103,269` (`_likeError = ex.Message;`). Reference: `PollView.razor:162,330` (`<InlineAlert Message="@_error" />` + `_error = ExceptionPresenter.GetUserMessage(ex)`); `SpotlightRedemptionPage.razor:176,291-293` (`<InlineAlert>` + typed `when` filter).
cells: F47 L4/L3 (Stage 5 — proposes reopen), F36 L3 BlogPostPage
effort: M | route: mechanical sweep — route mod/like error state through `ExceptionPresenter`+`InlineAlert`
verify: [pending]

### MA-704 | Tier 3 | Bucket A | Slice 7
claim: `ReportDialog` catches **all** exceptions and shows a generic message but never logs the exception nor routes through `ExceptionPresenter` — a genuine unexpected fault (e.g. server 500) is silently swallowed with no log line / trace-id, defeating the "unexpected → log Error then generic-with-trace-id" contract. (Better than MA-703 in that the message is generic, worse in that it drops diagnostics.)
evidence: `SharedUI/Moderation/ReportDialog.razor:122-131` — `try { await ModerationService.SubmitReportAsync(...); _submitted = true; } catch (Exception) { _errorMessage = "Something went wrong. Please try again."; }` — no logger, no `ExceptionPresenter`.
cells: F46 L3 (Stage 5 — proposes reopen)
effort: S | route: mechanical sweep — funnel through `ExceptionPresenter.GetUserMessage` (which logs unexpected) or add explicit log
verify: [pending]

### MA-705 | Tier 2 | Bucket A | Slice 7
claim: `ToggleLikeAsync` writes `LikeCount` as a **C#-computed absolute value** (`currentLikeCount ± 1`) via `ExecuteUpdateAsync`, a read-then-write with a lost-update window under concurrent likes by different users — the MA-502 non-atomic-counter class. layer2 counter discipline mandates an atomic delta (`SetProperty(b => b.LikeCount, b => b.LikeCount + delta)`), which a like/unlike toggle can express (+1 on add, −1 on remove).
evidence: `Server/BlogPosts/ServerBlogPostWriteService.cs:161,169,174-176` — `newCount = Math.Max(0, currentLikeCount.Value - 1);` / `newCount = currentLikeCount.Value + 1;` then `.ExecuteUpdateAsync(s => s.SetProperty(b => b.LikeCount, newCount));` (absolute, not `x => x.LikeCount + delta`). Contrast the correct atomic delta at `:334` (`ActiveReportCount + delta`) and `:62` (`BlogPostsWritten + 1`) in the same/sibling service.
cells: F35 L2 (Stage 5 — proposes reopen)
effort: S | route: Stage-4 reconcile — atomic `± 1` delta keyed off `alreadyLiked`
verify: [pending]

### MA-706 | Tier 3 | Bucket A | Slice 7
claim: BlogPosts' DI binds `IBlogPostReadService` → **`ServerBlogPostWriteService`** (the derived write class), producing two independent instances of the write service per scope and handing read-only consumers the full write impl. This deviates from both the layer2-documented norm (Read→ReadImpl, as Poll/Spotlight/SiteSettings do) and Moderation's forwarding-delegate reference (Read resolved from the Write instance). MA-107 DI-shape class.
evidence: `Server/Program.cs:366-367` — `AddScoped<IBlogPostReadService, ServerBlogPostWriteService>(); AddScoped<IBlogPostWriteService, ServerBlogPostWriteService>();`. Reference (correct forwarding delegate) `:416-417` — `AddScoped<IModerationWriteService, ServerModerationWriteService>(); AddScoped<IModerationReadService>(sp => sp.GetRequiredService<IModerationWriteService>());`. Norm `:371-372,394-395` — `IPollReadService→ServerPollReadService`, `ISpotlightReadService→ServerSpotlightReadService`.
cells: F35/F36 L2 (Stage 5 — proposes reopen)
effort: S | route: mechanical sweep — bind read to `ServerBlogPostReadService` or use the forwarding delegate
verify: [pending]

### MA-707 | Tier 3 | Bucket A | Slice 7
claim: `BlogPostPropertiesForm` wraps `EditorView` but its submit `<button>` carries no `aria-label` — the MA-212/MA-307/MA-607 class (testing.md collision rule: every button in an EditorView-wrapping component needs a unique aria-label so bUnit selectors don't collide with Quill toolbar buttons).
evidence: `SharedUI/BlogPosts/BlogPostPropertiesForm.razor:33` `<EditorView @ref="_editor" ...>` and `:83-87` `<button type="submit" ...>@(ViewModel.IsLoading ? "Saving…" : SubmitLabel)</button>` — no `aria-label`.
cells: F35 L4 (Stage 1 — visual sign-off pending)
effort: S | route: mechanical sweep
verify: [pending]

### MA-708 | Tier 3 | Bucket A | Slice 7
claim: `BlogPostEditorPage` uses `NavigationManager.NavigateTo("/not-found")` (HTTP 200 + client redirect, not a real 404) for the missing/non-owned post case — the MA-202/MA-304/MA-606 class (render-and-layout.md mandates `NavigationManager.NotFound()`). `BlogPostPage` uses a third variant: an inline `<p>Post not found.</p>` soft message (also 200, not 404) — the GroupPage-style inline pattern. Zero uses of `Nav.NotFound()` in the slice.
evidence: `SharedUI/BlogPosts/BlogPostEditorPage.razor:161` `NavigationManager.NavigateTo("/not-found");`. `SharedUI/BlogPosts/BlogPostPage.razor:22-25` `else if (_notFound || Post is null) { <p class="text-(--color-danger)">Post not found.</p> }`.
cells: F35 L3 (editor), F36 L3 (page) — Stage 5, proposes reopen
effort: S | route: mechanical sweep (part of the cross-cutting MA-202 `Nav.NotFound()` sweep) — direction undetermined for the inline-vs-404 case (missing-vs-hidden ambiguity, S4/GroupPage precedent)
verify: [pending]

### MA-709 | Tier 3 | Bucket C | Slice 7
claim: `CreateProfileBlogPostAsync` carries an untracked `TODO(WU33)` for follower notifications in a Stage-5 cell — the H-07 stale/untracked-TODO class (same shape as MA-506/MA-605). The group path (`CreateGroupBlogPostAsync`) DOES fan out (`NotifyNewGroupBlogPostAsync`), so profile-post follower notifications are the silent gap.
evidence: `Server/BlogPosts/ServerBlogPostWriteService.cs:64` — `// TODO(WU33): notify followers of a new blog post once the notification type exists.`
cells: F35 L2 (Stage 5)
effort: S | route: doc-touch decision — confirm whether profile-post follower notifications are in scope; track or remove the TODO
verify: [pending]

---

## Hypothesis results (slice 7)

- **H-01 (@key on stateful list children):** clean — BlogPostPage keys `PollView` on `poll.PollId` (`:117`); ModReportsPage/ModUsersPage table rows hold no per-row stateful child (action state lives in parent `_activeReport`), so no @key needed; SpotlightRedemptionPage blocks keyed.
- **H-02 (dispatcher reload discipline):** clean — BlogPostPage is a textbook guarded dispatcher (sentinel `_loadedBlogPostId` + `_initialized`, plain-assign on reload, `??=` only on the persisted-restore branch). Mod/Spotlight pages are desktop-only, no route param (mod convention).
- **H-03 (unnamed HasIndex overwrite):** n/a — EF configs are S1's; slice product code has no `HasIndex`; the spotlight composite `(start_date,end_date)` index shipped in the WU-Spotlight migration.
- **H-04 (factory-per-method reads):** clean — all read services (`ServerModerationReadService`, `ServerPollReadService`, `ServerSpotlightReadService`, `ServerBlogPostReadService`, `ServerSiteDailyStatReadService`) open `await using` contexts per method; write services hold only `writeDb`; the derived-write CS9107 `protected ReadDbFactory`/`ActiveUser`/`SiteSettings` idiom is used correctly.
- **H-05 (dead Tailwind classes):** clean — paren-form `(--color-*)` throughout; bare-name semantic tokens (`text-danger`, `bg-surface`, `text-text`, `bg-warning/20`) in ReportDialog/mod pages are the sanctioned dual style (S2's ruling), and the audit notes record `check-design-tokens.ps1` green for these files. No bracket-form/raw-palette/hex.
- **H-06 (unregistered silent catches):** **MA-704** — `ReportDialog` catch-all swallows without logging. All service best-effort notify catches correctly `LogWarning`/`LogError`; PollView/PollsPage/BlogPostEditorPage route through `ExceptionPresenter`.
- **H-07 (stale/untracked TODO):** **MA-709** — `TODO(WU33)` follower-notification gap in a Stage-5 blog write cell.
- **H-08 (Nav.NotFound vs manual /not-found):** **MA-708** — BlogPostEditorPage `NavigateTo("/not-found")`; BlogPostPage inline soft message; zero `Nav.NotFound()`.
- **H-09 (dispatcher load parallelism):** clean — BlogPostPage `Task.WhenAll`s its 2 independent loads (`:220-222`); mod pages do single loads; SpotlightRedemptionPage loads are dependency-ordered.
- **H-10 (debounced/pending writes lost):** n/a — no per-component debounce in the slice; poll vote / blog like / redeem all write synchronously (await + reconcile/rollback). MA-401 class does not recur.
- **H-11 (doc-vs-code staleness):** clean for new instances — no post-flip doc claim contradicted beyond already-filed items; the settled BlogPosts/Poll/Spotlight audit notes match the code (TPT projection rule, config-lock, advisory-lock redemption, soft-delete model all present as documented).
- **H-12 (fire-and-forget without observation):** clean — no `_ = SomeAsync(...)`; all client calls awaited; best-effort notify catches log.
- **H-13 (denormalized counter discipline):** **MA-705** — BlogPost `LikeCount` written as C#-computed absolute (non-atomic). `AdjustActiveReportCountAsync` and `BlogPostsWritten` correctly use atomic `x => x.col + delta`.
- **H-14 (elevated reads annotated + named):** clean — `ServerModerationReadService` uses `IgnoreQueryFilters(["IsTakenDown"])` with `// elevated read:` on all ~6 mod-queue paths; ContentRating/GroupAudience left live (per-mod rating scoping); write service reads ground truth unfiltered by architecture (no bypass needed).
- **H-15 (write-path by-id bypass ContentRating):** clean by construction — every write-service existence check reads unfiltered `writeDb` (spotlight story/rec eligibility, poll owner, blog author, moderation `LoadModeratableAsync`); no readDb PK fetch in a write path.
- **H-16 ([FromQuery] on non-GET arrays):** clean — `PollEndpoints` vote carries explicit `[FromQuery] int[] optionIds` with the trap comment (the Global-Flip fix); all other slice endpoints use scalar/DTO-body params.
- **H-17 (nullable client reads use tolerant helpers):** clean — `ClientBlogPostReadService.GetByIdAsync`/`GetForEditAsync` use `GetNullableFromJsonAsync<T?>`; non-null paged shapes use `GetFromJsonAsync<PagedResult<T>>`; write clients use `ThrowIfWriteFailedAsync`+`ClientHttpHelpers.ReadProblemDetailAsync`.
- **H-18 (aria-labels on icon-only + EditorView-adjacent buttons):** **MA-707** — BlogPostPropertiesForm submit (wraps EditorView) lacks aria-label. PollEditorForm remove-option button IS labeled (`:42-43`); it wraps no EditorView (plain text inputs).
- **H-19 (AuthorizeView-gated DI uses wrapper/inner split):** clean — no AuthorizeView-gated DI leaf in the slice; PollView/CommunitySpotlightDisplay inject via the sanctioned self-contained-write precedent (page-resolved `CurrentUserId`, not `IActiveUserContext`); mod pages are page-level `[Authorize(Roles=...)]` (router never constructs for anon).
- **H-20 (feedback-channel discipline):** **MA-703 + MA-704** — mod pages + BlogPostPage raw `ex.Message` via hand-rolled danger `<p>`; ReportDialog unlogged catch-all. In-slice references: PollView, PollsPage, BlogPostEditorPage, SpotlightRedemptionPage, ModSpotlightPage (InlineAlert + ExceptionPresenter/typed filters).

**TPT projection safety (headline resolution check):** clean — `ServerPollReadService` filters with `Where(p => p is SitePoll)` / `Where(p => p is BlogPostPoll)` on a statically `IQueryable<BasePoll>` source and never `OfType<TChild>()` into the shared `ProjectAsync` base-typed projection, with an explicit comment documenting the coercion-crash trap (`:20-24`). `ServerPollWriteService.SetSitePollArchivedAsync` uses `writeDb.Polls.OfType<SitePoll>()` but that is a single-type tracked write query, not a base-typed shared projection — safe. No `OfType`-into-shared-projection anywhere.

**Sanitization (MA-201 class):** clean — blog bodies sanitize on save (`ServerBlogPostWriteService.cs:40,88,203` `sanitizer.Sanitize(dto.Content)` on create/update/group-create). Poll options/name/description are **plain text, not EditorView HTML** — `PollEditorForm` uses `<input type="text">`/`<textarea>` (`:19,25,35`), `PollView` renders `@Poll.PollName`/`@Poll.Description`/`@option.Text` via Razor auto-encoding, not `MarkupString`/`RichTextView` (`:17,56,72`; author comment `:10-11` confirms "plain Container text, never RichTextView"). The write service's `.Trim()`-only handling is therefore correct — no XSS. Spotlight has no rich text.
