# Error Handling Strategy

(settled 2026-07-06, WU-ErrorHandling / decision row 9). Split out of `cross-cutting.md`
(2026-07-07) — already self-contained, referenced as its own unit by `SKILL.md` and `logging.md`.

Three purposes, arbitrated deliberately: user trust & agency (did it break, whose fault, is my
work lost, what next), operator observability (every failure path reaches `logging.md`'s
contract), and blast-radius containment (a fault never costs more UI state than its own island).
On a writing site, silent draft loss is the worst failure — worse than any message.

## Layered error boundaries (containment)

`CanalaveErrorBoundary` (`SharedUI/Errors/`) subclasses `ErrorBoundary`: logs the exception at
`Error` with a `{Boundary}` label + `{ErrorId}` (trace id), renders a design-language fallback
with a **Try again** (`Recover()`) button and the error id, auto-`Recover()`s on navigation so
error state never traps the user across pages. Placement is layered:

| Boundary | Where | Fallback |
|---|---|---|
| Page | `MainLayout` around `@Body` | Full panel + Try again |
| Chrome | around the layout's bell/messages/menu group | Compact one-liner |
| Card | `StoryDeck`, per `<StoryCard>` | Compact tile |
| Comments | each `<CommentSection>` consumer site | Compact panel |

An unhandled render/lifecycle/event exception now degrades the *island*, not the circuit — the
circuit (and every other island's state, including in-server drafts) survives. Boundaries do NOT
catch background-`Task` continuations or exceptions thrown outside their subtree; the
`#blazor-error-ui` bar (in `App.razor`, so it exists on every page — it was previously stranded
in `MainLayout`, leaving interactive pages with *no* teardown surface at all) remains the true
last resort, restyled to the design language, alongside the restyled `ReconnectModal`.

## Feedback channels (hybrid)

- **Inline** (`InlineAlert`, `SharedUI/Errors/`) — the channel for form/validation feedback,
  rendered next to what it's about. All forms use it instead of hand-rolled danger divs.
- **Toast** (`IToastService` + `ToastHost`, `SharedUI/Toasts/`) — transient, auto-dismissing,
  `aria-live`; ONLY for non-blocking system events with no inline home (e.g. "draft restored").
  Never for validation errors, never for anything requiring a decision.

## Exception-message discipline

Only typed user-facing exceptions may show their message. `ExceptionPresenter`
(`Core/Errors/`) is the single mapper: the `*ValidationException` family (+ the other Core
user-ready types) surface their messages; `UnauthorizedAccessException`/`KeyNotFoundException`
map to fixed friendly text (BCL messages are dev text — never show them);
everything else maps to a generic message. Raw `ex.Message` in UI is a defect. Catch sites
follow `logging.md` "No silent catches": typed→translate (no log needed); unexpected→log
`Error` with IDs, show the generic message. What an unexpected error *shows* pairs with what it
*logs* — the on-screen error id equals the log/trace id.

## Editor draft safety

Long-form editors (story/chapter/blog-post edit pages) autosave the in-progress draft to
browser localStorage (`DraftAutosave` component + `draft-autosave.js`, `SharedUI/Drafts/`)
every ~10s and offer restore on load; a successful submit clears the backup. Device-local by
design: survives circuit teardown, network drop, reload, and browser crash, and works
identically under InteractiveServer and future WASM (`[PersistentState]` was rejected — it only
bridges the prerender→interactive handoff). Drafts are the user's own unsaved input on their
own device — not server state; no sanitization concern until submit (sanitize-on-save
unchanged).

## The API error envelope (WU-ErrorHandling2, settled 2026-07-30)

`ProblemDetails` API error envelope + full client-service HTTP error translation. The original
deferral rationale (pre-flip, InteractiveServer called services in-process — no HTTP error
existed to shape) expired at the Global Flip (DONE ✓ 2026-07-13): the WASM client now genuinely
makes HTTP calls (per `layer5-wasm.md`'s `ServerXXXService`/HTTP-endpoint/client-impl pattern),
so a testable HTTP error surface exists. Partial coverage had already shipped 2026-07-18
(per-service 400-arm validation translation, the MA-008 unified validation-exception family +
shared client translator); WU-ErrorHandling2 completes the contract: every `/api/*` failure —
read or write, expected or not — is a bodied `ProblemDetails`, and every client service
reconstructs the contract's typed exception from it. `NavigationManager.NotFound()` continues to
cover the 404-document case; this section covers the JSON API surface only.

**Server side.** `Program.cs` registers `AddProblemDetails()` plus an `ApiExceptionHandler :
IExceptionHandler` scoped to `/api/*` only (returns `false` for everything else, so the existing
`UseExceptionHandler("/Error")` HTML path and `Error.razor` are untouched): an unhandled
exception under `/api` is logged at `Error` with the trace id (`logging.md` §"Unhandled
exceptions") and answered with a 500 `ProblemDetails` carrying a `traceId` extension. Every
write **and** read handler that calls a service capable of throwing a typed exception wraps in
`EndpointHelpers.ExecuteAsync(Func<Task<IResult>>)` — the single copy of the status mapping.
(Renamed from `ExecuteWriteAsync`: the mapping was never write-specific, and reads throw the same
typed exceptions writes do — `ServerTreeSearchReadService`'s `ArgumentException`s,
`ServerFanonReadService`'s `UnauthorizedAccessException`, `ServerMessagingReadService`'s
`KeyNotFoundException`, etc. An unwrapped read endpoint turns a legitimate 400/403/404 into a
500.) Every API error status is a **bodied** `Results.Problem`, never a bare
`Results.NotFound()`/`Results.StatusCode(...)` — see `layer5-wasm.md` §"The Error-Translation
Contract" for the full table and the binary/asset exemption list (image/export downloads: GET-only,
no JSON contract to protect).

**Client side.** `ClientHttpHelpers` is the single translation seam: `ThrowIfWriteFailedAsync`
covers writes, `ThrowIfReadFailedAsync` (added by this WU) covers reads with no per-feature
validation type. Both read the envelope's `traceId` off the response body. Two new Core
exception types complete the table:

| Status | Exception | Note |
|---|---|---|
| 401 | `SessionExpiredException` | The cookie handler's bare 401 *and* a service's `InvalidOperationException`→401 arm both reconstruct as this — a **session** signal, not an authorization denial (403 stays `UnauthorizedAccessException`; see `identity-and-authorization.md`). |
| 5xx (unhandled) | `ServerFaultException(string? traceId)` | Replaces the bare `EnsureSuccessStatusCode()` fall-through when the body is a `ProblemDetails` envelope; carries the **server's** trace id so the id a user reports is the id of the request that actually failed, correct under both InteractiveServer and the WASM hop (`Activity.Current` is null in WASM — the client has no OpenTelemetry). |

**Channel per failure kind** (extends "Feedback channels" above): validation → `InlineAlert`;
permission (403) / not-found (404) → `InlineAlert`; session-expired (401) → `InlineAlert` +
an inline **Sign in** affordance (`SharedUI/Errors/ErrorAlert.razor` — carries the current path
as `ReturnUrl`; the user stays on the page so `DraftAutosave` keeps unsaved work, unlike a hard
redirect); unexpected/5xx → `ExceptionPresenter`'s generic message with the server trace id,
boundary-caught if it escapes a component catch site. Toast stays reserved for non-blocking
system events only — never any of the above. `ErrorAlert` is the presentation counterpart to
`ExceptionPresenter`: components hold the caught `Exception` and render `<ErrorAlert
Error="_error" />` instead of hand-rolling `ExceptionPresenter.GetUserMessages` +
`InlineAlert` each site.

**Logging note:** `ServerFaultException` is deliberately **not** user-facing
(`ExceptionPresenter.IsUserFacing` excludes it — it's the generic-message path) but must not be
double-logged at the client catch site: the failure was already logged at `Error` server-side
when the envelope was produced; the client only re-presents the trace id.
