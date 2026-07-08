# Layer 3 — UI Logic (Contract + Behavior)

The `@code` block of Razor components: what the component needs from outside (Contract) and
what it does in response to events and lifecycle (Behavior). Pure C# — decidable entirely
from the spec and data model with no visual design dependency.

## Contract: Parameters, Services, Events

Every component's public API consists of three kinds of declarations:

```razor
@code {
    // What it receives from its parent
    [Parameter] public StoryListingDto Story { get; set; } = default!;
    [Parameter] public bool IsCompact { get; set; }

    // What it raises back to its parent
    [Parameter] public EventCallback<int> OnFavoriteToggled { get; set; }

    // What it fetches independently (pages and cross-cutting components only)
    @inject IStoryReadService StoryService
}
```

**Leaf components:** Parameters and EventCallbacks only. No service injection.

**Composite components:** Parameters flowing through, plus coordination state. Service injection
only for genuinely independent concerns (typeahead queries, self-contained writes).

**Page/dispatcher components:** Service injection, route `[Parameter]`s, `IDeviceDetectionService`.

### Service Injection Principle

Inject a service when the component has a genuinely independent concern that cannot or should
not be coordinated from above. The constraint that IS rigid: **pure display components that show
pre-loaded data must never inject read services** (prevents N+1 in lists).

