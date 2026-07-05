# Layer 5 — WASM Enablement

API endpoints, client-side service implementations, serialized auth state, render-mode topology.
This is a body-swap behind stable interfaces — DTO shapes, service method signatures, and
component props don't change. Only the method bodies behind the interfaces change.

Every pattern in this file is verified against built code in a real browser (reference
implementation: `Server/Tags/TagEndpoints.cs`, `Client/Tags/ClientTag{Read,Write}Service.cs`;
verification narrative: `workplan.md` WU-L5Pilot).

## Rollout Strategy (settled — do not revisit)

- **Per-feature service surface lands incrementally.** Endpoints + client impls are inert until
  a page renders in WASM and are fully testable headlessly (see §"Testing"), so they ship
  feature-by-feature with zero user-facing risk.
- **The render-mode conversion happens once, globally.** When every reachable page's services
  have client impls, `App.razor` flips to `InteractiveAuto` in a single pass, followed by one
  whole-site browser debug wave (§"The Global Flip"). No long-lived mixed-mode state: islands
  degrade UX (inert layout chrome, full-reload navigation), deliver nothing users need early,
  and each one is a standing forgotten-attribute circuit-crash hazard (§"The Island Recipe").
- Pages carry no `@rendermode` of their own in the resting state — before the flip they ride the
  global `InteractiveServer` mode; after it, the global `InteractiveAuto` mode (axiom 8: render
  mode is set once, on `<Routes>`/`<HeadOutlet>` in `App.razor`).

## How `InteractiveAuto` Works

- First visit: interactive via a **server circuit** while the WASM bundle downloads in the
  background. Subsequent visits (runtime cached): **WebAssembly**. After a deploy invalidates
  the cached bundle, users are back on the circuit path until re-download completes.
- The decision input is **client cache state, not code availability**. Auto never checks whether
  a service has a client registration — a component that injects an unregistered service during
  a WASM pass throws an unhandled DI exception in the browser. There is no server fallback.
  Auto is a delivery/latency optimization, not graceful degradation.
- Consequence: **both execution paths are live in production permanently** — server impls serve
  the circuit pass AND back the endpoints; client impls serve the WASM pass. The body-swap axiom
  is the production shape, not a migration convenience.
- Auto prefers to match the interactive runtime already active on the page, so one page never
  loads both runtimes — one more reason a half-converted site misbehaves rather than blends.

## API Endpoint Organization

Minimal-API endpoints in a feature-cluster extension class —
`Server/{Cluster}/{Feature}Endpoints.cs`, colocated with the server service impl it wraps
(SKILL.md "Code Organization"; never the deprecated flat `Server/Endpoints/` folder):

```csharp
// Server/Tags/TagEndpoints.cs — reference implementation
public static class TagEndpoints
{
    public static WebApplication MapTagEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/tags");

        group.MapGet("/directory", async (ITagReadService tags) =>
            Results.Ok(await tags.GetTagDirectoryAsync()));

        group.MapGet("/", async (ITagReadService tags, TagTypeEnum type) =>   // enum binds from query
            Results.Ok(await tags.GetTagsByTypeAsync(type)));

        group.MapGet("/chips/by-ids", async (ITagReadService tags, int[] ids) =>  // ?ids=1&ids=2
            Results.Ok(await tags.GetTagChipsByIdsAsync(ids)));

        group.MapPost("/", (ITagWriteService tags, CreateTagDto dto) =>
            ExecuteWriteAsync(async () => Results.Ok(await tags.CreateTagAsync(dto))));
        // ... PUT /{tagId:int}, DELETE /{tagId:int} — same ExecuteWriteAsync wrapper
        return app;
    }
}

// Program.cs
app.MapTagEndpoints();
```

- **Endpoints are thin pass-throughs.** No business logic, no auth logic — the service is the
  single enforcement point (`RequireMod()` etc.). The endpoint's only added job is the
  exception→status translation below.
- **Read endpoints:** public where the page is public; return the same DTOs the service returns.
- **Write endpoints:** return the service's return value as-is. A `string?` return crosses as a
  raw JSON string/`null` body — do NOT mint a wrapper DTO (Layer 5 never changes contracts).
