# Patterns-Inventory Dimensions (ratified at checkpoint; fixed headings thereafter)

Every slice inventory uses EXACTLY these section headings, in this order, so cross-slice diffing
is mechanical. Per section: `mechanism:` (what the slice's clusters actually do), `exemplar:`
(one file:line), `deviations:` (intra-slice divergences with file:line, or "none observed").

1. **Pagination** — offset vs keyset; PagedResult envelope usage; PaginationControls vs bespoke
2. **DTO mapping** — two-step row-projection vs direct Select; record vs class DTOs; tuple returns
3. **Error surfacing** — typed-exception catch shape; ExceptionPresenter usage; InlineAlert vs other
4. **Form patterns** — EditForm+ViewModel vs @code state; enum/bool `<select>` idiom (two sanctioned patterns); validation tiering
5. **Flyout/overlay mechanics** — catcher-div vs `<details>` vs modal; z-token usage; dismissal wiring
6. **Optimistic updates & debounce** — where used, timer home, flush semantics
7. **Disposal & lifecycle** — IDisposable/IAsyncDisposable presence where subscriptions/CTS/JS exist; OnAfterRender vs OnInitialized for interop
8. **Query shape** — factory-per-method compliance; ApplyFilters-style composition vs inline predicates; Include vs projection; AsSplitQuery use
9. **Write-method skeleton** — observed ordering of: auth guard → rate limit → validate → existence checks → sanitize → save → counters → badges → notifications
10. **Endpoint & client shape** — route naming, ExecuteWriteAsync usage, ThrowIfWriteFailedAsync shape, nullable-read helpers
11. **Sanitization & derived fields** — sanitize-on-save presence; word-count/strip helpers
12. **Notification triggering** — best-effort post-commit try/catch shape; semantic method usage
13. **Counter updates** — ExecuteUpdateAsync discipline; transition-delta for toggles
14. **Test idioms** — seeding style, selector strategy (aria-label vs index), absolute assertions, FK-parent seeding comments
15. **Code economy** (ratified 2026-07-17; reframed same day — NOT about feature scope) — for the slice's FIXED feature set: (a) per-cluster product+test LOC and pattern-tax share (endpoints/client-impl/DTO/config boilerplate); (b) **compression candidates**, each with three numbers + a cost: LOC saved, sites collapsed, and the new machinery/indirection the reader would inherit; (c) near-identical file pairs (esp. Desktop/Mobile components — measure actual structural difference against the codebase's own "separate only when structurally different" rule) and near-duplicate DTOs; (d) mechanical repetition attributable to a fixable root cause (cite the MA finding); (e) **false economies considered and rejected** — disciplined repetition that DRY-ing would worsen, named so the question stays answered. Classify candidates: pure win / trade / false economy. No verdicts on trades — Brian decides those.
