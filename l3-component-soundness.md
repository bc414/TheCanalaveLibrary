# L3/L3.5 Component-Soundness Wave — Lifecycle Reload + List Keying

## Context

A first-principles soundness audit of Layer 3 (Razor component data-handling & logic) and the
objective half of Layer 3.5 (structure: data-flow, composition, list identity) — the same rigor
applied to L1/L2 — found the component layer mostly sound (optimistic-like handlers snapshot +
re-find-index-after-await + roll back; debounce disposes correctly; leaves are injection-free;
ownership computed at dispatchers). But **three genuinely unsound, compile-clean patterns** surfaced.
All are the WU38 class: structurally wrong, Stage-5-labelled, cheap now, and they will only bite
during the upcoming human end-to-end pass. The user chose to fix **all three**.

Each finding was verified against current official .NET 10/11 documentation (Blazor moves fast):
- **F1:** [MS Learn — component lifecycle](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/lifecycle),
  [Blazor University](https://blazor-university.com/components/component-lifecycles/): navigating to the
  *same* page component reuses the instance, does **not** re-run `OnInitialized{Async}`; new route
  values arrive via `SetParametersAsync` → `OnParametersSet{Async}`.
- **F2/F3:** [MS Learn — retain element/component/model relationships](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/element-component-model-relationships):
  without `@key`, Blazor preserves child instances *positionally*, so a reused instance keeps its
  private state while bound to a new model; `@key` by unique id preserves the instance↔model mapping.

## The three findings

**F1 — Route-param dispatchers load only in `OnInitializedAsync` → stale content on in-place nav (HIGH, systemic).**
`ChapterNavigation` / tab strips navigate via plain `<a href>` (Router intercepts, no reload, same
component instance). `OnInitializedAsync` doesn't re-fire, so the page keeps the previous param's
data. The Router ([Routes.razor](../../RiderProjects/TheCanalaveLibrary/TheCanalaveLibrary.Server/Components/Routes.razor))
uses a plain unkeyed `AuthorizeRouteView` — nothing mitigates it. `MessagesPage` is the ONLY
route-param page that implements `OnParametersSet` (proof the rest are oversights). Confirmed-biting
core flows: chapter prev/next/version, profile tabs, bookshelf tabs.

**F2 — Unkeyed `StoryDeck`→`StoryCard`→`UserStoryInteractionPanel` bleeds & mis-writes interaction state (HIGH, data-corrupting).**
`StoryDeck` `@foreach` has no `@key`; `UserStoryInteractionPanel` caches `_localState` once
(`if (_localState is null)`). On pagination/filter-swap the panel instance is reused positionally,
keeps story A's toggles while bound to story B's `StoryId`, and a toggle's `FlushAsync` writes A's
booleans onto B. Affects Discovery sorted pagination, Bookshelves, Profile story tabs, Groups.

**F3 — Unkeyed `CommentSection`→`CommentItem` bleeds the spoiler-reveal flag across pages (MED-HIGH, spoiler bypass).**
`CommentItem._isRevealed` is instance-local; on pagination the reused item at slot *i* renders a
different, un-revealed spoiler comment as already revealed.

## Plan

### Phase 0 — Doc-Touch moment 1 (conventions, before code)

These are two new conventions for *built* code, so record them first:

1. **`.claude/skills/canalave-conventions/layer3-logic.md`** — add a "Route-parameter dispatchers
   reload in `OnParametersSetAsync`" rule near "Page Dispatcher: Entity Not Found". State: load logic
   keyed on route `[Parameter]`s must run in `OnParametersSetAsync` with a changed-key guard (cache
   the last-loaded key, early-return if unchanged), NOT `OnInitializedAsync`, because same-component
   navigation reuses the instance (cite the two findings' mechanism). `MessagesPage` is the reference.
   One-time/identity work that must NOT repeat (auth resolution) stays in `OnInitializedAsync`.
2. **`.claude/skills/canalave-conventions/layer3.5-structure.md`** — add a "`@key` on `@foreach` over
   stateful children" rule near "StoryDeck and StoryCard" / "Loading States". State: any `@foreach`
   rendering a component that holds instance state (optimistic caches, ephemeral reveal/menu flags,
   debounce) MUST carry `@key` on a stable domain id, or positional reuse bleeds state across entities.
   Pure-display leaves don't require it but may key for consistency.

### Phase 1 — F1: per-page `OnParametersSetAsync` (chosen over global router `@key`)

For each route-param dispatcher that loads in `OnInitializedAsync` — **all except `MessagesPage`,
which is the template** — split the lifecycle:
- Keep one-time identity work (auth-state resolution → `_currentUserId`) in `OnInitializedAsync`.
- Move param-dependent loads into `OnParametersSetAsync`, guarded by a cached last-loaded key so an
  unrelated re-render doesn't refetch.

Pages (verify each during impl; confirmed core-flow ones first):
- `Chapters/ChapterReadingPage.razor` — key `(StoryId, ChapterNumber, VersionOrder)`; also reset the
  per-chapter scroll-JS registration (`_jsRegistered`) so a new chapter re-registers `OnScrollProgress`.
- `Profiles/ProfilePage.razor` — key `(UserId, Tab)`; it already has `LoadTabPayloadAsync()` — call it
  on `Tab` change. Reload banner only when `UserId` changes.
- `Bookshelves/BookshelvesPage.razor` — key `Tab`.
- `Stories/StoryPage.razor` — key `StoryId` (preserve the `[PersistentState]` `??=` first-load guard).
- `Groups/GroupPage.razor` — key `GroupId`. `BlogPosts/BlogPostPage.razor` — key `BlogPostId`.

### Phase 2 — F2 & F3: `@key` on stateful `@foreach` lists

- `Stories/StoryDeck.razor` — `@key="story.StoryId"` on `<StoryCard>`.
- `Comments/CommentSection.razor` — `@key="root.CommentId"` on the root `<CommentItem>` and
  `@key="reply.CommentId"` on the reply `<CommentItem>`.
- `Recommendations/RecommendationSection.razor` — `@key="rec.RecommendationId"` on `<RecommendationCard>`
  (pure-display leaf; keyed for consistency + future-proofing).
- Grep other `@foreach`-over-component sites for stateful children (e.g. messaging threads,
  notification lists) and key any that hold instance state.

### Phase 3 — Tests (RazorComponents / bUnit tier)

- **F1:** for `ChapterReadingPage` and `ProfilePage`, `SetParametersAndRender` twice with different
  route params on the same instance; assert the second render reflects the new param's data (fails
  pre-fix, passes post-fix).
- **F2:** render `StoryDeck` with story A + its state, re-render with story B at the same slot; assert
  the panel shows B's toggles, not A's.
- **F3:** render `CommentSection`/`CommentItem`, reveal a spoiler, swap in a different spoiler comment
  at the same slot; assert it renders hidden.

### Phase 4 — Doc-Touch moment 3 (after green)

- Run `dotnet test` (all three tiers; should be green, +the new RazorComponents tests).
- **Audit Stage notes** (narrative): `Stories.md` (StoryDeck/panel F2, StoryPage + ChapterReadingPage
  F1), `Comments.md` (CommentSection F3), `Recommendations.md` (keying), `Profiles.md` (ProfilePage F1),
  Bookshelves/Groups/BlogPosts audit files (F1). Each: what was unsound, the doc-backed fix, covering
  test tier (RazorComponents).
- **`status.md`:** these cells were labelled Stage 5 but were unsound (a Stage-4 condition); they
  return to Stage 5 *within this wave*, so no net grid-number change — record the reconciliation in the
  audit Stage notes, not status.md (per Doc-Touch). Add a short Global-conditions pointer only if
  warranted.
- **`workplan.md`:** new `WU-L3Soundness` entry, DONE with date, pointing at this file + audit notes.

## Verification

- `dotnet build` green (0 warnings); `dotnet test` green (1232 existing + new RazorComponents tests).
- Manual boot gate (the human pass this pre-empts): page a Discovery deck / switch profile+bookshelf
  tabs / click chapter next-prev — content updates and interaction badges track the correct story;
  reveal a spoiler then paginate — the next spoiler stays hidden.