- **High-frequency write endpoints ("fast and dumb"):** validate → `LPUSH` to Redis queue →
  return `202 Accepted`. Do NOT touch `DbContext`. See [layer7-redis.md](layer7-redis.md).
- **Cookie auth returns 401/403, not 302 redirects** — configured in `Program.cs`
  (`ConfigureApplicationCookie` `OnRedirectToLogin`/`OnRedirectToAccessDenied`); required so
  WASM API calls fail cleanly.

## The Error-Translation Contract

Components catch the service contract's **typed exceptions** (`TagValidationException` →
inline form error, etc.). That contract must survive the HTTP hop, so the boundary translates
symmetrically — endpoint maps exception→status, client impl maps status→exception:

| Contract exception | Status | Message channel |
|---|---|---|
| `{Feature}ValidationException` | 400 | `Results.Problem(detail: ex.Message, statusCode: 400)` — `ProblemDetails.Detail` carries the user-facing message verbatim; client rethrows it |
| `UnauthorizedAccessException` | 403 (401 if unauthenticated — cookie handler emits it before the service runs) | none |
| `KeyNotFoundException` | 404 — `Results.Problem(statusCode: 404)` | none |
| anything else | 500 (unhandled) | client surfaces `HttpRequestException` via `EnsureSuccessStatusCode()` |

**Every API error status must be a bodied result (`Results.Problem`), never a bare
`Results.NotFound()`/`Results.StatusCode(...)`.** The app's
`UseStatusCodePagesWithReExecute("/not-found")` re-executes **body-less** error responses into
the HTML not-found route **with the original HTTP method** — a PUT/DELETE re-executed against
that GET-only page surfaces as 405 to the client (regression net: `TagEndpointsTests`).
Bodied results are skipped by that middleware; success statuses (`Results.Ok`,
`Results.NoContent`) are unaffected.

Server side: wrap write handlers in one `ExecuteWriteAsync(Func<Task<IResult>>)` try/catch per
endpoint class. Client side: one `ThrowIfWriteFailedAsync(HttpResponseMessage)` helper that
reads `ProblemDetails.Detail` on 400 (deserialize a private `record ProblemPayload(string? Detail)`
— MVC's `ProblemDetails` type isn't referenced in the WASM client) and rethrows the mapped
exception.

## Client Service Implementations

Mirror the server inheritance structure (CQRS-lite: write inherits read):

```csharp
public class ClientTagReadService(HttpClient http) : ITagReadService
{
    protected HttpClient Http { get; } = http;   // C# primary-ctor params can't be shared with a
                                                 // subclass that also passes them to base — expose
                                                 // a protected property instead (avoids CS9107)

    public async Task<List<TagDirectoryGroupDto>> GetTagDirectoryAsync() =>
        await Http.GetFromJsonAsync<List<TagDirectoryGroupDto>>("api/tags/directory") ?? [];

    public async Task<List<TagChipDto>> SearchTagChipsAsync(TagTypeEnum type, string term)
    {
        if (string.IsNullOrWhiteSpace(term)) return [];   // mirror server short-circuits —
                                                          // no round trip for a contractual no-op
        return await Http.GetFromJsonAsync<List<TagChipDto>>(
            $"api/tags/chips/search?type={(short)type}&term={Uri.EscapeDataString(term)}") ?? [];
    }
}

public sealed class ClientTagWriteService(HttpClient http) : ClientTagReadService(http), ITagWriteService
{
    public async Task<TagSaveResult> CreateTagAsync(CreateTagDto dto)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/tags", dto);
        await ThrowIfWriteFailedAsync(response);          // status→exception translation
        return (await response.Content.ReadFromJsonAsync<TagSaveResult>())!;
    }
}
```

- **Registration** (`Client/Program.cs`): `AddScoped<ITagReadService, ClientTagReadService>()` +
  `AddScoped<ITagWriteService, ClientTagWriteService>()` beside the existing same-origin
  `HttpClient` registration.
- **Cookie auth is free:** WASM's fetch-backed `HttpClient` sends the Identity cookie on
  same-origin requests automatically. No token plumbing, no auth headers.
- **Server-side convenience wrappers** (e.g. `GetAllGenreTagsAsync` delegating to
  `GetTagsByTypeAsync`) are mirrored as client-side delegation to the same endpoint — don't
  mint an endpoint per wrapper.
