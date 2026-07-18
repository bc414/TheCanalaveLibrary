# Slice 1 — Patterns Inventory (Foundation)

Headings per `modernization-audit/dimensions.md`. This is the foundation slice: several feature-facing dimensions have no in-slice call sites; each such section states what the slice *contributes* to the mechanism instead.

## 1. Pagination
mechanism: No paginated reads in slice. The slice owns the two shared pieces: `PagedResult<T>` (Core/Http — HTTP-boundary envelope for tuple returns, record, sealed) and the `PaginationControls` atom's tests (7-slot window, `aria-current`, guard-hides at ≤1 page).
exemplar: `TheCanalaveLibrary.Core/Http/PagedResult.cs:11`
deviations: none observed.

## 2. DTO mapping
mechanism: Direct single-step `.Select()` projection to positional records for the slice's one read service (`ThemeDto`); DTOs in slice are records (`ThemeDto`, `ThemeContext`, `PagedResult<T>`). Sprite/avatar rule honored: DTOs carry raw identifiers/stored URLs, never resolved sprite URLs.
exemplar: `Server/Sprites/ServerThemeReadService.cs:18-21` (`.Select(t => new ThemeDto(t.ThemeId, t.Name, null))`)
deviations: `ThemeDto.PreviewColor` is a wired-but-always-null slot ("add it when the swatch design is finalised") — speculative field, documented in-code.

## 3. Error surfacing
mechanism: The slice owns the whole error spine: `EndpointHelpers.ExecuteWriteAsync` (single exception→status table; validation matched by type-name suffix), `ClientHttpHelpers` (ProblemDetails detail/retryAfter extraction, empty-body-tolerant nullable reads), `ExceptionPresenter` tests, `CanalaveErrorBoundary`/`InlineAlert`/`ToastHost` tests, `#blazor-error-ui` in App.razor.
exemplar: `Server/Http/EndpointHelpers.cs:14-77`
deviations: `Error.razor` is template debris outside the discipline (MA-110); `ImageEndpoints` bare `Results.NotFound()` vs the bodied-result rule (MA-122); Moderation vs SiteSettings `RequireModerator` throw types map to 401 vs 403 (MA-123).

## 4. Form patterns
mechanism: n/a — no forms in slice (Identity pages are S6; feature forms in their slices). Slice contributes the antiforgery/pipeline ordering (`UseRateLimiter` after static files, before `UseAntiforgery` — Program.cs:492-493).
exemplar: `Server/Program.cs:490-494`
deviations: none observed.

## 5. Flyout/overlay mechanics
mechanism: n/a for production flyouts (atoms audited in S0). DesignGalleryPage carries the reference recipes: modal = `fixed inset-0 z-(--z-modal) bg-(--color-backdrop)` + `@onclick:stopPropagation` inner panel; dropdown = `bg-(--color-surface-raised) shadow-medium` panel. All z/shadow usage token-form.
exemplar: `SharedUI/DevTools/DesignGalleryPage.razor:204-216`
deviations: none observed.

## 6. Optimistic updates & debounce
mechanism: n/a — no debounced writes in slice. The typeahead debounce (300 ms default) is exercised by `CanalaveTypeaheadTests` with `DebounceMilliseconds=1` (test-speed idiom).
exemplar: `Tests.RazorComponents/CanalaveTypeaheadTests.cs:26` (`.Add(c => c.DebounceMilliseconds, 1)`)
deviations: none observed.

## 7. Disposal & lifecycle
mechanism: Uniform: `ServerWriteRateLimitService : IDisposable` disposes its partitioned limiter; `ProcessedImage` owns its MemoryStream; test fixtures implement `IAsyncLifetime` with container disposal; `TestAppFactory.Dispose` best-effort temp-webroot cleanup (commented IOException swallow). `TelemetryCircuitHandler` caches per-circuit state without disposal needs.
exemplar: `Server/Security/ServerWriteRateLimitService.cs:63` (`public void Dispose() => _limiter.Dispose();`)
deviations: MA-101 — ReconnectModal's JS module reference points at a stale physical path, so the disconnect-lifecycle UI never wires up (lifecycle-adjacent runtime defect).

