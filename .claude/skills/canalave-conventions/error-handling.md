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
| Page | `DesktopLayout`/`MobileLayout` around `@Body` | Full panel + Try again |
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

## Deferred (Phase-5-adjacent follow-up — not designed here)

`ProblemDetails` API error envelope + client-service HTTP error translation. InteractiveServer
calls services in-process; there is no HTTP error to shape until the WASM client makes those
calls. Design it when Phase 5 gives it a testable surface. `NavigationManager.NotFound()`
continues to cover the 404 case.