- **Client impls may carry extra responsibilities** beyond the server impl: local caching /
  session-lifetime memoization, optimistic UI updates, connection-status checks. Add
  per-feature only as justified.

## Authentication in WASM

The .NET 9+ serialization pair — **not** the .NET 8 template's hand-written
`PersistentAuthenticationStateProvider` (obsolete; do not port it):

```csharp
// Server Program.cs
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization(options => options.SerializeAllClaims = true);

// Client Program.cs
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();
```

- **`SerializeAllClaims = true` is required for this project.** The default serializes only
  name + role claims; our components read the custom claims baked by
  `ApplicationUserClaimsPrincipalFactory` (`canalave:theme`, `canalave:prefers_animated_sprites`
  → `ThemeContextProvider`). With it, `<AuthorizeView Roles="...">` evaluates inside the WASM
  runtime with no server round trip.
- Auth state reaches WASM components through client DI (`AddCascadingAuthenticationState`), not
  through Routes.razor's markup-level `<CascadingAuthenticationState>` — auth is therefore
  available in any interactive tree, including islands, without extra wiring.

## ThemeContext in WASM

`ThemeContextProvider` lives in **SharedUI** (`SharedUI/Sprites/`) so it can render in any
runtime. It reads the two sprite claims off the cascaded `Task<AuthenticationState>`, so with
`SerializeAllClaims` the resolved sprite URLs are byte-identical across prerender → interactive
in both modes — no flicker; the sprite `onerror` fallback chain (animated `.webp` → static
`.png` → `unknown.png`) behaves identically under WASM. Under global interactivity the
Routes-level provider covers the whole tree. Cascading values do NOT cross a static→interactive
island boundary, so a page running as an island must wrap its own content in
`<ThemeContextProvider>` (or the client registers a root-level `AddCascadingValue`).

## Avoiding the double fetch: `[PersistentState]` (.NET 10)

Interactive pages prerender, then the interactive pass re-runs `OnInitializedAsync` — refetching
and flashing "Loading…". Applies to InteractiveServer today and WASM after the flip. Persist the
prerendered data instead:

```csharp
[PersistentState]                                   // public property — required by the attribute
public List<TagDirectoryGroupDto>? Directory { get; set; }

protected override async Task OnInitializedAsync()
{
    Directory ??= await TagReadService.GetTagDirectoryAsync();   // ??= — restore-or-fetch idiom
}
```

Under WASM this means zero HTTP calls on first paint — the island hydrates entirely from the
persisted payload. This is the declarative .NET 10 replacement for the manual
`PersistentComponentState` API — don't hand-roll the register/dispose dance. (`??=`-vs-plain
assignment gotcha: `layer3-logic.md`.)

## The Island Recipe (available technique — not a resting state)