## 8. Query shape
mechanism: Factory-per-method on both slice read services; write context plain scoped `AddDbContext`; all display filters on `ReadOnlyApplicationDbContext.OnModelCreating` only (four named filters closing over `_activeUser`); config = 20 cluster-grouped `IEntityTypeConfiguration<T>` files colocated in `Data/Configurations/`; explicit `.OnDelete` throughout; golden indexes named + comment-justified (USI seven filtered/covering indexes carry the H-03 lesson inline).
exemplar: `Server/SiteSettings/ServerSiteSettingsReadService.cs:17-26`
deviations: `User.Roles` phantom shadow FK on asp_net_roles (MA-102); unnamed `HasIndex(NormalizedEmail).IsUnique()` silently mutating Identity's EmailIndex (MA-103); index naming split — hot-path indexes use explicit `HasDatabaseName`, uniqueness constraints mostly rely on the snake_case convention's auto-name (consistent outcome, two idioms).

## 9. Write-method skeleton
mechanism: Only one write service in slice: `ServerSiteSettingsWriteService.SetIntAsync` = auth/role guard (`RequireModerator`) → load-or-create → `SaveChangesAsync`. Matches the canonical order (no rate limit — mod-only; no sanitize — int values). `ImageUploadProcessor` is the shared pre-write step for uploads: throttle → MIME fast-fail → capped buffer → sniff → bomb guard → decode → orient+strip → downscale → re-encode, in the documented order.
exemplar: `Server/Images/ImageUploadProcessor.cs:43-125`
deviations: none observed.

## 10. Endpoint & client shape
mechanism: Kebab-plural route groups (`/api/themes`, `/api/site-settings`); thin pass-throughs; writes wrapped in shared `ExecuteWriteAsync`; scalar body params carry explicit `[FromBody]`; read auth mirrors the consuming page (SiteSettings read carries inline `Roles = "Moderator,Admin"` with a documented rationale). Client side: `ClientHttpHelpers` centralizes detail extraction + nullable-read mapping (`""`/`"null"` → default); MVC-free `ProblemPayload` record.
exemplar: `Server/SiteSettings/SiteSettingsEndpoints.cs:37-57`
deviations: image-serving endpoint's bare `Results.NotFound()` (MA-122, GET-only so benign); `ImageEndpoints` is a serving route mapped conditionally on the S3 provider switch — unique but documented.

## 11. Sanitization & derived fields
mechanism: Slice owns the trust-boundary infrastructure, not call sites: `ServerHtmlSanitizationService` allow-list pinned by `HtmlSanitizationServiceTests` (13 tags, scheme filtering, target/rel normalization, style/class stripping); `SocialDescriptionHelper.Clean` (strip→decode→collapse→word-boundary truncate) as the Seo derived-field helper; DataSeeder hand-writes only allow-list HTML.
exemplar: `Tests.Unit/HtmlSanitizationServiceTests.cs` (whole suite)
deviations: none observed.

## 12. Notification triggering
mechanism: n/a — no notification call sites in slice. Slice contributes the seeded `NotificationType`/`NotificationCategory` catalogs (gap-numbered enum ↔ HasData rows, all 37 types present incl. Spotlight 90-92 and Polls 100).
exemplar: `Server/Data/Configurations/NotificationConfigurations.cs:66-133`
deviations: none observed.

## 13. Counter updates
mechanism: n/a — no counter mutations in slice. DataSeeder maintains counters by construction (documented header contract).
exemplar: `Server/Data/DataSeeder.cs:748-769`
deviations: seeder's `WordsWritten` values are hand-approximated, not computed (MA-109 note).

## 14. Test idioms
mechanism: Exemplary and uniform: GUID-suffixed seeding helpers with never-query-by-name discipline; absolute assertions off Respawn resets; `TablesToIgnore` for HasData lookups; settable fakes swapped in `TestAppFactory` (`FakeActiveUserContext`, pass-through `FakeWriteRateLimitService`, workers removed by type list); real-encoded image fixtures (`TestImages`, duplicated per tier by design — no shared test project); aria-label selectors and attribute-substring selectors for paren-form tokens in bUnit; telemetry tested via ActivityListener/MetricCollector/FakeLogger per the pilot pattern; purpose-named DI smoke (`HostBootTests`).
exemplar: `Tests.Integration/IntegrationTestBase.cs:57-113`
deviations: FakeActiveUserContext `Theme="Pokémon"` vs slug contract + 5-of-7 property copy in `SetActiveUser` (MA-119); PaginationControlsTests tautological loop + stale doc tokens (MA-120); missing ConfirmDialogTests / direct ClientHttpHelpers pin (MA-121).

## 15. Code economy
Product LOC in slice ≈ 7.3k (excl. generated package-lock and the 1.6k historical ReferenceSQL); owned test LOC ≈ 4.4k.

