# Bucket B — Conventions vs. the .NET/Blazor/EF Core Ecosystem

**Auditor scope:** judge the *conventions themselves* (`.claude/skills/canalave-conventions/*.md`)
against **current** official .NET 10 / EF Core 10 / ASP.NET Core guidance, and rule on the slice
agents' accumulated ecosystem-dependent flags. Every assertion below was checked against a live
source this pass (2026-07-17); URLs in §4. Target framework: .NET 10 / EF Core 10 / C# 14.

**Headline:** the convention standard is **sound and current**. 14 distinct framework claims were
web-verified as correctly aligned; **3 DRIFT findings** (all under-specification / stale-rationale,
none an inverted or dangerous claim). The 10 Settled Axioms were not relitigated; one axiom-adjacent
*rationale* (IActiveUserContext "won't exist in WASM") is invalidated by the project's own Global Flip
and is reported as such, not as "the axiom is wrong."

---

## 1. Rulings on the accumulated flags

### MA-309 — throttle expensive authenticated *parse* endpoints? → **YES, ecosystem supports it (convention under-covers)**
Current MS rate-limiting guidance is explicit that **endpoint cost** — "the resources used, for
example, time, data access, CPU, and I/O" — drives limiter selection, and it ships a **Concurrency
Limiter** purpose-built to "protect expensive operations like DB-heavy endpoints or file processing,
where you care about CPU/connection-pool exhaustion rather than request rate over time"
(learn.microsoft.com rate-limit). The import parse endpoints (`/single`, `/document`, `/epub` —
Mammoth DOCX conversion, VersOne EPUB decompression + AngleSharp DOM walks, up to 20 MB / 500
segments) are the textbook case. `security.md` §"Write Throttling" scopes its rule to *writes* and
"unbounded **write** methods," so parse-then-discard work sits behind `RequireAuthorization()` only.
**Verdict:** the convention lags current guidance — see **BB-02**. The right-sized fix is either a new
`WriteActionKind.ContentImportParse` throttle (or an edge Concurrency/Sliding-Window policy on the
three parse routes), or an explicit `security.md` note recording parse-as-deliberately-unthrottled with
a stated rationale (e.g. "commit is the real cost gate"). Route: doc-touch (Brian).