A single page can run as a WASM island inside the otherwise-InteractiveServer app. Use this
only as a debug bisect during the flip wave (isolate one page's WASM behavior) or a deliberate
short-lived staged rollout — never leave pages parked here. Two directives, **both required**:

```razor
@attribute [ExcludeFromInteractiveRouting]
@rendermode RenderMode.InteractiveWebAssembly
```

- `@rendermode` (full `RenderMode.` form — SharedUI's `_Imports` has no `@using static`) makes
  the page an interactive-WASM island. Use `InteractiveWebAssembly`, not `Auto` — an island
  exists to *prove* the WASM path; Auto silently falls back to a server circuit on first visit.
- `[ExcludeFromInteractiveRouting]` does two jobs:
  1. `App.razor`'s `AcceptsInteractiveRouting()` check returns `null` for this route, so
     `<Routes>`/`<HeadOutlet>` render static SSR and the page's own `@rendermode` can take
     effect — differing interactive render modes can't nest; without this the request throws.
  2. It makes the interactive-server router on every other page perform a **full-document
     navigation** to this route instead of rendering it in-circuit. Without it, an in-app nav
     link to the island page **crashes the circuit** — and the failure is invisible until
     someone clicks the link. (Same mechanism the Identity pages use.)
- Island consequences: the layout renders static SSR around the island (per-page render modes
  can't make layouts interactive — `@Body` is a `RenderFragment` and can't serialize), so
  layout chrome interactivity is inert on that page; `PageTitle` applies at prerender only;
  Routes-level cascading values don't reach the island (wrap `ThemeContextProvider` per page);
  services resolve to Server impls during prerender and Client impls in the WASM runtime.

## The Global Flip (checklist)

Prerequisite: every feature's endpoints + client impls built and registered. Then, in one pass:

1. Move `Routes.razor` — and anything it references that lives in Server (`NotFound` page) —
   into a client-reachable assembly (Client or SharedUI); keep `AdditionalAssemblies` correct on
   both the `Router` and `MapRazorComponents`.
2. `App.razor`: `PageRenderMode` → `InteractiveAuto` (the `AcceptsInteractiveRouting()` guard
   stays — Identity pages remain static SSR).
3. Sweep every component reachable by the interactive router for services with no client
   registration — one miss is a browser crash on any cached-runtime visit. Known instance:
   `DevLoginBar` injects `IHostEnvironment`, which WASM DI does not register (it registers
   `IWebAssemblyHostEnvironment`) — needs a client-side registration or an adapter.
4. Adopt `[PersistentState]` page-by-page (without it every page double-fetches on hydration —
   under WASM that's a visible flash plus redundant HTTP).
5. Run a whole-site browser debug wave (L4.5 band, `run-server/SKILL.md`), verifying **which
   runtime actually ran**: network log shows `_framework/*.wasm` fetches and console messages
   source from `dotnet.runtime.js` under WASM. This check outlives the flip — under Auto, "the
   page works" never says which path executed, and a WASM-path regression can hide behind a
   working circuit path indefinitely. To force the WASM path deterministically while debugging,
   island the page with `InteractiveWebAssembly` (recipe above).

## Testing the Layer-5 surface

- **Integration tier:** `{Feature}EndpointsTests` drive the real HTTP surface via
  `Factory.CreateClient()` — routing, model binding (enum-from-query, repeated-key arrays),
  status mapping, ProblemDetails bodies, payload round trips. The fake `IActiveUserContext`
  flips mod/non-mod per test exactly as in service tests. See `TagEndpointsTests`.
- **Unit tier:** `Client{Feature}ServiceTests` construct the client impls over a canned
  `HttpMessageHandler` (no host) and pin URL/verb shapes and the status→exception translation.
  `Tests.Unit` references the Client project for this (see testing.md "Project setup reference").
- **Browser band (L4.5):** WASM boot, `[PersistentState]` hydration, serialized auth, and
  cross-navigation are real-circuit behavior the automated tiers can't see; verify per
  `run-server/SKILL.md`, including the which-runtime-ran check above.

## The Contract Boundary

The vertical-line test: can this feature's Layer 1–4 contract be fully defined now, with *some*
correct implementation behind it, such that Layer 5 only changes what's *behind* the contract?

Layer 5 is naturally batchable: the same endpoint + `HttpClient` wrapper pattern applies to N
stable interfaces. The Phase-4 batch (middle_plan.md item 6) applies it feature-by-feature
(headless), then §"The Global Flip" closes the layer.

## Avoid

- Injecting `DbContext`, a concrete service, or `HttpClient` directly into a component.
- `@rendermode` on a page **without** `[ExcludeFromInteractiveRouting]` — compiles fine, then
  crashes the circuit on in-app navigation to the page.
- Leaving pages parked as islands — the island recipe is a debugging/rollout technique, not an
  architecture (see Rollout Strategy).
- Porting the .NET 8 template's `PersistentAuthenticationStateProvider` — superseded by
  `AddAuthenticationStateSerialization`/`Deserialization`.
- Minting wrapper DTOs for scalar returns, or new endpoints for server-side convenience
  wrappers — Layer 5 never changes the service contract.
- Bare `Results.NotFound()`/body-less error statuses in API endpoints (405-via-re-execute trap —
  see §"The Error-Translation Contract").
- Referencing MVC's `ProblemDetails` type in the Client — deserialize the one field you need.
- Creating an HTTP-based `IDistributedCache` for the WASM client (rejected: extreme overhead).
- Instantiating rich domain models on the client for validation reuse (WASM payload bloat).