Legitimate non-page injection:
- Cross-cutting layout elements (notification bell — no parent dispatcher owns this data)
- User-input-driven queries (tag typeahead — bubbling keystrokes to parent is absurd)
- Self-contained writes (follow button, comment like — parent doesn't need the result)
- **Coordinated paginated regions** (`CommentSection` pattern — the composite injects
  `ICommentWriteService : ICommentReadService` to own its own paginated load + all writes
  in one independent region; this is not an N+1 violation because the component is a single
  per-page self-contained region, not an item in a list).

## State Persistence: `[PersistentState]` (.NET 10)

.NET 10 replaces the manual `PersistentComponentState` dance with a **declarative attribute**:

```razor
@code {
    [PersistentState]
    public StoryListingDto[]? Stories { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Stories ??= await StoryService.GetListingsAsync();
    }
}
```

**Rules:**
- Property must be **`public`** (framework uses reflection for trimming/source-gen).
- The `??=` guard is the key pattern — set only when not already restored from prerender state.
- **Security:** persisted state is exposed to the browser under `InteractiveAuto`. **Never persist
  sensitive/private data** this way.
- For multiple instances of the same component, pair with `@key`.

### Persisting Service State Across the Handoff

For state held in a scoped DI service (not a component), annotate the service property and register:

```csharp
public class CounterTracker
{
    [PersistentState]
    public int CurrentCount { get; set; }
}

builder.Services.AddScoped<CounterTracker>();
builder.Services.AddRazorComponents()
    .RegisterPersistentService<CounterTracker>(RenderMode.InteractiveAuto);
```

## The Component @code IS the ViewModel

For display components, the component's `@code` block serves the ViewModel role: computed display
properties, ephemeral UI state, and display enrichment on top of DTO data.

```razor
@code {
    [Parameter] public StoryListingDto Story { get; set; } = default!;

    private string WordCountDisplay => Story.WordCount switch {
        < 1_000     => $"{Story.WordCount} words",
        < 1_000_000 => $"{Story.WordCount / 1000.0:F0}K words",
        _           => $"{Story.WordCount / 1_000_000.0:F1}M words"
    };

    private bool _synopsisExpanded = false;
}
```

None of this crosses the service boundary. No separate class needed.

## When a Separate ViewModel Class IS Needed

1. **EditForm binding.** `EditForm` requires a bound model. `DataAnnotations` need a class.
   Applies to: story create/edit, profile tagline, account management text fields.
2. **Shared form shape.** Two pages share the same editable fields.
3. **Complex testable logic.** Computed properties worth unit testing without rendering.
4. **Service-owned persisted state.** `RegisterPersistentService<T>` requires a class for DI.

### What does NOT need a ViewModel class

WYSIWYG editor surfaces (Quill doesn't use `InputText`), toggle interactions (booleans in `@code`),
selection state (`List<Tag>` in `@code`), navigation controls, settings toggles, `ResultsFilterPanel`
coordination state. For all of these the component's `@code` is the model.

## Forms (When ViewModels Apply)

```razor
<EditForm Model="viewModel" OnValidSubmit="HandleSubmit">
    <DataAnnotationsValidator />
    <InputText @bind-Value="viewModel.Title" />
    <ValidationMessage For="() => viewModel.Title" />
    <button type="submit">Save</button>
</EditForm>

@code {
    private StoryEditViewModel _viewModel = new();
    private bool _isSubmitting;

    // Read-path data for dropdowns — NOT in the ViewModel
    private ContentRatingEnum[] _ratingOptions = Enum.GetValues<ContentRatingEnum>();
}
```

Dropdown/selection options are separate `@code` fields, not in the ViewModel.

### Nested Validation with `[ValidatableType]` (.NET 10)

.NET 10 supports nested object validation without custom validators. Mark a complex type with
`[ValidatableType]` and `DataAnnotationsValidator` validates it recursively:

```csharp
[ValidatableType]
public class StoryEditViewModel
{
    [Required, StringLength(200)]
    public string Title { get; set; } = "";

    [ValidatableType]
    public StoryMetadataViewModel Metadata { get; set; } = new();
}
```

Useful for ViewModels containing grouped settings or multi-section forms. Simplifies the
three-tiered validation strategy at Tier 1.

### When to Drop to the Manual PersistentComponentState Service

The imperative `PersistentComponentState` service (`RegisterOnPersisting` / `PersistAsJson` /
`TryTakeFromJson`) is the escape hatch for complex serialization, runtime-computed keys, or
conditional persistence. .NET 10 adds `RegisterOnRestoring` for imperative control over how
state is restored (complementing `RegisterOnPersisting` for persistence). Use `[PersistentState]`
by default; drop to the service only when the attribute's behavior doesn't fit.

## Optimistic Updates & Debounce

For high-frequency interactions (Favorite/Follow/Ignore buttons):

1. **Optimistic local update** on click — toggle the bool, re-render immediately.
2. **2-second per-component debounce** (`InteractionConstants.InteractionDebounceMs` in
   `Core/UserStoryInteractions/`, default 2000ms — lives in **Core**, not Server's `SiteConstants`,
   because `SharedUI` cannot reference Server; spec's literal `SiteConstants.InteractionDebounceMs`
   wording is a historical artifact).
3. When the timer fires, one API call for that one story.

The debounce timer lives in the coordination composite (`UserStoryInteractionPanel`), not in
individual leaf buttons.

**Distinct from typeahead debounce.** `TagSelector`'s `Debounce="300"` on `BlazoredTypeahead` (WU11)
governs input responsiveness for a third-party widget's own search-as-you-type — the package manages
that timer internally. `InteractionDebounceMs` (2000ms) governs batching optimistic writes in a
coordination composite we own. Same word, two unrelated concerns, two different homes — don't
conflate them or try to unify the constants.

## UserStoryInteractionButton — EventCallback-Driven Behavior

No mode enum. The presence or absence of `OnToggle` determines behavior:

```razor
@code {
    [Parameter] public bool IsActive { get; set; }
    [Parameter] public EventCallback<bool> OnToggle { get; set; }
    [Parameter, EditorRequired] public string IconPath { get; set; } = "";    // SVG path d-attribute
    [Parameter, EditorRequired] public string AccentColor { get; set; } = ""; // CSS color
    [Parameter] public string Label { get; set; } = "";                       // aria-label + title
    private bool IsReadOnly => !OnToggle.HasDelegate;
}
@if (!IsReadOnly || IsActive)
{
    <button @onclick="HandleClick" aria-label="@Label" title="@Label" ...>
        <svg ...><path d="@IconPath" /></svg>
    </button>
}
```

**Icon delivery (WU7):** `IconPath`/`AccentColor` are inline SVG — not a sprite URL, see
`layer4-style.md` "Interaction Icons Are Inline SVG." The owning composite maps
`InteractionTypeEnum` → `(IconPath, AccentColor)`, not the sprite service.

**Two presentation contexts (6 buttons total — enum declaration order is the canonical left-to-right
order: `Favorite → PrivateFavorite → Follow → Complete → ReadLater → Ignore`):**
- **Listing context** (StoryCard): `ReadLater` and `Ignore` receive `OnToggle` (clickable).
  `Favorite`, `PrivateFavorite`, `Follow`, `Complete` receive no `OnToggle` (read-only, visible only
  when true — so story deck cards show favorited/followed/**completed** as status badges).
- **Detail context** (story page): all 6 receive `OnToggle` (all clickable).

**Visibility rules:** `ReadLater` and `Ignore` are visible only when the story is a blank slate
(not favorited, not hidden-favorited, not followed, **not completed**, not actively reading) OR when
already active.

**Own story:** The author's own story replaces the interaction panel with an Edit Story button.
The panel composite receives `IsOwnStory` parameter.

## Spoiler Comment State

`IsSpoiler` on `ChapterComment` — completion-gated reveal:

```razor
@code {
    // Passed from ChapterPage dispatcher (already loaded for interaction panel)
    [Parameter] public bool UserHasCompletedStory { get; set; }

    // Ephemeral — re-hides on every page load
    private bool _isRevealed = false;

    private void HandleRevealClick()
    {
        if (UserHasCompletedStory)
            _isRevealed = true;
        else
            _showConfirmDialog = true; // "You haven't finished. Are you sure?"
    }
}
```

## Component Injection Rules

- **Inject interfaces, never `DbContext` or concrete services.**
- **Components are render-mode agnostic.** A component in SharedUI must work under both server and WASM.
- Use `RendererInfo.IsInteractive` only when genuinely needed:

```razor
@if (!RendererInfo.IsInteractive)
{
    <p>Loading...</p>
}
else
{
    @ChildContent
}
```

## Page Dispatcher: Entity Not Found (.NET 10)

Use `NavigationManager.NotFound()` in page dispatchers when the requested entity doesn't exist:

```csharp
@inject NavigationManager Nav

protected override async Task OnInitializedAsync()
{
    var story = await StoryService.GetDetailAsync(StoryId);
    if (story is null)
    {
        Nav.NotFound();
        return;
    }
    Story = story;
}
```

Replaces manual navigation to error pages. The framework routes to the designated Not Found page.

## Route-Parameter Dispatchers Reload in `OnParametersSetAsync`

When a page component's route template contains a parameter (e.g. `{StoryId:int}`, `{Tab}`), in-place
navigation between two URLs that match the *same template* — such as clicking chapter prev/next, or
switching a profile/bookshelf tab — **reuses the existing component instance**. The Blazor Router
intercepts the link and calls `SetParametersAsync` on the existing object; `OnInitialized{Async}` does
**not** re-fire.

Any data load that is keyed on a route `[Parameter]` therefore **must run in `OnParametersSetAsync`**
(guarded, see below), not only in `OnInitializedAsync`. Placing the load exclusively in
`OnInitializedAsync` compiles cleanly and works on the first visit, but silently shows stale content on
every subsequent in-place navigation — the classic WU38 class of unsound compile-clean patterns.

### The required pattern (MessagesPage is the reference)

```csharp
private bool _initialized;
private int? _loadedConversationId = int.MinValue; // sentinel; no real id equals this

protected override async Task OnInitializedAsync()
{
    // ── One-time / identity work ─────────────────────────────────────────────
    // Auth resolution, user-id extraction, things that must NOT repeat.
    _currentUserId = await ResolveCurrentUserIdAsync(AuthState);

    // ── First param-dependent load ───────────────────────────────────────────
    await LoadDataAsync();           // same method OnParametersSetAsync uses
    _initialized = true;
}

protected override async Task OnParametersSetAsync()
{
    if (!_initialized) return;                    // skip the first pass (races OnInitializedAsync)
    if (RouteParam == _loadedRouteParam) return;  // skip unrelated re-renders (parent re-renders etc.)

    // Reset any stale local state, then reload.
    _data = null;
    await LoadDataAsync();
}

private async Task LoadDataAsync()
{
    _loadedRouteParam = RouteParam;   // record the key at the start of the load
    // ... actual service calls ...
}
```

**Rules:**
- `_loadedXxx` cache field seeded with a sentinel value (`int.MinValue`, `""`, etc.) so the first real
  load is never accidentally short-circuited by an uninitialized default.
- `_initialized` prevents `OnParametersSetAsync` from racing `OnInitializedAsync` on the first render
  pass (both fire; the flag makes the two cooperate cleanly).
- Extract the load body into a named method (`LoadDataAsync`, `LoadChapterAsync`, `LoadTabPayloadAsync`,
  etc.) so it is callable from both lifecycle points without duplication.
- When a single page has **two independently-keyed parameters** (e.g. `UserId` and `Tab`), cache them
  separately and only reload the portion that actually changed.

**One-time work that MUST stay in `OnInitializedAsync` only (never repeated on param change):**
- Auth state → `_currentUserId` extraction (auth doesn't change mid-session).
- Default-tab redirects, bad-slug `Nav.NotFound()` on parse failure (the redirect itself fires the new
  nav; a repeat would double-redirect).
- Any resource that is invariant to route-param changes.

**`[PersistentState]` interaction:** if a page uses `[PersistentState]` with a `??=` first-load guard
for anti-flicker (e.g. `Story ??= await Service.GetAsync(StoryId);`), the `??=` is correct in
`OnInitializedAsync` (where prerender state may already populate the field), but a **plain assignment**
(`Story = await Service.GetAsync(StoryId);`) is required in `OnParametersSetAsync` for a new key —
otherwise the non-null persisted field short-circuits the reload. Reset persisted fields to `null`
before the reload.

**Transient UI state survives in-place navigation too.** Instance reuse doesn't just skip data
reloads — open modals, error flags, compose buffers, and any other transient UI field carry over
into the "new" page. When a handler ends by calling `NavigateTo` to a URL matching the same
template (e.g. compose → navigate to the new entity), **close/reset the transient state before
navigating**; nothing else will. Reference fix: MessagesPage's compose modal
(`_composeOpen = false` before `Nav.NavigateTo($"/messages/{newId}")` — it survived into the new
thread otherwise; found in the L4.5 browser pass, 2026-07-02).

## Parameters: DTOs and Primitives

**Pass the DTO** when a component renders multiple fields from it:
```razor
[Parameter] public StoryListingDto Story { get; set; } = default!;
```

**Pass primitives** when a component only needs one value:
```razor
[Parameter] public bool IsActive { get; set; }
[Parameter] public EventCallback<bool> OnToggle { get; set; }
```

Don't decompose a DTO into individual primitive parameters — that defeats the stable contract.

## Razor Attribute Quoting

**Inner double-quotes inside `@onchange="..."` terminate the attribute early and cause CS1525.**

If you write `@onchange="e => Cast((e.Value ?? "0"))"` the inner `"0"` closes the attribute — the
Razor parser sees the attribute end there and treats `))"` as stray characters, producing a cryptic
CS1525 parse error.

**Fix: use a block lambda and `int.TryParse` instead of a default-fallback literal:**

```razor
@onchange="e => { if (int.TryParse(e.Value?.ToString(), out int rv)) _myEnum = (MyEnum)rv; }"
```

This avoids any string literal inside the attribute value. The `int.TryParse` block lambda is the
canonical form for all enum `<select>` onchange handlers in this codebase (e.g. `AuthorSettingsForm`,
`PrivacySettingsForm`, `AppearanceSettingsForm`).

### Enum `<select>` — two valid patterns, never mix their halves

`@bind` on an enum-typed property **does** work on a `<select>`, but it serializes the current
value as the enum **name** (`.ToString()`), so it only ever matches `<option>` values that are
also the enum name. The two coherent patterns:

1. **`@onchange` block lambda + numeric option values** (`value="@((int)v)"` + the `int.TryParse`
   lambda above) — the canonical settings-form pattern.
2. **`@bind` on the enum property + name option values** (`value="@type"` inside an
   `Enum.GetValues<T>()` loop) — `TagEditorForm` is the reference.

Mixing them — `@bind` with numeric option values — compiles clean and renders a **blank select**
whenever the bound value should be pre-selected, because nothing matches. Found live in the L4.5
browser pass (`audit/Tags.md`, 2026-07-01).

### String parameters take attribute text literally

For a **string-typed** component parameter, `Param="_myField"` passes the literal text
`"_myField"` — not the field's value. Only `@`-prefixed values (`Param="@_myField"`) compile as
expressions. Non-string-typed parameters compile the attribute text as an expression either way —
that asymmetry is why the trap survives review: identical syntax is correct on the `bool`
parameter above and wrong on the `string` parameter below it. Rule: **always `@`-prefix identifier
values regardless of the parameter's type.** Sweep for the bug class with `="_\w+"` (plus
suspicious PascalCase values on string params) — it was hit seven times before being swept
(`audit/Messaging.md`, `audit/Tags.md`).

## Component Tier × Logic Summary

| Tier | Service Injection | Parameters | State | Lifecycle |
|---|---|---|---|---|
| Leaf | Never | DTOs for multi-field display, primitives for single values, EventCallbacks | Computed display properties from DTOs, trivial internal fields | None |
| Composite | Rarely (independent concerns only) | DTOs flowing through, configuration primitives | Coordination state (debounce, mode, dropdown open) | Rarely |
| Page | Always (primary data loading) | Route `[Parameter]`s only | Loaded DTOs with `[PersistentState]`, ViewModels for EditForm | `OnInitializedAsync` (one-time/identity) + `OnParametersSetAsync` (param-keyed loads, guarded — see "Route-Parameter Dispatchers Reload in `OnParametersSetAsync`"); loading/error states |
