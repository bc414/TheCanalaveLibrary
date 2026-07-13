# Layer 5 — WASM Enablement

API endpoints, client-side service implementations, serialized auth state, render-mode topology.
This is a body-swap behind stable interfaces — DTO shapes, service method signatures, and
component props don't change. Only the method bodies behind the interfaces change.

Every pattern in this file is verified against built code in a real browser (reference
implementation: `Server/Tags/TagEndpoints.cs`, `Client/Tags/ClientTag{Read,Write}Service.cs`;
verification narrative: `workplan.md` WU-L5Pilot).

## Rollout Strategy (settled — do not revisit)

- **The mechanical add pass vs. the incremental philosophy.** The rule below ("ships
  feature-by-feature") describes how L5 cells earn Stage 5 (built AND verified). WU-L5Sweep
  (2026-07-12) is a distinct, one-time **add-without-verify** batch: endpoints + client impls for
  every remaining `ServerXXXService` were authored in one mechanical pass so the codebase is
  flip-ready, explicitly *without* the per-feature Integration/Unit tiers or browser verification
  that normally accompanies a feature reaching Stage 5. Grid cells touched by the sweep do **not**
  move to Stage 5 — see §"L5 Stage Semantics" below. The feature-by-feature *verification* wave
  (tests + browser pass) remains future work, done per-feature or in the Global Flip's debug wave.
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

## L5 Stage Semantics (corrects a grid mistake — 2026-07-12)

`status.md`'s L5 column had drifted to mean two different things under the same number. Groups
(F38–F40) and Recommendations (F27–F30) carried L5 **Stage 5** sourced from `GroupServiceTests`/
`RecommendationWriteServiceTests` — Integration-tier proof the **service** is sound — with **no
endpoints or client impl ever built**. Tags (F11–F13) and Tag Directory (F34) carry the same
Stage-5 number for the *actually-built-and-browser-verified* WASM surface (WU-L5Pilot). Both can't
be Stage 5 under CLAUDE.md's definition ("Aligned, sound, compiles" — L5 soundness means the HTTP
body-swap compiles, not that the service behind it does).

**Ruling:** L5 Stage 5 means an endpoint class + client impl exist, compile, and are registered.
Service-layer soundness alone (no HTTP surface) is **Stage 2** — "intent settled, no plan/code" for
this layer specifically, same as every other not-yet-built L5 cell. The Groups/Recommendations
cells were corrected to 2 in the same pass that reconciled this section (`status.md`, audit Stage
notes). Layer 5 Stage 5 still does **not** imply Stage-5 *verification* per WU-L5Sweep's
add-without-verify pass above — see that bullet for the distinction between "built" and "verified."

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

**One endpoints class + one route group per *interface*, not per cluster.** A cluster folder with
a read/write pair (`IFooReadService`/`IFooWriteService`) gets ONE `FooEndpoints.cs` mapping both;
a cluster with several independent interfaces (e.g. Stories: `IStoryReadService`/
`IStoryWriteService`, `IStoryArcReadService`/`IStoryArcWriteService`,
`IStoryLineageReadService`/`IStoryLineageWriteService`) gets one endpoints class **per interface
pair**, all colocated in the same cluster folder. This is the one hard correctness contract of the
layer: the client impl hardcodes the route as a string literal, so endpoint and client path must
agree character-for-character — there is no compile-time check tying them together.

Path = `/api/{kebab-plural-entity}`, matching the *entity* the interface is about, not the
interface name verbatim. Canonical list for names that aren't a mechanical pluralization:

