# Slice 0 — Shared Atom Clusters (Part 1 formal audit)

Audited 2026-07-17 by the main session (xhigh), per plan v3. Scope: RichText, Errors, Layout,
Controls, Pagination, Users, Toasts, Dialogs, Indicators, Drafts (~1.8k LOC, 36 files) across
Core/Server/SharedUI. All findings pending Tier-1/2 verification in Part 2d where marked.

**Overall read:** quality is high — atoms consistently match their documented conventions
(flyout catcher pattern, Outer Margin Rule, paren-form tokens, CSP-safe image fallbacks,
pull-on-submit editor contract, disposal on event subscriptions). Findings below are the
exceptions, dominated by two themes: silent-catch registry drift and doc-vs-code staleness
introduced by the Global Flip.

---

### MA-001 | Tier 2 | Bucket A | Slice 0
claim: Two silent `catch (Exception)` sites are neither logged at Warning nor annotated/registered per logging.md's no-silent-catches registry, whose sweep-grep contract claims only one sanctioned site exists.
evidence: `TheCanalaveLibrary.SharedUI/RichText/ReaderDisplayProvider.razor:84` — "catch (Exception)" (settings-load fallback, prose comment only); `TheCanalaveLibrary.SharedUI/Drafts/DraftAutosave.razor:127` — "catch (Exception)" (capture-tick skip, prose comment only). logging.md §"No silent catches": "A silent catch is legal only when annotated at the catch site: `// sanctioned-silent:` … Registry of sanctioned sites (keep current)" — registry lists only `ServerActiveUserContext.ResolvePrincipal`.
cells: cross-cutting (Errors/Drafts atoms; logging.md contract)
effort: S | route: mechanical sweep (log Warning per level-semantics table, or annotate + register)
verify: [pending]

### MA-002 | Tier 2 | Bucket A | Slice 0
claim: DraftAutosave's autosave loop has no general exception handling — one unexpected exception (e.g. a JS-interop failure in `Store.SaveAsync` during a transient disconnect) permanently and silently kills the draft-safety loop for the rest of the session, which is precisely the failure class the component exists to prevent (error-handling.md: "silent draft loss is the worst failure").
evidence: `TheCanalaveLibrary.SharedUI/Drafts/DraftAutosave.razor:88-119` — "catch (OperationCanceledException) { // Disposal — expected }" is the only catch; loop is launched fire-and-forget at line 86 ("_ = RunAutosaveLoopAsync(_cts.Token);") so a throw is an unobserved task exception.
cells: cross-cutting (Drafts atom; consumed by all four long-form editors)
effort: S | route: mechanical sweep (catch-log-continue inside the loop body)
verify: [pending]

### MA-003 | Tier 2 | Bucket A | Slice 0
claim: Logout `ReturnUrl` is stale after any SPA navigation — UserMenu and LoginDisplay capture the current URL once in their init lifecycle, but as persistent layout chrome they initialize once per circuit, so logging out from page N returns the user to page 1.
evidence: `TheCanalaveLibrary.SharedUI/Layout/UserMenu.razor:107` — "_currentUrl = Nav.ToBaseRelativePath(Nav.Uri);" (in OnInitializedAsync, no LocationChanged subscription); `TheCanalaveLibrary.SharedUI/Layout/LoginDisplay.razor:23` — same pattern in OnInitialized.
cells: F1 L3 (proposes reopen, Stage 5); layout chrome
effort: S | route: mechanical sweep (subscribe LocationChanged, or compute at render/submit time)
verify: [pending]