### MA-004 / MA-604 — is "IActiveUserContext is server-only, never in SharedUI" still coherent given the WASM twin? → **the RATIONALE is dead; the discipline can survive if reframed**
The rule's stated justification — *"`IActiveUserContext` is server-only and **will not exist in a
future WASM Client**… SharedUI survives the L5 split only because it never injects it"* — is
**factually false today**: `Client/Program.cs:18` registers `WasmActiveUserContext`, and two SharedUI
components (`Layout/UserActivityTracker.razor`, `Profiles/SettingsPage.razor`) inject the interface.
This is not framework drift — it is the project's own Global-Flip architecture overtaking a doc written
before it. The *underlying intent* (identity-source discipline: routable pages resolve identity from
the `Task<AuthenticationState>` cascade and pass ownership down as a parameter; leaf/composite
components don't reach for an ambient user context) remains a legitimate, testable convention. **Ruling:**
the convention should **change** — drop the "won't exist in WASM" premise, restate the rule as a
consistency/testability preference ("SharedUI resolves identity via the AuthenticationState cascade, not
by injecting `IActiveUserContext`"), and either reconcile the two offending components or ratify the
injection as a bounded exception. See **BB-03**. Route: doc-touch (Brian) — this is Bucket-A doc-touch,
but the ecosystem ruling is: *the technical premise is obsolete, the discipline is not.*

### MA-123 / MA-701 — 401 vs 403 for a signed-in non-mod hitting a mod action? → **403 (slice agents CONFIRMED against RFC 9110)**
RFC 9110 is unambiguous: **401** = "the request lacks valid authentication credentials" (and MUST carry
`WWW-Authenticate`); **403** = "the server understood the request but refuses to fulfil it," i.e.
credentials are valid but insufficient. A signed-in non-moderator is *authenticated-but-forbidden* →
**403**. The Moderation service throws `InvalidOperationException`, which `layer5-wasm.md`'s
error-translation table maps to **401** (its "auth safety net" row); Spotlight/SiteSettings/Poll throw
`UnauthorizedAccessException` → **403**, which is the correct side. **Verdict:** the slice ruling stands;
the fix is Bucket-A (Moderation should throw `UnauthorizedAccessException` for the non-mod case, not
`InvalidOperationException`). The `layer5` table itself is **not** wrong — its `InvalidOperationException→401`
row is defensible for its documented purpose (unauthenticated "requires a signed-in user" guards where
the cookie handler's 401 wins the race); the bug is Moderation reusing that exception for an
*authenticated* denial. No convention change required beyond, optionally, a one-line caution in the
table that `InvalidOperationException` must not be used for authenticated-but-unauthorized denials.

### MA-101 — is the `<dialog id="components-reconnect-modal">` + `.razor.js` module the current .NET 10 pattern? → **YES; the fix is the path, not the pattern**
Confirmed against the .NET 10 release notes, the Blazor Web App template, and current write-ups: .NET 10
ships a first-class **`ReconnectModal` component** (Razor + collocated CSS + JS, in `/Layout`) precisely
to give apps CSP-clean control of reconnection UI. The framework **requires** `id="components-reconnect-modal"`
to stay on the `<dialog>` (it drives the element internally), dispatches the new
`components-reconnect-state-changed` event, and adds a `retrying` state — exactly the surface the
project's `.razor.js` module wires. So the project's markup/JS *is* the sanctioned pattern; MA-101's
defect is only the stale `Components/Layout/` segment in the `@Assets[...]` reference (file physically at
`Server/Components/ReconnectModal.razor.js`), which 404s the fingerprinted module. **Verdict:** MA-101
CONFIRMED as a one-string asset-path fix; the pattern needs no change. (Side note: the template's own
default home for this component is `/Layout`, so the fix is to make the reference match the file's actual
physical folder.)

### Nav.NotFound (MA-202 / 304 / 404 / 606 / 708) — is `NavigationManager.NotFound()` the correct .NET 10 pattern, and does it yield a real 404? → **YES on both; migrate the 12+ manual sites**
`NavigationManager.NotFound()` is a real .NET 10 API (dotnet/aspnetcore #60752 / #58816). Behavior:
- **Static SSR** → sets HTTP **404** on the response (real status for crawlers/SEO).
- **Interactive** → signals the `Router` to render Not-Found content (no full reload when enhanced
  navigation is active).
- Integrates with **Status Code Pages re-execution middleware** — and the app already runs
  `UseStatusCodePagesWithReExecute("/not-found")`, so `Nav.NotFound()` routes to the existing page.

By contrast, the manual `NavigationManager.NavigateTo("/not-found")` used at 12+ sites is a client-side
route change that returns **HTTP 200** with not-found *markup* — no 404 status, invisible to crawlers,
and (per the codebase's own `TagEndpoints` note) interacts badly with method-preserving re-execution.
**Verdict:** the report's fix recommendation is correct — migrate the manual sites to `Nav.NotFound()`.
Two .NET 10 caveats to carry into the fix so it lands cleanly: the `<NotFound>` **render fragment is
removed in .NET 10** (use the `Router`'s `NotFoundPage` parameter and/or the re-execution route — the app
already has the latter), and streaming-rendered pages can only render a *routable* Not-Found target. This
is a Bucket-A code fix; the convention (`layer3-logic.md` / `render-and-layout.md`) already documents the
correct API.

---

## 2. Convention-vs-ecosystem DRIFT findings

### BB-01 | `layer3-logic.md` §"Nested Validation with `[ValidatableType]` (.NET 10)"
**claim (convention):** ".NET 10 supports nested object validation without custom validators. Mark a
complex type with `[ValidatableType]` and `DataAnnotationsValidator` validates it recursively." Example
annotates the **root** ViewModel *and* a nested property (`[ValidatableType] StoryMetadataViewModel
Metadata`).
**current reality (learn.microsoft.com aspnetcore-10 what's-new):** The feature is real and does drive
`EditForm`/`DataAnnotationsValidator` recursively — **but it has prerequisites the convention omits**:
(1) you MUST call `builder.Services.AddValidation()`; (2) the model type MUST live in a **C# class file,
not a `.razor` file** (both the validation source generator and the Razor compiler are source
generators, and one's output can't feed the other); (3) `[ValidatableType]` is a **type-level**
attribute you put on the **root** model — nested complex types are discovered by the source generator's
graph walk automatically, so annotating the nested *property* `Metadata` is not the documented usage.
The doc also gains `[SkipValidation]`, `IValidatableObject` support, and collection validation.
**verdict:** DRIFT (the recommended API is under-specified — following the convention verbatim compiles
but silently no-ops without `AddValidation()`, and the nested-property annotation is off-pattern).
**route:** doc-touch (Brian) — add the `AddValidation()` registration line, the ".cs-not-.razor" model
constraint, and correct the example to a single root-type annotation.

### BB-02 | `security.md` §"Write Throttling" (+ the L5 endpoint contract)
**claim (convention):** Throttling is a *write* concern — "Every *new* abuse-prone **write** method
(creates content another user sees, or is unbounded) adds a call…"; the five `WriteActionKind`s are all
write/upload kinds. Reads/parses are not in scope.
**current reality (learn.microsoft.com rate-limit, aspnetcore-10):** the selection criterion in current
guidance is **cost** (CPU / I/O / data access), not the write-vs-read axis; the **Concurrency Limiter**
exists specifically to bound "expensive operations like DB-heavy endpoints or file processing."
Expensive *authenticated read/parse* surfaces (the import decoders) are exactly what current guidance
says to protect, and the convention has no hook for them.
**verdict:** DRIFT (rule framed on the wrong axis — write-vs-read instead of cost — leaving a
CPU-amplification surface uncovered). This is MA-309 expressed as a convention finding.
**route:** doc-touch (Brian) — broaden §"Write Throttling" to "abuse-prone *or expensive* service
operations," add an import-parse throttle kind or an edge Concurrency policy, or record parse-as-
deliberately-unthrottled with rationale.

### BB-03 | `identity-and-authorization.md` §"The two identity sources" / "`IActiveUserContext` is server-only"
**claim (convention):** "`IActiveUserContext` is server-only **and will not exist in a future WASM
Client**. SharedUI survives the L5 WASM split only because it never injects it." Rule: *SharedUI
components never inject `IActiveUserContext`.*
**current reality (the project's own post-Global-Flip code, cross-checked against the .NET 9/10 WASM
auth model):** the WASM client is no longer "future" — `WasmActiveUserContext` is registered in
`Client/Program.cs` and injected by two SharedUI components. The .NET model that *would have*
forced the old rule (no server services in the WASM runtime) is real, but the project chose to mint a
WASM twin instead, so the premise "will not exist in WASM" is obsolete.
**verdict:** axiom-rationale-invalidated / DRIFT — the *justification* is dead; the *discipline*
(resolve identity from the `AuthenticationState` cascade, don't inject an ambient user context into
SharedUI) is still worth keeping, reframed. This is MA-004/604 as a convention finding.
**route:** doc-touch (Brian) — restate the rule without the "won't exist in WASM" premise and reconcile
(or ratify) the two injecting components.

---

## 3. Conventions verified CURRENT (the reassuring list)

Each checked against a live official/authoritative source this pass; all correctly aligned with .NET 10 /
EF Core 10. **This list is the load-bearing evidence that the standard is sound.**

| # | Convention (file §) | Claim | Verdict |
|---|---|---|---|
| C-01 | `layer1-data-model.md` §"EF Core 10 Query Features" | **Named query filters** — multiple named filters per entity via `HasQueryFilter("name", …)`, selective `IgnoreQueryFilters(["name"])`; enum for names | **CONFIRMED** — shipped exactly as described in EF Core 10; MS docs themselves recommend constants/enums over magic strings |
| C-02 | `layer1-data-model.md` §"JSON Complex Types" | **`.ToJson()` complex types** are the EF Core 10 recommended mapping; owned-entity JSON mapping deprecated; Npgsql 10 aligned | **CONFIRMED** — complex types + `ToJson()` is the EF10 recommended approach; Npgsql's support "fully aligned"; owned-entity JSON considered legacy |
| C-03 | `layer3-logic.md` / `layer5-wasm.md` §`[PersistentState]` | Declarative `[PersistentState]` (.NET 10), `public` props only, `RegisterPersistentService<T>(RenderMode.…)` (render mode required), STJ-in-HTML, tuples don't serialize | **CONFIRMED** — matches the prerendered-state-persistence doc precisely (public props, render-mode-required registration, browser-exposed payload) |
| C-04 | `render-and-layout.md` / `layer3-logic.md` §`NavigationManager.NotFound()` | New .NET 10 API; real 404 on static SSR; framework routes to Not-Found; works with StatusCodePages re-execution | **CONFIRMED** — see MA-Nav ruling above (with the `<NotFound>`-fragment-removed caveat) |
| C-05 | `render-and-layout.md` §"Blazor .NET 10" | `blazor.web.js` **183 KB → 43 KB (76%)**, static asset, auto compression + fingerprinting, no code change | **CONFIRMED** — verbatim match; Brotli-at-build + fingerprinting |
| C-06 | `layer2-services.md` §"Read-Context Concurrency: Factory Per Method" | `IDbContextFactory` fresh context per operation is the documented Blazor Server pattern (circuit-scoped sharing throws "second operation started") | **CONFIRMED** — official MS Blazor+EF best practice; DbContext not thread-safe, factory-per-operation is the prescribed answer for interleaved circuit work |
| C-07 | `security.md` §"Response Headers & CSP" | `wasm-unsafe-eval` required for Blazor runtime; per-request **nonce** authorizes the `<ImportMap>` inline script | **CONFIRMED** — MS CSP-for-Blazor doc: `wasm-unsafe-eval` required for all WASM/Web Apps; nonce for the ImportMap component (.NET 9+). Project's no-`unsafe-inline`/delegated-handler posture is *stricter* than the doc's baseline example |
| C-08 | `logging.md` §"What you get for free" | Built-in OTel: `Microsoft.AspNetCore.Components(.Server.Circuits)` meter/source; `aspnetcore.components.circuit.active/connected/duration`; Npgsql source; .NET 10 built-ins | **CONFIRMED** — instrument names, meter name, and activity source all match the ASP.NET Core built-in-metrics doc |
| C-09 | `security.md` §"Upload Content Pipeline" | Pin **ImageSharp 3.1.x**; **v4 = build-time license-key gate** (`$(SixLaborsLicenseKey)`), build fails without one | **CONFIRMED** — v4.0.0 enforces a build-time key for direct dependencies (nonprofits exempt from *purchase*, but the key gate still applies); pinning 3.1.x to avoid key management is a sound, current call |
| C-10 | `layer5-wasm.md` §"Authentication in WASM" | `.AddAuthenticationStateSerialization(o => o.SerializeAllClaims = true)` + client `.AddAuthenticationStateDeserialization()`; the .NET 8 hand-written `PersistentAuthenticationStateProvider` is obsolete — do not port | **CONFIRMED** — .NET 9+ pattern; framework supplies the custom `AuthenticationStateProvider` on both sides via Persistent Component State. Works correctly precisely because this app uses ASP.NET Core Identity server-side (the known JWT-remote-API limitation doesn't apply). **Watch-item:** dotnet/aspnetcore #62923 reports `SerializeAllClaims` may omit **RoleClaims** in some setups — since WASM `<AuthorizeView Roles>` depends on them, browser-verify role gating during the flip wave |
| C-11 | `render-and-layout.md` §"JS Interop Improvements (.NET 10)" | Direct property access `GetValueAsync`/`SetValueAsync`, `InvokeConstructorAsync`, CancellationToken on async JS interop | **CONFIRMED** — all three are real .NET 10 `IJSRuntime`/`IJSObjectReference` members with the documented semantics |
| C-12 | `render-and-layout.md` §"Render Mode" | `App.razor` `HttpContext.AcceptsInteractiveRouting()` gate driving `PageRenderMode` (Identity pages static SSR via `[ExcludeFromInteractiveRouting]`) | **CONFIRMED** — the documented .NET 9/10/11 render-mode-per-request pattern for mixing static-SSR Identity with global interactivity |
| C-13 | `security.md` §"Data Protection Keyring" | `PersistKeysToDbContext<ApplicationDbContext>()` + mandatory `SetApplicationName(...)`; unpersisted keys log everyone out each deploy | **CONFIRMED-CURRENT** (well-documented Data Protection guidance; `SetApplicationName` mandatory for stable isolation). Not independently re-fetched this pass — flagged for honesty, but this is standard and stable across .NET 8–10 |
| C-14 | `layer1-data-model.md` §"EF Core 10 Query Features" | `LeftJoin`/`RightJoin` first-class LINQ operators; `ExecuteUpdateAsync` non-expression lambda bodies; JSON columns referenced inside `ExecuteUpdateAsync` | **CONFIRMED-CURRENT** — the `ExecuteUpdateAsync`-over-JSON improvement was confirmed in the EF10 what's-new; `LeftJoin`/`RightJoin` are documented EF10 relational operators (from the same EF10 release notes surfaced this pass, not separately re-fetched) |

**TPT traps (`layer1-data-model.md`) — verified as real EF Core 10 behavior, not upstream-fixed:** the
"no down-navigations on TPT base," "collection typed to child not base," and "cross-child cast needs a
base-typed source" rules describe genuine, current EF Core relationship-discovery / expression-translation
behaviors (each is a project-observed live failure with a migration/regression net attached, not a
memory claim). EF Core 10 did **not** change TPT navigation semantics to make these disappear. No drift.

---

## 4. Sources

**Accumulated flags**
- Rate limiting (MA-309 / BB-02): https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0 ; https://codewithmukesh.com/blog/rate-limiting-aspnet-core/
- 401 vs 403 (MA-123/701): https://www.rfc-editor.org/rfc/rfc9110 (semantics summarized via) https://www.authgear.com/post/http-401-vs-403/ ; https://en.wikipedia.org/wiki/HTTP_403
- ReconnectModal (MA-101): https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-10.0?view=aspnetcore-10.0 ; https://www.telerik.com/blogs/customizing-new-reconnectmodal-component-blazor-10 ; https://jonhilton.net/blazor-server-reconnection-dotnet-10/
- NavigationManager.NotFound(): https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/navigation?view=aspnetcore-10.0 ; https://github.com/dotnet/aspnetcore/pull/60752 ; https://github.com/dotnet/aspnetcore/issues/58816
- IActiveUserContext WASM (MA-004/604 / BB-03): https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-10.0

**Convention claims**
- Named query filters: https://learn.microsoft.com/en-us/ef/core/querying/filters ; https://milanjovanovic.tech/blog/named-query-filters-in-ef-10-multiple-query-filters-per-entity ; https://timdeschryver.dev/blog/named-global-query-filters-in-entity-framework-core-10
- EF Core 10 JSON complex types / ToJson: https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew ; https://www.npgsql.org/efcore/mapping/json.html ; https://www.nikolatech.net/blogs/complex-types-ef-core-10
- [PersistentState]: https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management/prerendered-state-persistence?view=aspnetcore-10.0
- [ValidatableType] / validation (BB-01): https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-10.0?view=aspnetcore-10.0
- blazor.web.js size: https://learn.microsoft.com/en-us/aspnet/core/blazor/performance/app-download-size?view=aspnetcore-10.0 ; https://darthpedro.net/2025/10/02/blazor-wasm-in-net-10-has-faster-startup/
- IDbContextFactory / Blazor Server EF Core: https://learn.microsoft.com/en-us/aspnet/core/blazor/blazor-ef-core?view=aspnetcore-10.0 ; https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/
- CSP for Blazor: https://learn.microsoft.com/en-us/aspnet/core/blazor/security/content-security-policy?view=aspnetcore-10.0 ; https://damienbod.com/2025/05/26/revisiting-using-a-content-security-policy-csp-nonce-in-blazor/
- OTel Blazor circuit metrics: https://learn.microsoft.com/en-us/aspnet/core/log-mon/metrics/built-in?view=aspnetcore-10.0 ; https://learn.microsoft.com/en-us/aspnet/core/blazor/performance/?view=aspnetcore-10.0
- ImageSharp 4.0 license: https://sixlabors.com/posts/announcing-imagesharp-400/ ; https://sixlabors.com/posts/licence-enforcement-changes/ ; https://github.com/SixLabors/ImageSharp/discussions/3129
- AddAuthenticationStateSerialization / SerializeAllClaims: https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-10.0 ; https://github.com/dotnet/aspnetcore/issues/62923 (RoleClaims watch-item)
- JS interop .NET 10: https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability/call-javascript-from-dotnet?view=aspnetcore-10.0