**(a) Per-cluster LOC + pattern-tax share:**
| Cluster | product | test | pattern-tax note |
|---|---|---|---|
| Data (contexts/configs/seeder) | ~3,030 | ~660 (ContentRating/DataProtection/ConcurrentRead/HostBoot + harness) | EF config is the tax the architecture buys deliberately (~1.9k, one graph); ~40 "Future indexes for querying..." filler comments |
| Program.cs + Client Program.cs | ~670 | — | ~260 LOC of mechanical AddScoped/Map* registration lines (see (e)) |
| Images | 613 | ~740 | near-zero waste; S3/Local duality is the documented provider seam |
| Security (Server+Core) | ~214 | ~420 | none |
| Diagnostics/Telemetry (Core+Server) | ~510 | ~160 | per-component nested classes are repetitive by design (metric-shape safety) |
| SiteSettings | ~221 | 0 integration | smallest full CQRS+endpoint+client stack — the marginal cost of one knob cluster ≈ 220 LOC + client impl (in S-other) |
| Sprites/Seo/Lookups/Http | ~800 | ~550 | none |
| Home + NotFound + DevTools | ~310 | 0 | DesignGallery (245) is dev-only reference, pays rent |
| Legacy folders (all projects) | ~290 | — | pure straggler weight (MA-105/112) |
| ServiceDefaults + AppHost | ~350 | — | template-standard |

**(b) Compression candidates (LOC saved / sites collapsed / machinery cost):**
- **Delete dead wiring** — Server/Pages pair + `AddRazorPages()` + comment (~30 LOC / 3 sites / zero machinery); `RedirectToLogin.razor` (10 / 1 / zero — unless wired into `<NotAuthorized>`, a small decision); unused Redis package ref (1 line / 1 / zero); `User.Roles` nav + phantom column (2 LOC + migration / 1 / one migration). **Classification: pure win** (all four).
- **HomeDesktop/HomeMobile merge** — the pair differs only in spacing (`p-8 gap-6 max-w-5xl` vs `p-4 gap-5`); responsive prefixes in one component save ~18 LOC and 2 files, cost: departs from the codebase's Desktop/Mobile-pair grammar that real (structurally different) pages use, weakening the "separate only when structurally different" signal by making readers check which kind each pair is. **Classification: trade.**
- **Unify the double-registration DI shape onto forwarding delegates** (MA-107) — no LOC change (±0 / 8 clusters / none); this is correctness-consistency, not compression, but folding it into any registration cleanup is free. **Classification: pure win** (shape-unification, not size).
- **`ImageEndpoints`/`ThemeEndpoints`-style micro-endpoint files** — could inline into Program.cs (~30 LOC saved / 2 files) at the cost of breaking the uniform `{Feature}Endpoints.Map{Feature}Endpoints` grammar every cluster shares. **Classification: false economy.**

**(c) Near-identical pairs:**
- `HomeDesktop`/`HomeMobile` — structural difference: none (spacing only); measured against the codebase's own "separate components only when structurally different" rule this pair fails the test today (it exists as a placeholder for the future WU-Home divergence). See (b).
- `TestImages` (Tests.Integration) / `TestImages` (Tests.Unit) — byte-identical ~50-LOC fixture classes. Collapsing requires a shared test-utility project (new csproj, solution wiring) for 50 LOC. **Classification: false economy — named and rejected.**
- `FakeActiveUserContext` (Integration) / `StubActiveUserContext` (Unit) — same shape, different defaults (and one carries the Theme contract bug, MA-119); same shared-project economics as above. **False economy to merge; fix the drift instead.**

**(d) Mechanical repetition with a fixable root cause:**
- Per-feature `ThrowIfWriteFailedAsync` copies across client services exist because the 13 validation-exception types share no base/constructor shape — root cause recorded in MA-008 (S0); the S1-visible half is `EndpointHelpers`' name-suffix matching (`Server/Http/EndpointHelpers.cs:74-76`). One marker interface would collapse both.
- ~40 "Future indexes for querying..." comments across config files — copy-paste filler; a single sweep deletes them (cosmetic).

**(e) False economies considered and rejected:**
- **Assembly-scanning/convention-based DI registration** to replace the ~260 explicit `AddScoped`/`Map*Endpoints` lines in the two Program.cs files: the explicit lists are load-bearing documentation (the L5 sweep's client-registration audit, the "deliberate structural exclusions" trailing comment, per-line WU rationale comments) and greppable; scanning would save lines and cost the exact visibility the Global Flip checklist depends on. Rejected.
- **Merging the two DbContexts or moving filters into services**: the read/write split + filters-on-read-context-only is the settled structural-safety axiom; any "simplification" here reopens the WU38 bug class. Rejected.
- **Caching SiteSettings reads**: explicitly rejected by convention ("No caching… mod edit takes effect on next read") — stays rejected.