### MA-004 | Tier 2 | Bucket A | Slice 0
claim: identity-and-authorization.md's load-bearing rule "`IActiveUserContext` is server-only and will not exist in a future WASM Client… SharedUI components never inject `IActiveUserContext`" is falsified by post-Global-Flip code: a WASM twin exists and a SharedUI component injects the interface — a future session enforcing the written rule would "fix" working code, and one following the code would erode the rule's residual intent (auth-source discipline in SharedUI).
evidence: `TheCanalaveLibrary.SharedUI/Layout/UserActivityTracker.razor:14` — "@inject IActiveUserContext ActiveUser" (SharedUI); `TheCanalaveLibrary.Client/Program.cs:18` — "builder.Services.AddScoped<IActiveUserContext, WasmActiveUserContext>();"
cells: doc-vs-code (identity-and-authorization.md §"The two identity sources")
effort: S | route: seam — direction undetermined (update the doc to a narrowed rule, or re-implement the tracker via the auth-state cascade)
verify: [pending]

### MA-005 | Tier 2 | Bucket C | Slice 0
claim: CanalaveTypeahead does not guard the caller-supplied `SearchMethod` — a transient failure (most plausibly an HTTP error under the WASM runtime) propagates out of the input event handler into the hosting island's error boundary, degrading the whole form/panel instead of showing a "search failed/no results" row; additionally, Enter during an in-flight search selects from the stale previous result set (`_results` is not cleared when a new search starts).
evidence: `TheCanalaveLibrary.SharedUI/Controls/CanalaveTypeahead.razor:112-118` — "await Task.Delay(…); IEnumerable<TItem> found = await SearchMethod(_term);" (only TaskCanceledException is caught); `:149-151` — "case \"Enter\": await SelectAsync(_results[_highlight]);" while `_searching` may be true with stale `_results`.
cells: Controls atom (consumed by TagSelector, StoryTitlePicker)
effort: S | route: mechanical sweep
verify: [pending]

### MA-006 | Tier 3 | Bucket A | Slice 0
claim: ContentSurface hardcodes the three ReadingBackground palettes as raw hex in a `style=` attribute, which layer4-style.md declares a defect ("Raw hex in class strings or `style=` attributes is a defect once the sweep lands") — either tokenize the three palettes or record a sanctioned exception.
evidence: `TheCanalaveLibrary.SharedUI/RichText/ContentSurface.razor:35-37` — "Core.ReadingBackgroundEnum.Light => \"background-color:#FBFAF6;color:#262620\"" (+ Sepia/Dark arms).
cells: F7 L4 area (ContentSurface material)
effort: S | route: doc-touch decision (sanction) or mechanical (tokenize)
verify: [pending]

### MA-007 | Tier 3 | Bucket C | Slice 0
claim: ContentSurface retains the pre-ratification `FrameStyle` int parameter (magic values 2/3/default) that the component's own header says is removed once the gate ratifies a treatment — ratification happened 2026-07-10; the parameter survives only for the dev gallery's comparison switcher.
evidence: `TheCanalaveLibrary.SharedUI/RichText/ContentSurface.razor:47` — "[Parameter] public int FrameStyle { get; set; } = 2;" with header "the winning treatment becomes the only one and the parameter is removed".
cells: ContentSurface atom
effort: S | route: mechanical sweep (remove or convert gallery to a private variant)
verify: [pending]

### MA-008 | Tier 3 | Bucket C | Slice 0
claim: The 13-type validation-exception family has drifted shapes — `StoryValidationException` exposes `ValidationErrors` while the other ten multi-message types expose `Errors`, and (per layer5-wasm.md) constructor shapes also differ enough that client-side error translation cannot be shared — a small unification would simplify ExceptionPresenter and every `ThrowIfWriteFailedAsync`.
evidence: `TheCanalaveLibrary.Core/Errors/ExceptionPresenter.cs:59` — "StoryValidationException e => e.ValidationErrors," vs `:60-69` — ten arms of "e => e.Errors".
cells: cross-cutting (Core exception family)
effort: M | route: mechanical sweep (rename to one property; optional shared marker base — note layer5 judged the base out of scope for the WASM pass, not forever)
verify: [pending]

