# Slice 6 — Identity & Profiles (Identity, Profiles, Badges, Notifications)

Audited 2026-07-17 by the S6 slice agent. Read-only pass; no builds/tests run — all `verify: [pending]`.

## File inventory (path + LOC)

### Product — Core
| LOC | File |
|---|---|
| 7 / 62 / 15 / 160 | Core/Identity/{ApplicationRole, IActiveUserContext, IUserActivityWriteService, User}.cs |
| 13/38/72/25/35/63/19/9/11/26/60/27 | Core/Profiles/{AuthorSettingsDto, IUserProfileReadService, IUserSettingsService, PrivacySettingsDto, ProfileHeaderDto, ProfileTab, ReaderSettingsDto, UpdateProfileDto, UserProfile, UserSettingsDto, UserStat, UserStatsDto}.cs |
| 25/28/16/32/20 | Core/Badges/{Badge, EarnedBadgeDto, IBadgeReadService, IBadgeWriteService, UserBadge}.cs |
| 53/205/24/22/50/20/27/28/15 | Core/Notifications/{INotificationReadService, INotificationWriteService, Notification, NotificationCategory, NotificationDto, NotificationFeedOrder, NotificationSettingDto, NotificationType, UserNotificationSetting}.cs |

### Product — Server
| LOC | File |
|---|---|
| 53/54/47/100/17 | Server/Identity/{ApplicationUserClaimsPrincipalFactory, CanalaveSignInManager, IdentityRevalidatingAuthenticationStateProvider, ServerActiveUserContext, ServerUserActivityWriteService}.cs |
| 41/40/153/22/54/80 | Server/Identity/{EmailBodies, EmailOptions, IdentityComponentsEndpointRouteBuilderExtensions, IdentityNoOpEmailSender, IdentityRedirectManager, SmtpEmailSender}.cs |
| 74/43/44/78 | Server/Identity/{UserActivityBuffer, UserActivityEndpoints, UserActivityFlushWorker, UserActivityFlusher}.cs (LastActive signal buffer — Feature 62/SiteDailyStat, physically here) |
| ~3052 | Server/Identity/Pages/** + Shared/** (31 scaffolded Identity Razor pages — permanently L5 N/A; see MA-610) |
| 131/279/35/109/51/289 | Server/Profiles/{ServerUserProfileReadService, ServerUserSettingsService, UserProfileEndpoints, UserSettingsEndpoints, UserStatRecalculationWorker, UserStatRecalculator}.cs |
| 76/43/81 | Server/Badges/{BadgeEndpoints, ServerBadgeReadService, ServerBadgeWriteService}.cs |
| 41/47/90/352/346 | Server/Notifications/{NotificationCleanupSweeper, NotificationCleanupWorker, NotificationEndpoints, ServerNotificationReadService, ServerNotificationWriteService}.cs |
| 84 | Server/Services/UserDeletionService.cs (LEGACY folder — see MA-608) |

### Product — SharedUI / Client
| LOC | File |
|---|---|
| 89/89/156/151/142/303/345/399/115/180/258/30 | SharedUI/Profiles/{AppearanceSettingsForm, AuthorSettingsForm, BadgeSettingsForm, PrivacySettingsForm, ProfileBanner, ProfileDesktop, ProfileMobile, ProfilePage, ProfileSettingsForm, ReaderSettingsForm, SettingsPage, UserStatsBlock}.razor |
| 25/125/99/74/178/125/230 | SharedUI/Notifications/{NotificationBell, NotificationBellInner, NotificationCategoryVisuals.cs, NotificationItem, NotificationPresenter.cs, NotificationSettingsPage, NotificationsPage}.razor |
| 23/53/29 | Client/Identity/{ClientUserActivityWriteService, WasmActiveUserContext, WasmHostEnvironmentAdapter}.cs |
| 21/104/18/57/37/148 | Client/{Profiles/ClientUserProfileReadService, Profiles/ClientUserSettingsService, Badges/ClientBadgeReadService, Badges/ClientBadgeWriteService, Notifications/ClientNotificationReadService, Notifications/ClientNotificationWriteService}.cs |

### Tests owned by this slice
| Tier | File |
|---|---|
| Integration | AccountStatusEnforcementTests, BadgeServiceTests, NotificationCleanupTests, NotificationServiceTests, UserActivityFlushTests, UserDeletionServiceTests, UserStatRecalculatorTests |
| Unit | EmailBodiesTests, EmailOptionsTests, NotificationCategoryVisualsTests, NotificationPresenterTests, UserActivityBufferTests |
| RazorComponents | AccountStatusBannerTests, BadgeSettingsFormTests, ProfilePageTests, FakeProfileTestServices |

Scope notes resolved: **UserActivity\*** (Server/Identity/, Client/Identity/) is the `LastActiveUtc` signal buffer feeding Feature 62 (site stats / "last seen") — physically in Identity, functionally a SiteDailyStat concern; audited for the signal-buffer shape (conformant), no findings. **AccountStatusBanner / UserCard** are S0 atoms (consumption only). Identity `Email*`/`Smtp*` verified Stage-5 by S1's `audit/Identity.md` WU-Email note — skimmed, no new findings.

---

### MA-601 | Tier 1 | Bucket A | Slice 6
claim: `BadgeEndpoints` is a live broken-access-control (IDOR) surface — every route takes a client-supplied `userId` that the service trusts without any caller-vs-target check, so any authenticated caller can self-award (or award anyone) **any** catalogue badge via `/award`, read another user's full hidden-badge curation view via `/`, or overwrite another user's badge order via `/display-order`. The service is declared the single enforcement point but enforces nothing; the client impl actually calls these over HTTP, and the WASM flip made the routes reachable.
evidence: `TheCanalaveLibrary.Server/Badges/BadgeEndpoints.cs:56-59` — `group.MapPost("/award", (IBadgeWriteService badges, int userId, string badgeKey) => EndpointHelpers.ExecuteWriteAsync(async () => Results.Ok(await badges.AwardAsync(userId, badgeKey)))).RequireAuthorization();` ; `ServerBadgeWriteService.cs:28-51` — `AwardAsync(int userId, string badgeKey)` does only an idempotency check + insert, no `activeUser`/ownership guard ; `Client/Badges/ClientBadgeWriteService.cs:27-28` — `await Http.PostAsync($"api/badges/award?userId={userId}&badgeKey=...")` (endpoint is really reached) ; the class's own doc (`BadgeEndpoints.cs:20-24`): "a caller could pass an arbitrary `userId` to read another user's hidden-badge curation view, self-award any catalogue badge via `/award`, or overwrite another user's `DisplayOrder` … none of that is caught today … Flagged for the eventual browser debug wave rather than resolved here." Contrast the in-slice reference `NotificationEndpoints.cs:11-19`, which deliberately does NOT map its generation methods "a privilege-escalation surface."
cells: F50 L2 + L5 — **proposes reopen** (both Stage 5 per status.md). Fix direction: make the self-scoped ops (`GetMyBadgesForCurationAsync`, `SetDisplayOrderAsync`) resolve `userId` from `IActiveUserContext` (the §3.5 self-scoping pattern `IUserSettingsService` already uses) and drop `/award` from the HTTP surface entirely (server-internal only, like the Notify* methods).
effort: M | route: Stage-4 reconcile
verify: [pending]

### MA-602 | Tier 1 | Bucket A | Slice 6
claim: `UserProfileEndpoints` binds `includePrivate` straight from the query string and carries **no** `RequireAuthorization()`, and `GetProfileHeaderAsync(userId, includePrivate:true)` skips every `ProfileVisibility` gate plus the `ShowUserStats`/`ShowActivityStatus` gates — so `GET /api/user-profiles/{anyId}?includePrivate=true` returns any user's Private-profile header, hidden stats, and "last seen" to **any caller, including anonymous**. The client passes the bool through verbatim; the endpoint is the security boundary and it trusts a UI affordance, exactly the anti-pattern `identity-and-authorization.md` §"Security vs affordance" forbids.
evidence: `TheCanalaveLibrary.Server/Profiles/UserProfileEndpoints.cs:27-28` — `group.MapGet("/{userId:int}", async (IUserProfileReadService profiles, int userId, bool includePrivate) => Results.Json(await profiles.GetProfileHeaderAsync(userId, includePrivate)));` (no `.RequireAuthorization()`; class doc: "the caller … is trusted to pass `viewerId == profileUserId`") ; `ServerUserProfileReadService.cs:53-61` — `if (!includePrivate) { if (…Private) return null; if (…UsersOnly && activeUser.UserId is null) return null; }` (all gating is inside `!includePrivate`) ; `:85` stats gated by `(includePrivate || …ShowUserStats)`, `:106` last-seen by `(includePrivate || …ShowActivityStatus)` ; `Client/Profiles/ClientUserProfileReadService.cs:15-17` passes the bool through. With no `FallbackPolicy` in the app (S1 MA-104) the route is genuinely anonymous-reachable.
cells: F21 L2 + L5 — **proposes reopen** (both Stage 5). Fix: derive `includePrivate` server-side (`activeUser.UserId == userId`) in the endpoint or service; never accept it from the client.
effort: M | route: Stage-4 reconcile
verify: [pending]

### MA-603 | Tier 2 | Bucket A | Slice 6
claim: `SettingsPage` — the reference settings page for the FORM-HEAVY cluster — hand-rolls its success/error banners as raw `<div>`s instead of `InlineAlert` (the ratified "ONLY channel for validation feedback"), and its catch surfaces the raw `ex.Message` to the UI, which the `ExceptionPresenter` contract calls a defect (unexpected exceptions must be translated, never shown raw).
evidence: `TheCanalaveLibrary.SharedUI/Profiles/SettingsPage.razor:40-45` — `@if (_errorMessage is not null) { <div class="rounded-lg bg-(--color-danger)/10 px-4 py-3 text-sm text-(--color-danger)" role="alert">@_errorMessage</div> }` ; `:249-251` — `catch (Exception ex) { _errorMessage = $"Save failed: {ex.Message}"; }` ; calibration seam table (`ExceptionPresenter`): "raw `ex.Message` in UI is a defect"; (`InlineAlert`): "the ONLY channel for validation feedback." Matches the MA-205/307/405/501/504 class.
cells: F20 L3-Logic (SettingsPage; Stage 5 — polish inside an aligned cell)
effort: S | route: mechanical sweep
verify: [pending]

### MA-604 | Tier 2 | Bucket A | Slice 6
claim: `SettingsPage` (a SharedUI component) injects `IActiveUserContext` — the exact rule `identity-and-authorization.md` states SharedUI never does ("SharedUI survives the L5 WASM split only because it never injects it") — while its sibling `ProfilePage` resolves the viewer id the doc-approved way, from the cascaded `Task<AuthenticationState>`. Two profile pages resolve identity two different ways; and this is the same post-Global-Flip doc tension S0 filed as MA-004 (the rule is stale now that `WasmActiveUserContext` exists), recurring in a second SharedUI component beyond `UserActivityTracker`. Symmetric — direction undetermined.
evidence: `TheCanalaveLibrary.SharedUI/Profiles/SettingsPage.razor:9` — `@inject IActiveUserContext ActiveUser` (used at `:135`/`:224` `ActiveUser.UserId`) vs. `TheCanalaveLibrary.SharedUI/Profiles/ProfilePage.razor:111,180-181` — `[CascadingParameter] Task<AuthenticationState> AuthState` → `authState.User.FindFirst(ClaimTypes.NameIdentifier)`. Repo-wide grep: only `SettingsPage.razor` + `Layout/UserActivityTracker.razor` (S0's) inject `IActiveUserContext` in SharedUI.
cells: F20 L3-Logic (proposes no reopen — flag for the Bucket-B/doc pass with MA-004; H-11)
effort: S | route: doc-touch decision (ratify SharedUI-may-inject post-flip, or switch SettingsPage to the AuthState cascade for consistency)
verify: [pending]

### MA-605 | Tier 2 | Bucket A | Slice 6
claim: Changing `ShowMatureContent`, `Theme`, or `PrefersAnimatedSprites` through the settings service never re-issues the auth cookie, so the claims `IActiveUserContext` reads (which drive the content-rating query filter and sprite theme) stay stale until the user next signs in. `ApplicationUserClaimsPrincipalFactory`'s own doc explicitly hands this to WU30 — "stale until next sign-in unless that write path calls `SignInManager.RefreshSignInAsync` … Flagged here for WU30, not solved by WU12" — but WU30's `ServerUserSettingsService` (and no settings endpoint/page) calls it; `RefreshSignInAsync` appears only in Identity Manage pages. Distinct from the *by-design* AccountStatus/Warned staleness (which has the notification as its immediate channel): a user who enables mature content simply sees no effect until re-login.
evidence: `TheCanalaveLibrary.Server/Identity/ApplicationUserClaimsPrincipalFactory.cs:23-25` (doc) — "if a user's ShowMatureContent/Theme/PrefersAnimatedSprites changes (WU30 profile settings), the auth cookie is stale until next sign-in unless that write path calls `SignInManager.RefreshSignInAsync` … Flagged here for WU30, not solved by WU12." ; `Server/Profiles/ServerUserSettingsService.cs:179` sets `user.ShowMatureContent = dto.ShowMatureContent` and `:223-233` `UpdateAppearanceAsync` sets Theme/PrefersAnimatedSprites — neither refreshes sign-in ; repo grep `RefreshSignInAsync`: only Identity `Manage/*`, `ConfirmEmailChange` — never a settings path.
cells: F20 L2 (proposes no reopen; the factory doc's "Flagged for WU30" handoff is unclosed — H-07/H-11)
effort: M | route: seam — direction undetermined (refresh the cookie on the settings endpoint after a hot-claim change, or document the next-sign-in staleness as accepted the way AccountStatus is)
verify: [pending]

### MA-606 | Tier 2 | Bucket A | Slice 6
claim: `ProfilePage`'s bad-tab-slug branch navigates with `NavigationManager.NavigateTo("/not-found")` (two sites) rather than `NavigationManager.NotFound()`, so a bad profile-tab URL returns 200 + client redirect instead of a real 404 — the MA-202/304/404 class, now extended into Profiles. (The missing/hidden-profile branch renders an inline soft message, the deliberate don't-leak-existence choice — S5 GroupPage precedent, not filed.)
evidence: `TheCanalaveLibrary.SharedUI/Profiles/ProfilePage.razor:186` — `if (parsed is null) { Nav.NavigateTo("/not-found"); _initialized = true; return; }` and `:205` — `if (parsed is null) { Nav.NavigateTo("/not-found"); return; }`. `render-and-layout.md` mandates `NavigationManager.NotFound()`; grep confirms zero `Nav.NotFound()` uses in the slice.
cells: F21 L3-Logic (Stage 5 — same class the audit is tracking across dispatchers; direction undetermined per MA-202)
effort: S | route: mechanical sweep (consolidate with MA-202's resolution)
verify: [pending]

### MA-607 | Tier 3 | Bucket A | Slice 6
claim: `ProfileSettingsForm` wraps an `EditorView` (the bio composer) and its "Save Profile" button carries no `aria-label` — the H-18 rule ("every button in a component wrapping EditorView carries a unique aria-label"), same class as MA-212 (StoryPropertiesForm) and MA-307 (ChapterPropertiesForm).
evidence: `TheCanalaveLibrary.SharedUI/Profiles/ProfileSettingsForm.razor:42-44` — `<ContentSurface Variant="…Input"><EditorView @ref="_editor" … /></ContentSurface>` ; `:61-66` — `<button type="button" class="…" disabled="@Busy" @onclick="HandleSaveProfileAsync">@(Busy ? "Saving…" : "Save Profile")</button>` (no `aria-label`).
cells: F20 L4-Style (a11y criterion; Stage 5)
effort: S | route: mechanical sweep
verify: [pending]

### MA-608 | Tier 3 | Bucket A | Slice 6
claim: `UserDeletionService` still lives in the deprecated `Server/Services/` technical-layer folder and uses the pre-primary-constructor idiom (explicit ctor + `_context` field) that the rest of the slice has moved past — confirms S1's MA-112 disposition (this is the Identity-domain file S1 pointed at S6). The service's manual FK-ordered cleanup + `CreateExecutionStrategy` transaction handling is otherwise the settled, correct pattern (the retrying-strategy hazard is honored).
evidence: `TheCanalaveLibrary.Server/Services/UserDeletionService.cs:11-18` — `public class UserDeletionService { private readonly ApplicationDbContext _context; public UserDeletionService(ApplicationDbContext context) { _context = context; } }` ; SKILL.md §"Code Organization": "`Server/Services/` … No new file is ever added to one. Any work-unit that touches a file still living in one of them moves it into its feature cluster." Target: `Server/Identity/`.
cells: F52 organization (no stage change proposed — convention defers the move to the next touching WU; cross-refs S1 MA-112)
effort: S | route: mechanical sweep (move to Server/Identity/ + primary ctor when next touched)
verify: [pending]

### MA-609 | Tier 3 | Bucket A | Slice 6
claim: `AppearanceSettingsForm` diverges from the rest of the settings-form family on two mechanical points: it seed-guards with a value sentinel (`if (_themeId == 0)`) instead of the family's `private bool _seeded` flag, and it takes four scalar `Initial*` parameters instead of an `Initial` DTO like Reader/Privacy/Author. The sentinel guard is also subtly weaker — a valid `ThemeId` of 0 (or a first-render race before themes load) would re-seed. Intra-slice inconsistency across near-identical sibling forms.
evidence: `TheCanalaveLibrary.SharedUI/Profiles/AppearanceSettingsForm.razor:59-83` — `[Parameter] public int InitialThemeId … ` + `protected override void OnParametersSet() { if (_themeId == 0) { … } }` vs. `ReaderSettingsForm.razor:138,154,156-158` — `[Parameter, EditorRequired] public ReaderSettingsDto Initial` + `private bool _seeded;` + `if (!_seeded)` (Privacy/Author identical).
cells: F20 L3-Logic (cosmetic consistency; Stage 5)
effort: S | route: mechanical sweep
verify: [pending]

### MA-610 | Tier 3 | Bucket A | Slice 6
claim: The scaffolded Identity Pages (~3052 LOC, the single largest LOC block in the slice) carry ~1325 LOC of untouched ASP.NET-Identity scaffold for features that are **not** in the 65-feature set: two-factor auth (`LoginWith2fa`, `LoginWithRecoveryCode`, `Manage/EnableAuthenticator`/`Disable2fa`/`ResetAuthenticator`/`GenerateRecoveryCodes`/`TwoFactorAuthentication`), passkeys (`Manage/Passkeys`/`RenamePasskey`), and external-login (`ExternalLogin`, `Manage/ExternalLogins`) with no configured provider. This is both dead code (lens 5) and the headline code-economy item (lens 9). NOT a namespace/asset-path defect — the historical `Components.Account → Identity` namespace drift is resolved (grep finds the stale string only in docs/GeminiDiscussions), and `App.razor:83`'s `@Assets["Identity/Shared/PasskeySubmit.razor.js"]` matches the physical file (the historical 404 is fixed).
evidence: `wc -l` of the eleven pages above = 1325 LOC ; brief §"Identity pages … permanently L5 N/A"; these routes are wired into `Manage/ManageNavMenu.razor` but no feature row (grid_axes) covers 2FA/passkey/external-login.
cells: F1 organization / dead-code (no stage change — Identity scaffold is deliberately the framework flow; removal is a product decision)
effort: L (aggregate) | route: doc-touch decision (decide keep-as-framework-flow vs. prune the unbuilt-feature pages + their Manage nav entries)
verify: [pending]

### MA-611 | Tier 3 | Bucket A | Slice 6
claim: Two endpoint groups map a genuine business-rule failure onto HTTP 401 because the shared `EndpointHelpers.ExecuteWriteAsync` blanket-maps `InvalidOperationException` → 401 under the "requires an authenticated user" assumption: `BadgeEndpoints` `/display-order` (a not-yet-earned key) and `UserSettingsEndpoints` `/author` (a non-owned/unpublishable pinned story) both throw `InvalidOperationException` for non-auth reasons, so a 400 condition surfaces as 401. Both are self-documented as known; ties to the same shared-helper contract S1 flagged in MA-123 (403-vs-401 divergence).
evidence: `TheCanalaveLibrary.Server/Badges/BadgeEndpoints.cs:30-37` (doc) — "`SetDisplayOrderAsync` throws the same exception type for an unrelated reason … so that case surfaces as 401 instead of the more accurate 400." ; `TheCanalaveLibrary.Server/Profiles/UserSettingsEndpoints.cs:22-29` (doc) — "`UpdateAuthorSettingsAsync`'s pinned-story … guard also throws `InvalidOperationException` … still maps it to 401 uniformly … 400 would be more accurate." ; `ServerUserSettingsService.cs:206-208` throws `InvalidOperationException` for the business-rule pin failure.
cells: F50/F20 L5 (cross-refs S1 MA-123; message survives via ProblemDetails.Detail so no data loss — status semantics only)
effort: M | route: seam — direction undetermined (distinct exception type per case, or special-case the shared helper — a cross-cluster decision)
verify: [pending]

---

## MA-102 ruling (S1 handoff) — CONFIRMED (not refuted)

S1's MA-102 is correct on every point. `User.cs:160` — `public virtual ICollection<ApplicationRole> Roles { get; set; } = new List<ApplicationRole>();` is an unreferenced navigation (repo grep for `.Roles` usage: zero product hits). EF models it as a one-to-many, minting a phantom nullable FK on `asp_net_roles`:
- `ApplicationDbContextModelSnapshot.cs:271-273` — `b.Property<int?>("UserId") … .HasColumnName("user_id");` (on `ApplicationRole`)
- `:282-283` — `b.HasIndex("UserId").HasDatabaseName("ix_asp_net_roles_user_id");`
- `:4606-4609` — `b.HasOne("…User", null).WithMany("Roles").HasForeignKey("UserId").HasConstraintName("fk_asp_net_roles_asp_net_users_user_id");` — and this relationship carries **no** explicit `.OnDelete(...)`, violating layer1's "Delete behavior is always explicit."

Ruling: **Tier 2, Bucket A, proposes reopen F1 L1** (status.md row 1 L1 = Stage 5). Delete the nav + a migration dropping `user_id`/`ix_asp_net_roles_user_id`. (Sibling skip-navs on `User` — e.g. `Groups` at `User.cs:141` — were not individually snapshot-verified this pass; `Roles` is the confirmed phantom S1 named.) Filed by S1 as MA-102 — not re-numbered here.

---

## Hypothesis results (slice 6)

- **H-01** (`@key` on stateful list children): **clean.** `NotificationsPage`/`NotificationBellInner` `@foreach <NotificationItem>` carry no `@key`, but `NotificationItem` recomputes its only cached field (`_composed`) **unconditionally** in `OnParametersSet` (no `is null` guard, no reveal/menu flag, no per-row CTS), so instance reuse across list shuffles cannot corrupt state — benign. `BadgeSettingsForm` reorders inline markup (no stateful child components). Profile story tabs delegate to `StoryDeck` (S2, keyed).
- **H-02** (route-param dispatcher reload discipline): **clean.** `ProfilePage` is the textbook guarded dispatcher — sentinels `_loadedUserId`/`_loadedTab`, `_initialized`, `ClearTabPayload()` before reload, plain-assign of `[PersistentState]` on reload. `SettingsPage`/`NotificationsPage` have no route params (restore-or-fetch via `??=`).
- **H-03** (unnamed `HasIndex` overwrite): **n/a** — EF configs are S1's; no `HasIndex` in slice product code. (The phantom `ix_asp_net_roles_user_id` is MA-102's shadow FK, a different mechanism.)
- **H-04** (read-context factory-per-method): **clean.** All read services (`ServerUserProfileReadService`, `ServerNotificationReadService`, `ServerBadgeReadService`, `ServerUserSettingsService`) open a context per method via `IDbContextFactory`. `ApplicationUserClaimsPrincipalFactory` injects `ReadOnlyApplicationDbContext` directly — the sanctioned sign-in-scope exception (layer2 §"Read-Context Concurrency").
- **H-05** (dead Tailwind classes): **clean.** Paren-form tokens (`text-(--color-text)`, `bg-(--color-action)`) throughout; `bg-(--color-danger)/10` opacity modifiers on tokens are valid. No bare multi-word / bracket-form / raw-palette usage in slice markup.
- **H-06** (unregistered silent catches): **clean.** The one silent catch (`ServerActiveUserContext.cs:92-97`) is registered/commented `// sanctioned-silent:`. `SettingsPage` catch sets a message (not silent — but raw `ex.Message`, see MA-603). Best-effort catches (`ServerUserSettingsService.cs:269` orphan-blob delete) `LogWarning`.
- **H-07** (stale/untracked TODO(WU-x)): **MA-605** — `ApplicationUserClaimsPrincipalFactory`'s "Flagged here for WU30, not solved by WU12" is an unclosed handoff (RefreshSignInAsync never wired into settings). The WU35 "enrichment not yet committed" note in `audit/Notifications.md` is now **resolved** — `ServerNotificationReadService.BatchLoadEntitiesAsync` implements the real two-pass batch.
- **H-08** (`Nav.NotFound()` vs manual `/not-found`): **MA-606** — `ProfilePage` uses `Nav.NavigateTo("/not-found")` ×2; zero `Nav.NotFound()` in slice.
- **H-09** (dispatcher load parallelism): **clean.** `SettingsPage` `Task.WhenAll`s its 4 independent loads (sanctioned list). `ProfilePage` loads are genuine dependency chains (candidateIds→listings→states). `NotificationBellInner` parallel loads under the factory rule.
- **H-10** (debounced/pending writes lost on dispose): **clean.** No per-component debounce in slice — settings save on button click, notification mark-read is immediate. (The claim-staleness of MA-605 is a re-issue gap, not a dropped debounce.)
- **H-11** (doc-vs-code staleness): **MA-604** (SettingsPage injects `IActiveUserContext` in SharedUI — MA-004 recurrence), **MA-605** (WU30 RefreshSignInAsync handoff), **MA-610** (Identity scaffold for unbuilt features). Namespace drift (`Components.Account`) and the PasskeySubmit asset path are both **resolved** (verified).
- **H-12** (fire-and-forget without observation): **clean** — no `_ =`/unawaited launches in slice product code; all client calls awaited; best-effort notify catches `LogWarning`.
- **H-13** (denormalized counter discipline): **clean in slice.** Badge `DisplayOrder` is curation, not a counter. `UserStatRecalculator` uses set-based `UPDATE…FROM` with `IS DISTINCT FROM` two-pass (match + zero-unmatched). Real-time `UserStat` increments live in other slices' write services (the `RecommendationSuccessesEarned++` race is S5's MA-502).
- **H-14** (elevated reads annotated + named): **n/a** — zero `IgnoreQueryFilters` in slice (write-path existence checks read the unfiltered `writeDb`).
- **H-15** (write-path by-id lookups bypass ContentRating): **clean by construction** — `ServerUserSettingsService` reads `writeDb.Users.FindAsync`/`writeDb.Stories` (unfiltered write context); no readDb PK fetch in a write path.
- **H-16** (`[FromQuery]` on non-GET arrays): **clean** — `BadgeEndpoints` `/display-order` uses `[FromBody] List<string>` (avoids the query-bind trap); notification/settings endpoints use scalar params.
- **H-17** (nullable client reads use empty-body-tolerant helpers): **clean** — `ClientUserProfileReadService` uses `GetNullableFromJsonAsync` for `ProfileHeaderDto?`/`string?`; `ClientNotificationReadService`/`ClientBadgeReadService` use plain `GetFromJsonAsync` only for non-null shapes (counts, arrays with `?? []`) whose endpoints always return a value.
- **H-18** (aria-labels on icon-only + EditorView-adjacent buttons): **MA-607** — `ProfileSettingsForm`'s EditorView-adjacent Save button lacks `aria-label`. `NotificationBellInner` bell + `NotificationItem` are labeled; the settings-form Save buttons are text buttons (not icon-only).
- **H-19** (AuthorizeView-gated DI wrapper/inner split): **clean — confirmed reference.** `NotificationBell.razor` is the thin `<AuthorizeView><Authorized><NotificationBellInner/></Authorized></AuthorizeView>` wrapper; `NotificationBellInner.razor` holds all `@inject` + the `OnInitializedAsync` fetch. This IS the WU43 reference impl.
- **H-20** (feedback-channel discipline): **MA-603** — `SettingsPage` hand-rolls success/error `<div>`s + raw `ex.Message`. `ProfilePage.HandleCopyTagSelectionAsync` is the in-slice reference (`ExceptionPresenter.GetUserMessage` + Toast). Minor: `NotificationSettingsPage` per-row `@onchange` saves optimistically with no error channel or rollback (Tier 3, noted in inventory).