| Interface(s) | Route |
|---|---|
| `IStoryArcRead/WriteService` | `/api/story-arcs` |
| `IStoryLineageRead/WriteService` | `/api/story-lineage` |
| `ISeriesRead/WriteService` | `/api/series` |
| `ISavedTagSelectionRead/WriteService` | `/api/saved-tag-selections` |
| `IViewCountWriteService` | `/api/view-counts` |
| `IReadingProgressWriteService` | `/api/reading-progress` |
| `IChapterReadMarkWriteService` | `/api/chapter-read-marks` |
| `IUserSettingsService` | `/api/user-settings` |
| `IUserProfileReadService` | `/api/user-profiles` |
| `IUserActivityWriteService` | `/api/user-activity` |
| `IManualTreeSearchReadService` | `/api/manual-tree-search` |
| `ITreeSearchReadService` | `/api/tree-search` |
| `IDiscoveryDefaultsReadService` | `/api/discovery-defaults` |
| `ICoOccurrenceReadService` | `/api/co-occurrence` |
| `ISiteDailyStatReadService` | `/api/site-daily-stats` |
| `IThemeReadService` | `/api/themes` |
| `IContentImportService` | `/api/content-import` |
| `IExportService` | `/api/export` |

Every other interface's entity name pluralizes mechanically (`IGroupReadService` → `/api/groups`,
`INotificationReadService` → `/api/notifications`, etc.) — don't add a table row for those.

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
- **Nullable-returning methods (`Task<T?>`) produce an EMPTY 200 body when null — the client
  must tolerate it.** ASP.NET's `HttpResultsHelper` skips serialization entirely for a null result
  value, under `Results.Ok(null)` AND `Results.Json(null)` alike (verified by curl during the
  Global Flip wave — `Content-Length: 0`; the "Results.Json writes literal null" assumption is
  FALSE, and TagEndpoints' `UpdateTagAsync` carried the same latent bug from the pilot, just never
  hit its null branch). The client's `GetFromJsonAsync<T?>` throws
  `JsonException: ExpectedJsonTokens` on an empty body — this crashed every StoryPage render for
  viewers with no read history (`GetViewerLastInteractionUtcAsync`, `DateTime?`). The contract:
  every client deserialization of a nullable return goes through
  `ClientHttpHelpers.GetNullableFromJsonAsync<T>` (reads) or
  `ClientHttpHelpers.ReadNullableFromJsonAsync<T>` (write responses), which map empty → null.
  18 read methods + `UpdateTagAsync` were converted in the wave. Server side keeps
  `Results.Ok`/`Results.Json` (they behave identically); non-nullable returns keep plain
  `GetFromJsonAsync`.
- **Write endpoints:** return the service's return value as-is. A `string?` return crosses as a
  raw JSON string/`null` body — do NOT mint a wrapper DTO (Layer 5 never changes contracts).
- **Buffered-signal ping endpoints ("fast and dumb"):** the endpoint calls the same
  `IXService` method as everywhere else — its server body is already an in-process buffer merge
  (no `DbContext`); return `202 Accepted`. See `layer2-services.md` §"Signal Buffering".
- **Cookie auth returns 401/403, not 302 redirects** — configured in `Program.cs`
  (`ConfigureApplicationCookie` `OnRedirectToLogin`/`OnRedirectToAccessDenied`); required so
  WASM API calls fail cleanly.