### MA-009 | Tier 3 | Bucket A | Slice 0
claim: DevLoginBar uses raw Tailwind palette classes, which layer4-style.md's token rule forbids ("raw palette/hex colors" fail the build via check-design-tokens.ps1) — and its passing CI implies the checker exempts this file/pattern, worth confirming as a deliberate dev-only exemption vs. a checker blind spot.
evidence: `TheCanalaveLibrary.SharedUI/Layout/DevLoginBar.razor:13-16` — "bg-yellow-50 border-b border-yellow-300", "text-yellow-700", "text-blue-600".
cells: dev-only chrome
effort: S | route: mechanical sweep (tokenize) or doc-touch (sanction dev-only exemption in the checker)
verify: [pending]

### MA-010 | Tier 3 | Bucket A | Slice 0
claim: layer4-style.md's own Pattern Accumulation entries still show the dead v3 bracket-form token syntax the same file declares "compiles to invalid CSS… renders as nothing" — a copy hazard inside the authoritative style doc (agents are told to copy these recorded patterns).
evidence: `.claude/skills/canalave-conventions/layer4-style.md:441-446` — PaginationControls entry: "`bg-[--color-surface-raised]` at rest with `hover:bg-[--color-primary]/20`" (also ChapterNavigation entry, and `primary` alias references the same doc says were deleted).
cells: doc-only
effort: S | route: doc-touch (update historical entries to paren form + current family names)
verify: [pending]

### MA-011 | Tier 3 | Bucket C | Slice 0
claim: ToastHost's show/auto-dismiss paths are unobserved fire-and-forget — after component disposal a pending `AutoDismissAsync` continues its `Task.Delay` then invokes on a disposed component, surfacing as an unobserved exception rather than being linked to the component lifetime.
evidence: `TheCanalaveLibrary.SharedUI/Toasts/ToastHost.razor:33-38` — "_ = InvokeAsync(…); _ = AutoDismissAsync(toast);" with no cancellation tied to Dispose.
cells: Toasts atom
effort: S | route: mechanical sweep
verify: [pending]

### MA-012 | Tier 3 | Bucket A | Slice 0
claim: `MainLayout.razor` (the Identity-Manage-hosting layout) still lives under `Server/Components/Layout/` — the folder family the Identity move was supposed to empty; the vertical convention places it in `Server/Identity/`.
evidence: `TheCanalaveLibrary.Server/Components/Layout/MainLayout.razor:1` — file path itself; SKILL.md "Legacy technical-layer folders" + the recorded `Components` → `Identity` rename history.
cells: F1 organization
effort: S | route: mechanical sweep (move + grep the old dotted-path namespace per SKILL.md's rename rule)
verify: [pending]

### MA-013 | Tier 3 | Bucket A | Slice 0
claim: layer5-wasm.md's Global Flip checklist still flags "Known instance: DevLoginBar injects IHostEnvironment… needs a client-side registration or an adapter" as an open item, but `WasmHostEnvironmentAdapter` was registered at the flip — stale doc item that would send a future session hunting a solved problem.
evidence: `.claude/skills/canalave-conventions/layer5-wasm.md:534-536` — the flagged instance; `TheCanalaveLibrary.Client/Program.cs:19` — "AddScoped<IHostEnvironment, WasmHostEnvironmentAdapter>()".
cells: doc-only
effort: S | route: doc-touch (mark resolved)
verify: [pending]

---

## Hypothesis results (slice 0)

- H-@key on stateful list children: ToastHost's `@foreach` carries `@key="toast.Id"` ✓; no other atom loops over stateful children — **clean**.
- H-route-param reload: no atom is a route-param dispatcher — **n/a** (UserMenu's once-per-circuit init produced MA-003, a sibling class: layout-chrome staleness on navigation).
- H-`@namespace` directive: present on every `.razor` atom read — **clean**.
- H-bare-name/bracket token traps: none in code (paren form throughout) — **clean in code**; doc itself carries bracket-form examples (MA-010).
- H-silent catches: two unregistered sites (MA-001).
- H-fire-and-forget: three sites (MA-002 loop launch, MA-011 toast, UserActivityTracker's `_ =` ping — the last is documented loss-tolerant-by-contract, not flagged).