- **Read auth is a per-endpoint judgment call, not mechanical.** Public iff the page(s) consuming
  the read are public (mirror the existing page's `[Authorize]`/anonymous status); otherwise
  `RequireAuthorization()`. Write endpoints additionally take `RequireRateLimiting(...)` per
  `security.md` "Write Throttling" wherever the underlying service enforces a token bucket.
- **`CancellationToken` parameters are dropped at the client boundary.** A handful of read methods
  (`IManualTreeSearchReadService`, `ITreeSearchReadService`) take a `CancellationToken ct = default`
  for internal query cancellation. Endpoints may bind ASP.NET's own request-aborted token to the
  service call; the client impl simply never threads one through — `HttpClient` has its own
  cancellation path via `CancellationToken` overloads on `PostAsJsonAsync`/`GetFromJsonAsync` if a
  caller needs it later, but nothing plumbs the *service interface's* token across HTTP.

### Reads with non-scalar parameters: POST instead of GET

`MapGet` query binding only handles scalars, enums, and repeated-key arrays (`ITagReadService`'s
`type`/`term`/`ids`). Any read method taking a request/filter object —
`IStoryReadService.GetListingsAsync(StoryFilterDto, ...)`,
`IManualTreeSearchReadService.GetStoryNeighborsAsync(StoryNeighborsRequest, ...)`,
`ITreeSearchReadService.TraverseAsync(TreeSearchRequest, ...)`,
`IStoryReadService.FilterCandidateIdsAsync(ids, StoryFilterDto)` — is **not** GET-bindable.
Trigger: any parameter beyond scalar/enum/`int[]`/`IReadOnlyCollection<int>`.

```csharp
// Server: reads that take a request/filter object are POST, body-bound — never contorted into
// query-string binding just to keep the HTTP verb "correct." The operation is still read-only.
group.MapPost("/query", async (IStoryReadService stories, StoryFilterDto filter) =>
    Results.Ok(await stories.GetListingsAsync(filter)));

// Client: mirrors with PostAsJsonAsync, same as a write call minus ThrowIfWriteFailedAsync
// (a malformed filter DTO is a client bug, not a user-facing validation case — reads don't carry
// the write path's typed-exception contract).
public async Task<PagedResult<StoryListingDto>> GetListingsAsync(StoryFilterDto filter)
{
    HttpResponseMessage response = await Http.PostAsJsonAsync("api/stories/query", filter);
    response.EnsureSuccessStatusCode();
    return (await response.Content.ReadFromJsonAsync<PagedResult<StoryListingDto>>())!;
}
```

Sub-route naming: `/query` when the interface has exactly one such read; a short descriptive
segment (`/neighbors/story`, `/neighbors/user`, `/traverse`) when it has more than one — mirror the
method name, don't invent new vocabulary.

**Gotcha: array/collection parameters on non-GET handlers ALWAYS need explicit `[FromQuery]`.**
Minimal-API inference for a bare `int[]` is method- and sibling-dependent, and both failure modes
were hit live:

1. **Sibling body param → startup crash.** When a handler has one parameter already inferred as
   `[FromBody]` (the filter/request DTO), an unattributed `int[]` sibling resolves to an
   un-bindable source and **throws at app startup** (`GetListingsAsync`'s `restrictToStoryIds`,
   `FilterCandidateIdsAsync`'s `candidateIds` — WU-L5Sweep, caught by `dotnet test`, not `dotnet
   build`; `AuthorizationPolicyCache` builds every endpoint's metadata eagerly, so one bad handler
   took down the entire Integration tier).
2. **POST with no other body param → per-request 400.** On GET, a bare `int[]` infers as
   query-bound; on POST it infers as `[FromBody]` — so `PollEndpoints`' vote handler demanded a
   JSON body the client never sends and 400'd every vote ("Implicit body inferred for parameter") —
   found live in the Global Flip browser wave. This one compiles AND starts cleanly; it only fails
   when called.

Rule: on `MapPost`/`MapPut`/`MapDelete`, every query-bound array/collection parameter carries
`[FromQuery]` explicitly. On `MapGet` a single bare array is safe
(`ITagReadService.GetTagChipsByIdsAsync`'s `int[] ids` — the pilot's proven shape).

### Paged results: `PagedResult<T>`

Value-tuple returns (`Task<(T[] Items, int TotalCount)>` — Story/BlogPost/Group listings, 6 total)
don't round-trip named fields over `System.Text.Json`. The endpoint/client hop translates through
the shared `PagedResult<T>` record (`TheCanalaveLibrary.Core/Http/PagedResult.cs`) — construct it
server-side from the tuple, deconstruct client-side back to the tuple shape the interface expects.
This is a paging *envelope* at the HTTP boundary only, not a service-contract change — the
interface signature itself stays a tuple:

```csharp
// Server
group.MapGet("/", async (IGroupReadService groups, int page, int pageSize) =>
{
    (GroupCardDto[] Items, int TotalCount) result = await groups.GetListingsAsync(page, pageSize);
    return Results.Ok(new PagedResult<GroupCardDto>(result.Items, result.TotalCount));
});

// Client
public async Task<(GroupCardDto[] Items, int TotalCount)> GetListingsAsync(int page, int pageSize)
{
    PagedResult<GroupCardDto> result = (await Http.GetFromJsonAsync<PagedResult<GroupCardDto>>(
        $"api/groups?page={page}&pageSize={pageSize}"))!;
    return (result.Items, result.TotalCount);
}
```

### Streams and multipart

Three in-scope methods move bytes, not JSON — no interface signature changes, only the HTTP
transport:

- **Upload** (`IUserSettingsService.UploadProfilePictureAsync(Stream, string)`,
  `IContentImportService`'s import methods): client builds a `MultipartFormDataContent` with a
  `StreamContent` part; endpoint reads `IFormFile` via `[FromForm]`/`Request.Form.Files` and passes
  `file.OpenReadStream()`/`file.ContentType` straight through to the service — no buffering the
  whole file in memory beyond what `IFormFile` already does.
- **Download** (`IExportService`): the endpoint streams the file directly (`Results.File`/
  `Results.Stream`) with the right content-type/content-disposition headers; the client does **not**
  round-trip the bytes through a service call at all — the component points the browser at the
  endpoint URL directly (`<a href="/api/export/...">` or `NavigationManager.NavigateTo(url, forceLoad: true)`),
  same as any authenticated same-origin download. There is no `ClientExportService` HTTP-fetch-then-
  save-to-disk dance; that's what the direct-URL download already does natively in both circuit and
  WASM.
- **`StoryEditorPage`'s stray `IImageStorageService` injection.** The component calls
  `IImageStorageService.SaveAsync` directly instead of going through `IStoryWriteService`, unlike
  `IUserSettingsService.UploadProfilePictureAsync`'s pattern where the service owns the storage
  call. `IImageStorageService` itself stays server-only infra (never client-implemented — see
  "Avoid"); the fix is adding `IStoryWriteService.UploadCoverArtAsync(Stream, string, int storyId)`
  (mirroring `UploadProfilePictureAsync`'s shape) so the component goes through the same
  service-owns-the-upload pattern as Profiles, and the Stories cluster's multipart endpoint covers
  it like the other two upload cases above.

## The Error-Translation Contract

Components catch the service contract's **typed exceptions** (`TagValidationException` →
inline form error, etc.). That contract must survive the HTTP hop, so the boundary translates
symmetrically — endpoint maps exception→status, client impl maps status→exception. The full set,
extended past the original 3-case Tags pilot to every exception type actually thrown by the
service layer (audited 2026-07-12):

| Contract exception | Status | Message channel |
|---|---|---|
| `{Feature}ValidationException` (13 types), `ArgumentException`/`ArgumentOutOfRangeException`, `VouchLimitException`, `ImportException` | 400 | `Results.Problem(detail: ex.Message, statusCode: 400)` — `ProblemDetails.Detail` carries the user-facing message verbatim; client rethrows it |
| `UnauthorizedAccessException` | 403 (401 if unauthenticated — cookie handler emits it before the service runs) | none |
| `MessagingPermissionException`, `ContentRatingExceededException` | 403 | `Detail` carries the message |
| `KeyNotFoundException` | 404 — `Results.Problem(statusCode: 404)` | none |
| `WriteRateLimitExceededException` | 429 | `Detail` carries the message; `retryAfterSeconds` rides in the `ProblemDetails.Extensions` body (no response header — see `security.md` "Write Throttling") |
| `InvalidOperationException` | 401 | Auth safety net — every throw site is an "...requires an authenticated user" guard; every endpoint calling such a method also carries `RequireAuthorization()`, so the cookie handler's 401 normally wins the race first. `Detail` carries the message. |
| anything else (e.g. the one domain-invariant `NotSupportedException`) | 500 (unhandled) | client surfaces `HttpRequestException` via `EnsureSuccessStatusCode()` |

**Every API error status must be a bodied result (`Results.Problem`), never a bare
`Results.NotFound()`/`Results.StatusCode(...)`.** The app's
`UseStatusCodePagesWithReExecute("/not-found")` re-executes **body-less** error responses into
the HTML not-found route **with the original HTTP method** — a PUT/DELETE re-executed against
that GET-only page surfaces as 405 to the client (regression net: `TagEndpointsTests`).
Bodied results are skipped by that middleware; success statuses (`Results.Ok`,
`Results.NoContent`) are unaffected.

**Server side:** every write handler wraps in the **shared**
`EndpointHelpers.ExecuteWriteAsync(Func<Task<IResult>>)` (`Server/Http/EndpointHelpers.cs`) — one
copy of the table above, not a per-class try/catch. Validation-exception matching is by type-name
suffix (`ex.GetType().Name.EndsWith("ValidationException")`) rather than a shared marker base,
since the 13 `{Feature}ValidationException` types don't share one and retrofitting them was judged
out of scope for a WASM-add pass.

**Client side:** detail-extraction is shared (`ClientHttpHelpers.ReadProblemDetailAsync`/
`ReadRetryAfterSecondsAsync` in `Client/Http/ClientHttpHelpers.cs` — the private `ProblemPayload`
record MVC-free deserialization, so no class hand-rolls its own copy). Exception
**construction** stays per-class in each `Client{Feature}WriteService`'s own
`ThrowIfWriteFailedAsync(HttpResponseMessage)` — the `{Feature}ValidationException` constructor
shapes differ too much to share (some take `string message`, others `List<string>`/
`IReadOnlyList<string> errors`) — mirror the switch-on-status-code shape from `ClientTagWriteService`,
call the shared detail-reader, and construct the feature's own exception type per row above.

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
- **Self-referential services** (`IUserSettingsService` — spec's sanctioned read+write exception,
  `layer2-services.md` §"Self-Referential Editing Exception") get **one client class**
  implementing the whole interface directly, not a read/write inheritance split — there's no
  read-only consumer to separate from. The target user is resolved server-side from the cookie on
  every call; no `userId` parameter ever crosses the HTTP boundary.
- **Read-only interfaces** (no matching `*WriteService`, e.g. `IUserProfileReadService`,
  `IThemeReadService`, `ISiteDailyStatReadService`) get one client class with no base/subclass
  split at all — the read/write inheritance shape only applies where both sides of a CQRS-lite pair
  exist.

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

Rules verified against the official .NET 10 doc (learn.microsoft.com "Blazor prerendered state
persistence", 2026-07-13 — adopted app-wide in the Global Flip work-unit):

- **`public` properties only** — the framework uses reflection/source generation; a private or
  field-backed member silently never persists. Serialization is default-settings
  `System.Text.Json` embedded in the prerendered HTML.
- **`ValueTuple` state does not survive** — STJ doesn't serialize tuple fields. Pages that held
  `(Items, TotalCount)` tuples in one field split them into two persisted properties (or persist a
  `PagedResult<T>`). This is the same tuple/STJ trap as the endpoint boundary (§"Paged results").
- **Browser exposure**: the persisted payload is readable in the page source under WASM/Auto.
  Rule of thumb: persisting exactly what the page renders adds no exposure (the same data is in
  the prerendered HTML anyway); never persist a superset the UI filters down before display.
- **Internal SPA navigations don't prerender** — under global interactivity the interactive router
  handles them, so `OnInitializedAsync` runs exactly once with no persisted state and no double
  fetch. `[PersistentState]` only matters on full document loads (first visit, F5, external link).
  Consequence: route-param dispatcher pages (`OnParametersSetAsync` reload pattern) need
  restore-or-fetch on the *initial* load only; the param-change reload path never sees persisted
  state and must stay a plain fetch.
- **`@key` associates state to instances** when several same-type components render on one page.
- Options for special cases: `[PersistentState(AllowUpdates = true)]` lets enhanced-nav updates
  refresh read-only cached data; `RestoreBehavior.SkipInitialValue` / `SkipLastSnapshot` skip
  restore at prerender / at circuit reconnection respectively. Default (no options) is correct for
  this app's pages.
- **Scoped DI services can persist too** — `[PersistentState]` on the service's public property +
  `RegisterPersistentService<T>(RenderMode.InteractiveAuto)` at registration. Only scoped services.
- **Components that ALSO render on static-SSR pages cannot use `[PersistentState]`.** A
  `[PersistentState]` property registers a persistence callback whose render mode is inferred from
  the component — on a fully static render (the Identity pages, where App.razor's
  `AcceptsInteractiveRouting()` yields no render mode) there is nothing to infer and the framework
  throws `InvalidOperationException` at persist time, **500ing the whole page** (found live:
  `ReaderDisplayProvider` wraps the tree in Routes.razor and broke every `/Account/*` page). Such
  components use the manual API with the explicit-render-mode overload instead:
  `ApplicationState.RegisterOnPersisting(callback, RenderMode.InteractiveAuto)` — the one
  sanctioned exception to "don't hand-roll the register/dispose dance." Page components are never
  affected (they only render under the global interactive mode); only tree-wrapping providers and
  layout chrome shared with the Identity surface are.

## WASM renderer vs third-party DOM (Global Flip wave findings)

Two rules discovered live — both involve JS libraries that mutate DOM inside Blazor-tracked
regions, which the circuit renderer happened to tolerate and the WASM renderer does not:

1. **Same-component route redirects on Quill-hosting pages must `forceLoad: true`.** Quill
   (Blazored.TextEditor) inserts its toolbar as JS-created sibling DOM inside a Blazor-tracked
   parent. A cross-component navigation tears the region down root-first — safe. But a
   `NavigateTo` between two routes served by the SAME component (create→edit on
   Story/Chapter/BlogPost editors, chapter version switches) reuses the instance and applies a
   fine-grained in-place diff, which walks sibling lists Quill has altered →
   `TypeError: removeChild of null` → **the whole WASM renderer dies** (Blazored.TextEditor#71).
   `forceLoad: true` turns those redirects into full document navs; with `[PersistentState]` the
   reload hydrates from the prerender payload, so the cost is invisible. Grep target: any
   `NavigateTo` whose destination is a route of the same page hosting an `EditorView`.
2. **Blazored.Typeahead is REMOVED — use `SharedUI/Controls/CanalaveTypeahead.razor`.** The
   archived (2024-12) library's programmatic-Value-clear path (`Value = null` after a pick — the
   TagSelector pattern) crashed the WASM renderer the first time it ever ran under WASM
   (Blazored/Typeahead#221, never fixed upstream). The in-house replacement is 100%
   Blazor-managed DOM (no JS interop; one delegated `typeahead.js` keydown listener suppresses
   Enter form-submit), token-styled, and fully bUnit-testable (`CanalaveTypeaheadTests` covers the
   search→select path the old lib never could).

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
- **Client-implementing structurally server-only services**: `IImageStorageService` (infra —
  writes flow through the owning feature's write service instead, see "Streams and multipart"),
  `IHtmlSanitizationService` (sanitization must run server-side; called only from within write
  services, never a component), `IWriteRateLimitService` (server-side enforcement mechanism, called
  only from within services). None of these three are ever `@inject`ed by a component directly once
  `StoryEditorPage`'s `IImageStorageService` injection is fixed per "Streams and multipart" — if a
  future component injects one anyway, that's the bug to fix, not a cue to add a client impl.
